using UnityEngine;
public static class LighthouseIslandShape {
 public static float Radius(float a)=>19.5f+2.1f*Mathf.Sin(3*a+.5f)+1.4f*Mathf.Cos(5*a-.7f)+.7f*Mathf.Sin(7*a);
 public static float Edge(float x,float z){float a=Mathf.Atan2(z/0.84f,x);return Radius(a)-new Vector2(x,z/0.84f).magnitude;}
 public static float Height(float x,float z){float d=Edge(x,z);float h=d<0?Mathf.Max(-5,d*.35f):1.65f*Mathf.SmoothStep(0,1,d/6);return h+.09f*Mathf.Sin(x*.36f)*Mathf.Sin(z*.29f)*Mathf.SmoothStep(0,1,d/9);}
 public static float Grass(float x,float z){float d=Edge(x,z);float border=4.7f+1.1f*Mathf.Sin(x*.37f+z*.21f)+.7f*Mathf.Sin(z*.64f-x*.2f);float cover=Mathf.SmoothStep(0,1,(d-border)/1.2f);float path=Mathf.Abs(x-(1.2f*Mathf.Sin(z*.33f)));if(z<0&&z>-18)cover*=Mathf.SmoothStep(0,1,(path-.65f)/.9f);return cover;}
 public static Vector3 Beach(float a,float inland=2){float r=Radius(a)-inland;float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r*.84f;return new Vector3(x,Height(x,z),z);}
}
public static class CoastalIslandLayout {
 public const float ScaleX=1,ScaleZ=1,TerraceHeight=1.65f,MinX=-25,MaxX=25,MinZ=-22,MaxZ=22;
 public static readonly Vector2 Spawn=new Vector2(-3,-12),House=Vector2.zero;
 public static readonly Vector2[] Ramp={Spawn,House};
 public static readonly Vector2[][] Trails={Ramp};
 public static float Shore(float x){for(float z=-25;z<25;z+=.1f)if(LighthouseIslandShape.Edge(x,z)>=0)return z;return 0;}
 public static float Front(float x)=>0;
 public static float Height(float x,float z)=>LighthouseIslandShape.Height(x,z);
 public static float Hill(float x,float z)=>0;
 public static float Grass(float x,float z)=>LighthouseIslandShape.Grass(x,z);
 public static float PathDistance(float x,float z)=>Mathf.Abs(x);
 public static float RampDistance(float x,float z,out float progress){progress=0;return 999;}
}
