using UnityEngine;
using UnityEngine.Rendering;
// Fixed 24-print ring: 96 vertices, 48 triangles, one draw. No terrain edits or runtime instantiation per step.
public sealed class IslandFootprints : MonoBehaviour {
 const int Count=24; Mesh mesh; Material material; GameObject surface; Vector3 last;
 readonly Vector3[] positions=new Vector3[Count*4]; readonly Vector2[] uv=new Vector2[Count*4],birth=new Vector2[Count*4];
 int next; bool left; public int TotalSteps {get;private set;}
 public void Initialize(Vector3 p){last=p;surface=new GameObject("Sand footprints • fixed 96 vertices");mesh=new Mesh{name="Pooled sand footprints"};mesh.MarkDynamic();var tris=new int[Count*6];
 for(int i=0;i<Count;i++){int q=i*4,t=i*6;uv[q]=new Vector2(0,0);uv[q+1]=new Vector2(0,1);uv[q+2]=new Vector2(1,1);uv[q+3]=new Vector2(1,0);for(int j=0;j<4;j++)birth[q+j]=new Vector2(-1000,0);tris[t]=q;tris[t+1]=q+1;tris[t+2]=q+2;tris[t+3]=q;tris[t+4]=q+2;tris[t+5]=q+3;}
 mesh.vertices=positions;mesh.uv=uv;mesh.uv2=birth;mesh.triangles=tris;mesh.bounds=new Bounds(Vector3.zero,new Vector3(70,12,70));surface.AddComponent<MeshFilter>().sharedMesh=mesh;var r=surface.AddComponent<MeshRenderer>();material=new Material(Shader.Find("Tidehaven/Pooled Sand Footprints"));r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
 public void Tick(Transform player){if(!mesh||!player)return;Vector3 d=player.position-last;d.y=0;if(d.sqrMagnitude<.42f*.42f)return;last=player.position;if(d.sqrMagnitude>4)return;
 if(LighthouseIslandShape.Edge(last.x,last.z)<.3f||CoastalIslandLayout.Grass(last.x,last.z)>.35f||CoastalWalkableSurface.TryDeck(last.x,last.z,out _))return;
 var forward=d.normalized;var right=Vector3.Cross(Vector3.up,forward);left=!left;var center=last+right*(left?-.11f:.11f)-forward*.08f;int q=next*4;
 for(int j=0;j<4;j++){var p=center+right*((uv[q+j].x-.5f)*.24f)+forward*((uv[q+j].y-.5f)*.42f);p.y=CoastalIslandLayout.Height(p.x,p.z)+.012f;positions[q+j]=p;birth[q+j]=new Vector2(Time.time,1);}
 mesh.vertices=positions;mesh.uv2=birth;next=(next+1)%Count;TotalSteps++;}
 void OnDestroy(){if(surface)Destroy(surface);if(mesh)Destroy(mesh);if(material)Destroy(material);}
}
