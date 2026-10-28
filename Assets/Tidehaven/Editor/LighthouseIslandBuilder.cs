using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Collections.Generic;
using System.IO;
public static class LighthouseIslandBuilder {
 const string D="Assets/Tidehaven/LighthouseIsland";
 [System.Serializable]class Palette{public Entry[] items;}[System.Serializable]class Entry{public string name,texture;public float[] color;public float metal,rough;}
 static Bounds Bounds(GameObject g){var rs=g.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 static GameObject Clone(GameObject src,string name,Transform parent,Vector3 p,float height,float yaw){var g=Object.Instantiate(src);g.name=name;g.SetActive(true);g.transform.SetParent(parent);g.transform.position=Vector3.zero;g.transform.rotation=Quaternion.Euler(0,yaw,0);var b=Bounds(g);g.transform.localScale*=height/b.size.y;b=Bounds(g);g.transform.position=p-new Vector3(b.center.x,b.min.y,b.center.z);return g;}
 static Color C(string h){ColorUtility.TryParseHtmlString(h,out var c);return c;}
 public static string Build(){
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();var all=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 var palm=all.First(t=>t.name=="Palm").gameObject;var rock=all.First(t=>t.name=="Boulder").gameObject;var corals=all.Where(t=>t.name=="Coral"||t.name=="Reef").Take(9).Select(t=>t.gameObject).ToArray();var grass=all.FirstOrDefault(t=>t.name=="Seagrass")?.gameObject;
 var root=new GameObject("Lighthouse Island");var land=new GameObject("Irregular grassland and sandy beach");land.transform.parent=root.transform;
 int n=240;float span=100;var v=new Vector3[(n+1)*(n+1)];var col=new Color[v.Length];var uv=new Vector2[v.Length];var tr=new int[n*n*6];
 for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int i=z*(n+1)+x;float wx=(x/(float)n-.5f)*span,wz=(z/(float)n-.5f)*span;v[i]=new Vector3(wx,LighthouseIslandShape.Height(wx,wz),wz);uv[i]=new Vector2(wx,wz)*.1f;float noise=Mathf.PerlinNoise(wx*.4f+50,wz*.4f+50);var sand=Color.Lerp(C("#C8B58A"),C("#E9D5A5"),noise);var green=Color.Lerp(C("#477B3D"),C("#82A44F"),noise);col[i]=Color.Lerp(sand,green,LighthouseIslandShape.Grass(wx,wz));}
 int k=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*(n+1)+x;tr[k++]=i;tr[k++]=i+n+1;tr[k++]=i+1;tr[k++]=i+1;tr[k++]=i+n+1;tr[k++]=i+n+2;}
 var mesh=new Mesh{name="Island sand and meadow"};mesh.vertices=v;mesh.colors=col;mesh.uv=uv;mesh.triangles=tr;mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,D+"/IslandTerrain.asset");
 var mat=new Material(Shader.Find("Tidehaven/Stylized Vertex Lit"));mat.SetFloat("_OutlineWidth",0);mat.SetFloat("_ColorVibrance",1);AssetDatabase.CreateAsset(mat,D+"/Ground.mat");land.AddComponent<MeshFilter>().sharedMesh=mesh;land.AddComponent<MeshRenderer>().sharedMaterial=mat;land.AddComponent<MeshCollider>().sharedMesh=mesh;
 var mats=new Dictionary<string,Material>();foreach(var e in JsonUtility.FromJson<Palette>(File.ReadAllText(D+"/palette.json")).items){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=e.name;m.color=new Color(e.color[0],e.color[1],e.color[2],1).gamma;m.SetFloat("_Metallic",e.metal);m.SetFloat("_Smoothness",1-e.rough);if(!string.IsNullOrEmpty(e.texture)){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(e.texture);m.color=Color.white;}
 if(e.name.Contains("Glass")){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.color=new Color(.65f,.87f,.85f,.20f);}
 if(e.name.Contains("glow")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.62f,.19f)*2);}
 AssetDatabase.CreateAsset(m,D+"/"+e.name+".mat");mats[e.name]=m;}
 var tower=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(D+"/Lighthouse.fbx"));tower.name="Lighthouse and furnished guard quarters";tower.transform.parent=root.transform;
 foreach(var r in tower.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>m&&mats.ContainsKey(m.name)?mats[m.name]:m).ToArray();}
 var foundation=tower.GetComponentsInChildren<Transform>().First(t=>t.name=="SM_Stone_foundation").GetComponent<Renderer>().bounds;tower.transform.position+=new Vector3(-foundation.center.x,1.55f-foundation.min.y,-foundation.center.z);
 foreach(var f in tower.GetComponentsInChildren<MeshFilter>())if(f.name.Contains("Tower_band")||f.name.Contains("floor")||f.name.Contains("foundation")||f.name.Contains("Door_Open"))f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
 var palms=new GameObject("Palm groves").transform;palms.parent=root.transform;
 Random.InitState(2710);float[,] pp={{-11,-3},{-13,0},{-10,2},{11,3},{13,6},{9,8},{-6,11},{-3,12},{8,-7},{11,-5},{-12,-7}};
 for(int i=0;i<pp.GetLength(0);i++){float x=pp[i,0],z=pp[i,1];Clone(palm,"Island palm "+(i+1),palms,new Vector3(x,LighthouseIslandShape.Height(x,z),z),Random.Range(4.8f,6.8f),Random.Range(0,360));}
 var reef=new GameObject("Submerged coral gardens").transform;reef.parent=root.transform;
 for(int i=0;i<38;i++){float a=i*2.39996f;float rr=LighthouseIslandShape.Radius(a)+Random.Range(2.5f,6);float x=Mathf.Cos(a)*rr,z=Mathf.Sin(a)*rr*.84f;float y=LighthouseIslandShape.Height(x,z);float h=Mathf.Min(Random.Range(.45f,1.05f),-y-.15f);Clone(corals[i%corals.Length],"Underwater coral "+(i+1),reef,new Vector3(x,y,z),h,Random.Range(0,360));if(grass&&i%2==0)Clone(grass,"Swaying seagrass",reef,new Vector3(x+1,LighthouseIslandShape.Height(x+1,z),z),.6f,Random.Range(0,360));}
 var stones=new GameObject("Shore stones").transform;stones.parent=root.transform;for(int i=0;i<14;i++){float a=i*2.4f+.4f;Clone(rock,"Shore rock "+i,stones,LighthouseIslandShape.Beach(a,Random.Range(.5f,3)),Random.Range(.3f,.85f),Random.Range(0,360));}
 var wildlife=new GameObject("Animated seagulls and beach crabs").transform;wildlife.parent=root.transform;
 for(int i=0;i<5;i++){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/Seagull.prefab"));g.name="Circling seagull "+(i+1);g.transform.parent=wildlife;g.transform.position=new Vector3(Mathf.Cos(i*1.3f)*13,10+i*.4f,Mathf.Sin(i*1.3f)*11);g.transform.localScale=Vector3.one*.95f;var fly=g.AddComponent<LighthouseWildlife>();fly.flying=true;fly.phase=i*1.3f;g.GetComponent<WingMotion>().phaseOffset=i*1.7f;}
 for(int i=0;i<8;i++){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/WalkingCrab.prefab"));g.name="Beach crab "+(i+1);g.transform.parent=wildlife;g.transform.localScale=Vector3.one*.3f;g.transform.position=LighthouseIslandShape.Beach(i*.79f,2.2f);g.AddComponent<LighthouseWildlife>().phase=i*.79f;}
 var game=Object.FindFirstObjectByType<TidehavenGame>();var fishRoot=new GameObject("Reef fish").transform;fishRoot.parent=root.transform;var fish=game.fish.Where(t=>t).Take(18).ToArray();game.fish.Clear();for(int i=0;i<fish.Length;i++){var f=fish[i];f.SetParent(fishRoot,true);float a=i*.7f;float rr=LighthouseIslandShape.Radius(a)+4;f.position=new Vector3(Mathf.Cos(a)*rr,-.65f,Mathf.Sin(a)*rr*.84f);game.fish.Add(f);}
 var life=game.GetComponent<CoastalLife>();if(life)Object.DestroyImmediate(life);
 var player=game.player;var cc=player.GetComponent<CharacterController>();if(cc)cc.enabled=false;player.position=new Vector3(-3,LighthouseIslandShape.Height(-3,-12)+.04f,-12);if(cc)cc.enabled=true;
 var gear=game.GetComponent<StrandedGear>();if(gear){if(gear.bag)gear.bag.position=new Vector3(-4,LighthouseIslandShape.Height(-4,-12)+.15f,-12);if(gear.rod)gear.rod.position=new Vector3(-2,LighthouseIslandShape.Height(-2,-12)+.1f,-12);}
 string[] keep={"Main Camera","Warm morning sun","Coastal color grade","Fisherman • WASD to walk","Fishing • cast, bite and reel","Fishing line","Red fishing float","Washed-up belongings • E to recover","Uber Stylized Water • clear lagoon"};
 var removed=new List<string>();foreach(var r in roots)if(!keep.Contains(r.name)){removed.Add(r.name);Object.DestroyImmediate(r);}
 var water=GameObject.Find("Uber Stylized Water • clear lagoon");water.name="Ocean • clear turquoise shallows";water.transform.localScale=new Vector3(30,1,30);
 game.view.transform.position=new Vector3(33,34,-43);game.view.transform.LookAt(new Vector3(0,4,0));game.view.farClipPlane=400;game.view.fieldOfView=45;
 if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,4,0),Quaternion.Euler(38,-28,0),35);
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=root;
 return "Replaced map roots: "+string.Join(", ",removed)+". Created lighthouse, 11 palms, 38 coral colonies, 5 animated gulls, 8 animated crabs, 18 reef fish.";
 }
}
