using UnityEngine;
public static class LimbContact {
 public static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole,float weight=1){
  Vector3 a=upper.position;float l1=Vector3.Distance(a,lower.position),l2=Vector3.Distance(lower.position,end.position);Vector3 delta=target-a;float d=Mathf.Clamp(delta.magnitude,.02f,l1+l2-.001f);Vector3 dir=delta.normalized;Vector3 bend=Vector3.ProjectOnPlane(pole-a,dir).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(dir,Vector3.right).normalized;
  float x=(l1*l1-l2*l2+d*d)/(2*d);Vector3 joint=a+dir*x+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-x*x));
  upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-a,joint-a)*upper.rotation,weight);
  lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(end.position-lower.position,a+dir*d-lower.position)*lower.rotation,weight);
 }
 public static Vector3 Ground(Vector3 p,float clearance=0){p.y=CoastalWalkableSurface.Height(p.x,p.z)+clearance;return p;}
}
