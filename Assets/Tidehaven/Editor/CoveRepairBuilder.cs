using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public static class CoveRepairBuilder
{
    const string Folder = "Assets/Tidehaven/CoastalRefresh";
    static System.Random random;
    static float R(float a, float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
    static Color C(string h) { ColorUtility.TryParseHtmlString(h,out var c); return c; }
    static Material foliage, limestone;
    class Geometry
    {
        public List<Vector3> v = new List<Vector3>();
        public List<Color> c = new List<Color>();
        public List<int> t = new List<int>();
        public void Tri(Vector3 a, Vector3 b, Vector3 d, Color color) { int n=v.Count;v.Add(a);v.Add(b);v.Add(d);c.Add(color);c.Add(color);c.Add(color);t.Add(n);t.Add(n+1);t.Add(n+2); }
        public void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color color) {Tri(a,b,d,color);Tri(a,d,e,color);}
        public Mesh Mesh(string name) {var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
        public void Tube(Vector3 a,Vector3 b,float ra,float rb,Color col,int n=8)
        {
            var axis=(b-a).normalized;var side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(axis,side);
            for(int i=0;i<n;i++){float th=i*2*Mathf.PI/n,th2=(i+1)*2*Mathf.PI/n;var p=side*Mathf.Cos(th)+up*Mathf.Sin(th);var q=side*Mathf.Cos(th2)+up*Mathf.Sin(th2);var shade=col*Mathf.Lerp(.85f,1.08f,(Mathf.Sin(th)+1)/2);shade.a=1;Quad(a+p*ra,a+q*ra,b+q*rb,b+p*rb,shade);Tri(b,b+p*rb,b+q*rb,col);Tri(a,a+q*ra,a+p*ra,col);}
        }
        public void Blob(Vector3 center,Vector3 size,Color col,int seed,int n=10,int rings=6)
        {
            Vector3 Point(int j,int i){float a=i*2*Mathf.PI/n,lat=-Mathf.PI/2+j*Mathf.PI/rings;float wobble=1+.09f*Mathf.Sin(a*3+seed+lat*2)+.05f*Mathf.Cos(a*5+lat*3);return center+Vector3.Scale(new Vector3(Mathf.Cos(lat)*Mathf.Cos(a)*wobble,Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(a)*wobble),size);}
            for(int j=0;j<rings;j++)for(int i=0;i<n;i++){var color=col*Mathf.Lerp(.81f,1.12f,(j+1f)/rings);color.a=1;Quad(Point(j,i),Point(j+1,i),Point(j+1,i+1),Point(j,i+1),color);}
        }
        // Chamfered rectangular stone/wood with broad planar faces and clipped corners.
        public void Block(Vector3 center,Vector3 size,float bevel,Color col,float yaw=0,int seed=0)
        {
            var ring=new[]{new Vector2(-.72f,-1),new Vector2(.72f,-1),new Vector2(1,-.72f),new Vector2(1,.72f),new Vector2(.72f,1),new Vector2(-.72f,1),new Vector2(-1,.72f),new Vector2(-1,-.72f)};
            var rot=Quaternion.Euler(0,yaw,0);var pts=new Vector3[4,8];
            for(int j=0;j<4;j++)for(int i=0;i<8;i++){float shrink=j==0||j==3?1-bevel:1;float yy=j==0?-.5f:j==1?-.5f+bevel*.5f:j==2?.5f-bevel*.5f:.5f;float w=seed==0?1:1+.05f*Mathf.Sin(i*1.7f+seed);pts[j,i]=center+rot*new Vector3(ring[i].x*size.x*.5f*shrink*w,yy*size.y,ring[i].y*size.z*.5f*shrink*w);}
            for(int j=0;j<3;j++)for(int i=0;i<8;i++)Quad(pts[j,i],pts[j+1,i],pts[j+1,(i+1)%8],pts[j,(i+1)%8],col);
            for(int i=0;i<8;i++){Tri(center+Vector3.up*size.y*.5f,pts[3,(i+1)%8],pts[3,i],col);Tri(center-Vector3.up*size.y*.5f,pts[0,i],pts[0,(i+1)%8],col);}
        }
    }
    static Mesh Save(Geometry g,string name)
    {
        var m=g.Mesh(name);string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);return old;}AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject MeshObject(string name,Mesh m,Material mat,Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static GameObject Prefab(GameObject go,string name) {var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");Object.DestroyImmediate(go);return prefab;}
    static GameObject Spawn(GameObject source,Transform parent,Vector3 pos,float scale=1,float yaw=0)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;return go;
    }
    static GameObject Tree()
    {
        var root=new GameObject("Coastal broadleaf tree");var lod=new List<LOD>();
        for(int level=0;level<2;level++){
            var g=new Geometry();g.Tube(Vector3.zero,new Vector3(.10f,2.55f,.07f),.20f,.075f,C("#766044"),8);
            for(int k=0;k<6;k++){float a=k*2.4f;var end=new Vector3(Mathf.Cos(a)*.9f,2.5f+k*.17f,Mathf.Sin(a)*.8f);g.Tube(new Vector3(.08f,1.2f+k*.18f,0),end,.08f,.025f,C("#8C7453"),6);}
            for(int k=0;k<7;k++){float a=k*2.399f;float rad=k==6?0:1.03f;var center=new Vector3(Mathf.Cos(a)*rad,3.05f+(k%3)*.36f+(k==6?.7f:0),Mathf.Sin(a)*rad);g.Blob(center,new Vector3(1.04f,.86f,.96f),C(k%3==0?"#6F9B45":k%3==1?"#86A653":"#587F3C"),k,level==0?12:7,level==0?7:4);
                if(level==0)for(int j=0;j<3;j++){float b=j*2.1f+a;g.Blob(center+new Vector3(Mathf.Cos(b)*.65f,.38f,Mathf.Sin(b)*.65f),new Vector3(.42f,.32f,.46f),C("#9CB966"),k+j,7,4);}}
            var child=MeshObject("Broadleaf LOD"+level,Save(g,"CoastalBroadleaf_LOD"+level),foliage,root.transform);lod.Add(new LOD(level==0?.12f:.018f,new Renderer[]{child.GetComponent<Renderer>()}));
        }
        var group=root.AddComponent<LODGroup>();group.SetLODs(lod.ToArray());group.RecalculateBounds();var col=root.AddComponent<CapsuleCollider>();col.center=new Vector3(0,1.1f,0);col.height=2.2f;col.radius=.21f;return Prefab(root,"CoastalBroadleaf");
    }
    static GameObject Bush()
    {
        var g=new Geometry();for(int i=0;i<8;i++){float a=i*2.399f;g.Blob(new Vector3(Mathf.Cos(a)*.38f,.32f+(i%3)*.12f,Mathf.Sin(a)*.3f),new Vector3(.42f,.37f,.4f),C(i%2==0?"#719247":"#91AA56"),i,8,5);}
        var go=MeshObject("Rounded coastal shrub",Save(g,"RoundedShrub"),foliage,null);return Prefab(go,"RoundedShrub");
    }
    static void Surface(Transform root,string name,Vector3 center,Vector3 size,float tilt=0)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=center;go.transform.localRotation=Quaternion.Euler(tilt,0,0);var c=go.AddComponent<BoxCollider>();c.size=size;go.AddComponent<CoastalWalkableSurface>();
    }
    static GameObject Dock()
    {
        var g=new Geometry();var wood=C("#A08A65");var dark=C("#6F6049");
        // Long, continuous deck runs out from the shore; generous T-shaped fishing platform.
        for(int i=0;i<27;i++){
            float z=-7.7f+i*.4f;float width=i<6?5.2f:2.5f;
            g.Block(new Vector3(0,.39f,z),new Vector3(width,.18f,.374f),.09f,Color.Lerp(wood,C("#B9A27A"),R(0,1)));
            for(int j=0;j<3;j++){float x=R(-width*.32f,width*.32f);g.Block(new Vector3(x,.483f,z+R(-.12f,.12f)),new Vector3(R(.3f,.8f),.003f,.012f),.03f,dark);}
            foreach(float x in new[]{-width*.40f,width*.40f})g.Tube(new Vector3(x,.485f,z),new Vector3(x,.493f,z),.027f,.027f,C("#4A504A"),6);
        }
        foreach(float x in new[]{-.92f,.92f})g.Block(new Vector3(x,.22f,-2.5f),new Vector3(.18f,.22f,10.9f),.1f,dark);
        float[] posts={-7.9f,-5.5f,-3f,-.5f,2.5f};
        foreach(float side in new[]{-1f,1f}){
            Vector3 previous=Vector3.zero;
            for(int i=0;i<posts.Length;i++){
                float z=posts[i],x=side*(i<2?2.58f:1.26f);var top=new Vector3(x,1.30f,z);
                g.Tube(new Vector3(x,-1.6f,z),top,.14f,.115f,C("#7E7056"),10);
                g.Tube(top,top+Vector3.up*.055f,.14f,.14f,C("#B6A079"),10);
                for(int wrap=0;wrap<3;wrap++)for(int n=0;n<10;n++){float a=n*2*Mathf.PI/10,b=(n+1)*2*Mathf.PI/10;g.Tube(new Vector3(x+Mathf.Cos(a)*.124f,1.12f+wrap*.04f,z+Mathf.Sin(a)*.124f),new Vector3(x+Mathf.Cos(b)*.124f,1.12f+wrap*.04f,z+Mathf.Sin(b)*.124f),.018f,.018f,C("#C1AD7D"),5);}
                if(i>0){Vector3 prev=previous;for(int j=1;j<=12;j++){float t=j/12f;var p=Vector3.Lerp(previous,top,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*.19f+.12f);if(j==1)prev=previous-Vector3.up*.12f;g.Tube(prev,p,.027f,.027f,C("#B5A273"),6);prev=p;}}
                previous=top;
            }
        }
        // A shallow boarded ramp meets the beach without a vertical collision lip.
        for(int i=0;i<6;i++){float z=3.1f+i*.32f;float y=.48f+(z-2.9f)*.16f;g.Block(new Vector3(0,y-.09f,z),new Vector3(2.5f,.18f,.31f),.08f,wood);}
        var root=MeshObject("Timber fishing dock",Save(g,"TimberDock"),foliage,null);
        Surface(root.transform,"Continuous walking deck",new Vector3(0,.35f,-2.5f),new Vector3(2.5f,.26f,10.8f));
        Surface(root.transform,"Fishing platform",new Vector3(0,.35f,-6.7f),new Vector3(5.2f,.26f,2.4f));
        Surface(root.transform,"Shore access ramp",new Vector3(0,.55f,3.9f),new Vector3(2.5f,.18f,2.15f),-9.09f);
        return Prefab(root,"TimberFishingDock");
    }
    static GameObject Mountain(int variant)
    {
        var root=new GameObject("Grass capped limestone massif "+variant);var g=new Geometry();var caps=new Geometry();
        float h=5.5f+variant*.35f;
        // Offset layers and irregular shoulders make a broad geological formation.
        for(int row=0;row<3;row++)for(int col=0;col<3;col++){
            float x=(col-1)*2.8f+R(-.18f,.18f),z=R(-.18f,.18f);float y=.85f+row*(1.68f+variant*.175f);
            g.Block(new Vector3(x,y,z+row*.25f),new Vector3(R(3.1f,3.5f),1.9f,5.1f-row*.25f),.19f,C("#A0AAA0"),R(-5,5),variant*15+row*3+col+1);
        }
        g.Block(new Vector3(-4.4f,1.5f,.65f),new Vector3(2.8f,3.4f,4.3f),.25f,C("#A2ADA3"),-16,5+variant);
        g.Block(new Vector3(4.2f,2,.8f),new Vector3(3f,4.2f,4f),.22f,C("#A2ADA3"),12,9+variant);
        caps.Block(new Vector3(0,h-.33f,.5f),new Vector3(8.3f,.32f,4.8f),.17f,C("#6E984C"),0,variant+1);
        caps.Block(new Vector3(-4.35f,3.2f,.65f),new Vector3(2.6f,.22f,4.1f),.20f,C("#7DA454"),-16,5+variant);
        caps.Block(new Vector3(4.2f,4.13f,.8f),new Vector3(2.7f,.20f,3.8f),.20f,C("#7A9E50"),12,9+variant);
        var rock=MeshObject("Layered rounded limestone",Save(g,"LimestoneMassif"+variant),limestone,root.transform);
        MeshObject("Thick meadow cap",Save(caps,"MeadowCap"+variant),foliage,root.transform);
        var obstacle=root.AddComponent<BoxCollider>();obstacle.center=new Vector3(0,2.5f,.5f);obstacle.size=new Vector3(10,5,4.7f);
        return Prefab(root,"LimestoneMassif"+variant);
    }
    static void Flowers(Transform root)
    {
        var g=new Geometry();for(int i=0;i<440;i++){
            float x=R(-24,24),z=R(4,18.6f);if(CoastalIslandLayout.Grass(x,z)<.8f||CoastalIslandLayout.PathDistance(x,z)<1.05f)continue;
            if(Vector2.Distance(new Vector2(x,z),new Vector2(-14,14))<4.1f||Vector2.Distance(new Vector2(x,z),new Vector2(17,14))<3)continue;
            var p=new Vector3(x,CoastalIslandLayout.Height(x,z)+.035f,z);for(int j=0;j<3;j++){
                var c=p+new Vector3(R(-.3f,.3f),R(0,.04f),R(-.3f,.3f));for(int leaf=0;leaf<3;leaf++){float a=leaf*2.094f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(d,Vector3.up)*.075f;g.Quad(c,c+d*.11f+side,c+d*.20f,c+d*.11f-side,C("#7B9A4D"));}
                if(i%3==0){c+=Vector3.up*.09f;for(int petal=0;petal<5;petal++){float a=petal*1.2566f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(d,Vector3.up)*.035f;g.Quad(c,c+d*.055f+side,c+d*.10f,c+d*.055f-side,C(i%2==0?"#EEE6C2":"#E4C6BF"));}g.Blob(c+Vector3.up*.01f,new Vector3(.025f,.015f,.025f),C("#D7B75B"),1,5,3);}
            }
        }
        var go=MeshObject("Low clover and scattered meadow flowers",Save(g,"LowMeadowFlowers"),foliage,root);go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    static void BatchShrubs(Transform root)
    {
        var shrubs=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh.name=="RoundedShrub").ToArray();
        foreach(var chunk in shrubs.GroupBy(f=>new Vector2Int(Mathf.FloorToInt(f.transform.position.x/8),Mathf.FloorToInt(f.transform.position.z/8))))
        {
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(chunk.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),true,true);
            string name="ShrubGarden_"+chunk.Key.x+"_"+chunk.Key.y;
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name=name;
            if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            MeshObject(name,mesh,foliage,root);
        }
        foreach(var f in shrubs)Object.DestroyImmediate(f.gameObject);
    }

    [MenuItem("Tidehaven/Repair walking and refresh coast")]
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before rebuilding.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(!scene.path.EndsWith("/CoralCove.unity"))throw new System.InvalidOperationException("Open CoralCove.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Tidehaven","CoastalRefresh");
        random=new System.Random(25102026);
        foliage=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CoastalPalette.mat");if(!foliage){foliage=new Material(Shader.Find("Tidehaven/Stylized Vertex Lit"));AssetDatabase.CreateAsset(foliage,Folder+"/CoastalPalette.mat");}foliage.SetFloat("_OutlineWidth",0);foliage.SetFloat("_ColorVibrance",.98f);foliage.enableInstancing=true;
        limestone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CliffStone.mat");
        if(!limestone){limestone=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Expansion/Limestone.mat"));AssetDatabase.CreateAsset(limestone,Folder+"/CliffStone.mat");}
        limestone.SetColor("_BaseColor",new Color(.53f,.59f,.58f,1));
        limestone.SetColor("_MossColor",new Color(.46f,.52f,.47f,1));
        limestone.SetFloat("_Weathering",.85f);limestone.enableInstancing=true;
        var old=GameObject.Find("Coastal refresh • dock, cliffs and woodland");if(old)Object.DestroyImmediate(old);
        var root=new GameObject("Coastal refresh • dock, cliffs and woodland").transform;
        var compact=GameObject.Find("Compact cove • planted gardens and rock wall");
        if(compact)foreach(Transform t in compact.transform)if(t.name!="Garden waterfall in rock wall")t.gameObject.SetActive(false);
        var nature=GameObject.Find("Nature • palms, grasses and shore rocks");
        foreach(Transform t in nature.transform)if(t.name.ToLowerInvariant().Contains("grass")||t.name.Contains("Meadow"))t.gameObject.SetActive(false);
        var oldDock=GameObject.Find("Harbor").transform.Find("Dock");if(oldDock)oldDock.gameObject.SetActive(false);
        var tree=Tree();var bush=Bush();var dock=Dock();Spawn(dock,root,new Vector3(12.5f,0,0));
        for(int i=0;i<6;i++){
            float x=-24+i*9.5f,z=23.1f+Mathf.Sin(i*1.9f)*.6f;var mountain=Mountain(i%2);var massif=Spawn(mountain,root,new Vector3(x,1.0f,z),1,i%2==0?-3:4);
            float rise=new[]{.85f,1.18f,.92f,1.08f,1.28f,.94f}[i];
            massif.transform.localScale=new Vector3(1,rise,1);
            float top=1+(5.5f+(i%2)*.35f-.17f)*rise;
            for(int j=0;j<3;j++)Spawn(tree,root,new Vector3(x-2.7f+j*2.6f,top,z+R(-.4f,1.1f)),R(.70f,1.03f),R(0,360));
            for(int j=0;j<5;j++)Spawn(bush,root,new Vector3(x+R(-3.3f,3.3f),top,z+R(-1.5f,.9f)),R(.7f,1.3f),R(0,360));
        }
        Vector2[] trees={new Vector2(-22,12),new Vector2(-19,17),new Vector2(-8,16),new Vector2(8,16),new Vector2(22,16),new Vector2(24,10)};
        foreach(var p in trees)Spawn(tree,root,new Vector3(p.x,CoastalIslandLayout.Height(p.x,p.y),p.y),R(.8f,1.05f),R(0,360));
        // Replace the costly repeating berry shrubs with a shared, compact mesh.
        int shrubs=0;foreach(Transform t in nature.transform)if(t.name=="BerryBush"){var p=t.position;p.y=CoastalIslandLayout.Height(p.x,p.z);Spawn(bush,root,p,R(.8f,1.2f),R(0,360));t.gameObject.SetActive(false);shrubs++;}
        Flowers(root);
        BatchShrubs(root);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);
        int oversized=0;foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if((r.name.StartsWith("Reef_Expanded_")||r.name.StartsWith("Coral_Expanded_"))&&r.bounds.size.magnitude>20){r.gameObject.SetActive(false);oversized++;}
        var game=Object.FindFirstObjectByType<TidehavenGame>();var controller=game.player.GetComponent<CharacterController>();controller.minMoveDistance=0;controller.stepOffset=.35f;controller.skinWidth=.025f;
        game.player.position=new Vector3(0,CoastalIslandLayout.Height(0,3.7f),3.7f);
        EditorUtility.SetDirty(foliage);EditorUtility.SetDirty(limestone);EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        // Preserve camera culling when this tool regenerates the dock and meadow meshes.
        var coast=GameObject.Find("Sandy coast and seabed");
        if(coast&&coast.transform.Find(CoveVisibilitySetup.RootName))CoveVisibilitySetup.Run();
        return "Saved coast refresh: new dock, 6 grass-capped massifs, 24 LOD trees, compact shrubs (replaced "+shrubs+" heavy bushes), low meadow flowers; disabled "+oversized+" oversized reef copies and old spike grass.";
    }
}



