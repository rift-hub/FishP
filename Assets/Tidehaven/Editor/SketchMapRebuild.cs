using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>Rebuilds the supplied map sketch using the existing coastal asset library.</summary>
public static class SketchMapRebuild
{
    const string Folder="Assets/Tidehaven/SketchMap";
    const string RootName="Sketch map - terraced cove";
    static Transform root;
    static Material ground,rock,plants;
    static System.Random rng;
    static float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
    static Vector3 Land(float x,float z)=>new Vector3(x,CoastalIslandLayout.Height(x,z),z);
    static Color ColorOf(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    static Transform Group(string name){var g=new GameObject(name);g.transform.SetParent(root,false);return g.transform;}
    static T Save<T>(T obj,string name) where T:Object {
        string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(obj,old);Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(obj,path);return obj;
    }
    static GameObject MeshObject(string name,Mesh mesh,Material mat,Transform parent){
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void GroundObject(GameObject go,float x,float z,float bury=.03f){var b=BoundsOf(go);go.transform.position+=Land(x,z)-new Vector3(b.center.x,b.min.y+bury,b.center.z);}
    static GameObject Clone(GameObject source,string name,float x,float z,float height,Transform parent){
        var go=Object.Instantiate(source,parent);go.name=name;go.SetActive(true);go.transform.rotation=Quaternion.Euler(0,R(0,360),0);
        go.transform.localScale*=height/Mathf.Max(.01f,BoundsOf(go).size.y);GroundObject(go,x,z);return go;
    }
    static Mesh Kit(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Tidehaven/Meshes/Kit_"+name+".asset");
    static GameObject Plant(string kit,string name,float x,float z,float scale,Transform parent,bool solid=false){
        var mesh=Kit(kit);var go=MeshObject(name,mesh,plants,parent);go.transform.position=Land(x,z)-Vector3.up*mesh.bounds.min.y*scale;
        go.transform.rotation=Quaternion.Euler(0,R(0,360),0);go.transform.localScale=Vector3.one*scale;
        if(solid){var c=go.AddComponent<CapsuleCollider>();c.center=new Vector3(0,.65f,0);c.height=1.3f;c.radius=.13f;}return go;
    }
    static float Slope(float x,float z){float dx=(CoastalIslandLayout.Height(x+.15f,z)-CoastalIslandLayout.Height(x-.15f,z))/.3f;float dz=(CoastalIslandLayout.Height(x,z+.15f)-CoastalIslandLayout.Height(x,z-.15f))/.3f;return Mathf.Sqrt(dx*dx+dz*dz);}
    static bool Clear(float x,float z,float margin=2f){return CoastalIslandLayout.PathDistance(x,z)>margin&&Vector2.Distance(new Vector2(x,z),CoastalIslandLayout.House)>4.2f&&Vector2.Distance(new Vector2(x,z),new Vector2(-22,27))>3.2f&&Slope(x,z)<.35f;}
    static void Terrain(Transform parent){
        const int n=32;const float step=.25f;
        for(int tz=0;tz<9;tz++)for(int tx=0;tx<10;tx++){
            float x0=-40+tx*8,z0=-24+tz*8;var vs=new Vector3[(n+1)*(n+1)];var cs=new Color[vs.Length];var uv=new Vector2[vs.Length];var ns=new Vector3[vs.Length];
            var land=new List<int>();var stone=new List<int>();
            for(int j=0;j<=n;j++)for(int i=0;i<=n;i++){
                float x=x0+i*step,z=z0+j*step;int k=j*(n+1)+i;float g=CoastalIslandLayout.Grass(x,z);vs[k]=Land(x,z);uv[k]=new Vector2(x,z);
                cs[k]=Color.Lerp(ColorOf("#DACAA1"),ColorOf("#73974B"),g);cs[k].a=g;
                ns[k]=new Vector3(CoastalIslandLayout.Height(x-.08f,z)-CoastalIslandLayout.Height(x+.08f,z),.16f,CoastalIslandLayout.Height(x,z-.08f)-CoastalIslandLayout.Height(x,z+.08f)).normalized;
            }
            for(int j=0;j<n;j++)for(int i=0;i<n;i++){
                int k=j*(n+1)+i;float x=x0+(i+.5f)*step,z=z0+(j+.5f)*step;float h=CoastalIslandLayout.Height(x,z);float rd=CoastalIslandLayout.RampDistance(x,z,out _);
                bool cliff=Slope(x,z)>.70f&&h>1.18f;
                bool shelf=h>5.5f&&h<6.2f&&z-CoastalIslandLayout.Front(x)<5.2f&&rd>1.8f;
                var t=cliff||shelf?stone:land;t.AddRange(new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2});
            }
            var m=new Mesh{name="Terrace tile "+tx+"_"+tz,vertices=vs,colors=cs,uv=uv,normals=ns,subMeshCount=2};m.SetTriangles(land,0);m.SetTriangles(stone,1);m.RecalculateBounds();m=Save(m,"Terrain_"+tx+"_"+tz);
            var go=MeshObject(m.name,m,ground,parent);go.GetComponent<MeshRenderer>().sharedMaterials=new[]{ground,rock};go.AddComponent<MeshCollider>().sharedMesh=m;go.isStatic=true;
        }
    }
    static void Flora(Transform parent){
        var bins=new Dictionary<Vector2Int,List<CombineInstance>>();
        for(int i=0;i<4200;i++){
            float x=R(-33,33),z=R(5,41);if(CoastalIslandLayout.Grass(x,z)<.48f||!Clear(x,z,1.65f))continue;
            if(CoastalIslandLayout.Height(x,z)<4&&rng.NextDouble()<.65)continue;
            var key=new Vector2Int(Mathf.FloorToInt(x/8),Mathf.FloorToInt(z/8));if(!bins.TryGetValue(key,out var bin)){bin=new List<CombineInstance>();bins[key]=bin;}
            var mesh=Kit(i%8==0?"Wildflower_Patch":"Grass_TuftCluster");float s=R(.65f,1.2f);
            bin.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(Land(x,z)+Vector3.up*.015f,Quaternion.Euler(0,R(0,360),0),Vector3.one*s)});
        }
        foreach(var pair in bins){var m=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray(),true,true);MeshObject("Grass and wildflowers "+pair.Key,Save(m,"Grass_"+pair.Key.x+"_"+pair.Key.y),plants,parent);}
    }

    [MenuItem("Tidehaven/Rebuild map from reference sketch")]
    public static string Run(){
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play before rebuilding.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(!scene.path.EndsWith("CoralCove.unity"))throw new System.InvalidOperationException("Open CoralCove first.");
        if(GameObject.Find(RootName))throw new System.InvalidOperationException("Sketch map already exists; restore the backup before a full rebuild.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Tidehaven","SketchMap");
        rng=new System.Random(251026);Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Rebuild terraced cove from sketch");int undo=Undo.GetCurrentGroup();
        var nature=GameObject.Find("Nature • palms, grasses and shore rocks");var reef=GameObject.Find("Living reef");var refresh=GameObject.Find("Coastal refresh • dock, cliffs and woodland");
        var palm=nature.transform.Cast<Transform>().First(t=>t.name=="Palm").gameObject;
        var coral=reef.transform.Cast<Transform>().First(t=>t.name=="Coral").gameObject;var colony=reef.transform.Cast<Transform>().First(t=>t.name=="Reef").gameObject;var seaweed=reef.transform.Cast<Transform>().First(t=>t.name=="Seagrass").gameObject;
        var dockSource=refresh.transform.Find("TimberFishingDock").gameObject;
        root=new GameObject(RootName).transform;
        plants=Save(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Materials/StylizedPropOutlined.mat")),"Plants");
        plants.SetFloat("_OutlineWidth",0);plants.SetFloat("_ColorVibrance",1);
        ground=Save(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Materials/CoastalGround.mat")),"Ground");ground.SetFloat("_UseMeadowMask",0);
        rock=Save(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Expansion/Limestone.mat")),"Rock");rock.SetColor("_MossColor",new Color(.48f,.53f,.39f,1));
        Terrain(Group("01 - beach, rock terrace and surrounding hills"));
        var trees=Group("02 - forest and planted hilltops");var occupied=new List<Vector2>();int treeCount=0;
        for(int i=0;i<1500&&treeCount<75;i++){
            float x=R(-32.5f,32.5f),z=R(7,40.8f);var p=new Vector2(x,z);
            if(CoastalIslandLayout.Height(x,z)<5.5f||CoastalIslandLayout.Grass(x,z)<.85f||!Clear(x,z,2.9f)||occupied.Any(q=>Vector2.Distance(p,q)<2.8f))continue;
            // Keep the house clearing open; the forest occupies the upper-right area.
            if(x<0&&CoastalIslandLayout.Hill(x,z)<.9f)continue;
            Plant(treeCount%4==0?"Tree_CoastalPine":"Tree_CoastalBroadleaf","Hilltop tree "+treeCount,x,z,R(1.05f,1.55f),trees,true);occupied.Add(p);treeCount++;
        }
        Vector2[] palms={new Vector2(-22,12),new Vector2(-21,17),new Vector2(-19,21),new Vector2(-8,7),new Vector2(10,5),new Vector2(21,5),new Vector2(-22,30)};
        foreach(var p in palms)Clone(palm,"Coastal palm",p.x,p.y,R(3.7f,4.7f),trees);
        var bushes=Group("03 - bushes, grass and flowers");int shrubs=0;
        for(int i=0;i<1100&&shrubs<105;i++){
            float x=R(-32,32),z=R(6,40.5f);if(CoastalIslandLayout.Grass(x,z)<.6f||!Clear(x,z,1.9f))continue;
            Plant("Shrub_GoldenOlive","Hilltop bush "+shrubs++,x,z,R(.45f,.80f),bushes);
        }Flora(bushes);
        var rocks=Group("04 - coastal stones");for(int i=0;i<50;i++){
            float x=R(-25,25),z=CoastalIslandLayout.Shore(x)+R(.1f,2.3f);if(CoastalIslandLayout.PathDistance(x,z)<2||Vector2.Distance(new Vector2(x,z),CoastalIslandLayout.Spawn)<3)continue;
            Plant(i%3==0?"Rock_SteppedSlate":"Rock_SmoothCoastal","Shore stone",x,z,R(.35f,.9f),rocks);
        }
        var harbor=GameObject.Find("Harbor");var house=harbor.transform.Find("House");Undo.RecordObject(house,"Move house to upper left");GroundObject(house.gameObject,-16,28,.07f);
        var shop=harbor.transform.Find("Shop");Undo.RecordObject(shop,"Move shop beside house");GroundObject(shop.gameObject,-22,27,.07f);
        var dock=Object.Instantiate(dockSource,root);dock.name="05 - ocean fishing dock";dock.transform.position+=new Vector3(-9.5f,.28f,-1f);
        var ocean=Group("06 - coral gardens and swimming fish");
        Vector2[] gardens={new Vector2(-19,1),new Vector2(-13,-1),new Vector2(-7,-4),new Vector2(0,-5),new Vector2(9,-3),new Vector2(17,-4),new Vector2(24,-5),new Vector2(-25,-5),new Vector2(-17,-8),new Vector2(13,-10)};
        for(int i=0;i<gardens.Length;i++){
            var p=gardens[i];float depth=-CoastalIslandLayout.Height(p.x,p.y);Clone(colony,"Reef garden "+(i+1),p.x,p.y,Mathf.Min(1.25f,depth-.2f),ocean);
            for(int j=0;j<5;j++){float x=p.x+R(-2.2f,2.2f),z=p.y+R(-1.5f,1.5f);float h=Mathf.Min(R(.45f,.95f),-CoastalIslandLayout.Height(x,z)-.18f);if(h>.2f)Clone(j<3?coral:seaweed,j<3?"Branching coral":"Ocean seagrass",x,z,h,ocean);}
        }
        var game=Object.FindFirstObjectByType<TidehavenGame>();Undo.RecordObject(game,"Reposition reef fish");
        for(int i=0;i<game.fish.Count;i++){
            var f=game.fish[i];if(!f)continue;Undo.SetTransformParent(f,ocean,"Place fish in ocean");Undo.RecordObject(f,"Position fish");
            var p=gardens[i%gardens.Length];f.position=new Vector3(p.x+R(-1.4f,1.4f),R(-.85f,-.42f),p.y+R(-.5f,.5f));f.gameObject.SetActive(true);
        }
        // Keep prior authoring groups intact and hidden for recovery.
        foreach(var name in new[]{"Nature • palms, grasses and shore rocks","Living reef","Sandy coast and seabed","Compact cove • planted gardens and rock wall","Coastal refresh • dock, cliffs and woodland"}){
            var g=GameObject.Find(name);if(g){Undo.RecordObject(g,"Archive previous layout");g.SetActive(false);}
        }
        var belongings=GameObject.Find("Washed-up belongings • E to recover");Undo.RecordObject(belongings.transform,"Move belongings beside spawn");
        var anchor=belongings.transform.Find("Stranded belongings rock");Vector3 destination=Land(-18.5f,11);belongings.transform.position+=destination-anchor.position;
        Undo.RecordObject(game.player,"Set beach spawn");game.player.position=Land(CoastalIslandLayout.Spawn.x,CoastalIslandLayout.Spawn.y);
        var cc=game.player.GetComponent<CharacterController>();Undo.RecordObject(cc,"Set ramp walking");cc.slopeLimit=48;cc.stepOffset=.35f;
        Undo.RecordObject(game.view.transform,"Frame new spawn");game.view.transform.position=game.player.position+new Vector3(0,12.4f,-10.2f);game.view.transform.rotation=Quaternion.LookRotation(new Vector3(0,-11.6f,9));
        Undo.RecordObject(game.view,"Frame new spawn");game.view.orthographicSize=10.8f;
        // Spawn marker is editor-only and has no gameplay renderer.
        var marker=new GameObject("Spawn - lower beach");marker.transform.SetParent(root);marker.transform.position=game.player.position;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Create reference map");Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        Physics.SyncTransforms();return "Built reference map: 90 terrain tiles, "+treeCount+" trees, "+shrubs+" bushes, 10 coral gardens, "+game.fish.Count+" swimming fish, connected ramp, relocated house and dock.";
    }

    public static string Refine(){
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play before refining.");
        root=GameObject.Find(RootName).transform;rng=new System.Random(251026);
        ground=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Ground.asset");rock=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Rock.asset");plants=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Plants.asset");
        var terrain=root.Find("01 - beach, rock terrace and surrounding hills");foreach(Transform t in terrain.Cast<Transform>().ToArray())Object.DestroyImmediate(t.gameObject);Terrain(terrain);
        int moved=0;
        foreach(var groupName in new[]{"02 - forest and planted hilltops","03 - bushes, grass and flowers"}){
            var group=root.Find(groupName);
            foreach(var t in group.Cast<Transform>().ToArray()){
                if(t.name.StartsWith("Grass and wildflowers")){Object.DestroyImmediate(t.gameObject);continue;}
                float x=t.position.x,z=t.position.z;
                if(t.name.StartsWith("Hilltop")&&(Slope(x,z)>.40f||(x>0&&CoastalIslandLayout.Hill(x,z)<.5f&&z-CoastalIslandLayout.Front(x)<6))){
                    bool found=false;for(int a=0;a<300;a++){
                        float nx=R(0,25),nz=R(19,31);if(!Clear(nx,nz,2.8f)||group.Cast<Transform>().Any(o=>o!=t&&Vector2.Distance(new Vector2(o.position.x,o.position.z),new Vector2(nx,nz))<2.1f))continue;x=nx;z=nz;found=true;break;
                    }if(!found){t.gameObject.SetActive(false);continue;}moved++;
                }
                GroundObject(t.gameObject,x,z);
            }
        }Flora(root.Find("03 - bushes, grass and flowers"));
        var wr=GameObject.Find("Uber Stylized Water • clear lagoon").GetComponent<Renderer>();
        var water=Save(new Material(wr.sharedMaterial),"ClearReefWater");water.SetColor("_Color_Shallow",new Color(.22f,.72f,.62f,.16f));water.SetColor("_Color_Deep",new Color(.035f,.37f,.37f,.48f));water.SetFloat("_Water_Depth",4.7f);wr.sharedMaterial=water;EditorUtility.SetDirty(water);
        var game=Object.FindFirstObjectByType<TidehavenGame>();foreach(var f in game.fish)if(f)f.localScale*=1.25f;
        var review=GameObject.Find("Map review camera");if(review)Object.DestroyImmediate(review);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game.gameObject.scene);return "Rounded hill contours, opened rocky second-floor apron, repositioned "+moved+" plants, clearer ocean and visible fish.";
    }
}
