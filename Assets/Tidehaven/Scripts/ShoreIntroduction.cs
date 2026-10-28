using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
[DefaultExecutionOrder(200)]
public class ShoreIntroduction : MonoBehaviour {
 public TidehavenGame game;public float duration=8;
 public bool Playing{get;private set;} public float Elapsed=>Time.time-start;
 float start;Transform hips,chest,head,la,le,lh,ra,re,rh,lt,lk,lf,rt,rk,rf;Vector3 hipRest;Quaternion hipRotation;Transform[] poseBones;Quaternion[] restRotations;
 Transform Bone(string name)=>game.visual.GetComponentsInChildren<Transform>().First(t=>t.name==name);
 void Start(){game=GetComponent<TidehavenGame>();hips=Bone("Hips");chest=Bone("Chest");head=Bone("Head");la=Bone("LeftUpperArm");le=Bone("LeftLowerArm");lh=Bone("LeftHand");ra=Bone("RightUpperArm");re=Bone("RightLowerArm");rh=Bone("RightHand");lt=Bone("LeftUpperLeg");lk=Bone("LeftLowerLeg");lf=Bone("LeftFoot");rt=Bone("RightUpperLeg");rk=Bone("RightLowerLeg");rf=Bone("RightFoot");hipRest=hips.localPosition;hipRotation=hips.localRotation;poseBones=game.visual.GetComponentsInChildren<Transform>();restRotations=poseBones.Select(b=>b.localRotation).ToArray();start=Time.time;Playing=true;game.enabled=false;}
 void LateUpdate(){if(!Playing)return;for(int i=0;i<poseBones.Length;i++)poseBones[i].localRotation=restRotations[i];float t=Elapsed;bool skip=Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame;if(skip)t=duration;
 var p=game.player;float sit=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,3.2f,t));float stand=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.2f,6.3f,t));
 hips.localPosition=hipRest;hips.localRotation=hipRotation;Vector3 upright=hips.position;Vector3 seated=LimbContact.Ground(p.position,.18f);hips.position=Vector3.Lerp(seated,upright,stand);hips.rotation=Quaternion.AngleAxis(-84*(1-sit),p.right)*hips.rotation;
 Vector3 left=LimbContact.Ground(p.position-p.right*.13f+p.forward*Mathf.Lerp(.63f,.02f,stand),.16f);Vector3 right=LimbContact.Ground(p.position+p.right*.15f+p.forward*Mathf.Lerp(.55f,.02f,stand),.16f);
 var lrot=lf.rotation;var rrot=rf.rotation;LimbContact.Solve(lt,lk,lf,left,left+p.forward+Vector3.up);LimbContact.Solve(rt,rk,rf,right,right+p.forward+Vector3.up);lf.rotation=lrot;rf.rotation=rrot;
 float support=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,2,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,5.8f,t)));
 Vector3 lplant=LimbContact.Ground(p.position-p.right*.38f-p.forward*.12f,.06f),rplant=LimbContact.Ground(p.position+p.right*.38f-p.forward*.12f,.06f);
 lplant=Vector3.Lerp(lplant,lk.position+Vector3.up*.025f,sit);rplant=Vector3.Lerp(rplant,rk.position+Vector3.up*.025f,sit);
 LimbContact.Solve(la,le,lh,lplant,lplant-p.right+Vector3.up,support);LimbContact.Solve(ra,re,rh,rplant,rplant+p.right+Vector3.up,support);
 head.rotation=Quaternion.AngleAxis(Mathf.Sin(t*1.5f)*10*sit*(1-stand),p.up)*head.rotation;
 float pull=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.7f,8,t));Vector3 target=Vector3.Lerp((hips.position+head.position)*.5f,p.position+Vector3.up*.8f+Vector3.back*1.5f,pull);Vector3 close=p.position+p.right*2.5f+p.forward*3+Vector3.up*2.3f;Vector3 far=p.position+Vector3.up*.8f+Vector3.back*1.5f+new Vector3(0,11.6f,-9.0f);game.view.transform.position=Vector3.Lerp(close,far,pull);game.view.transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(target-close),Quaternion.LookRotation(new Vector3(0,-11.6f,9.0f)),pull);
 if(t>=duration){hips.localPosition=hipRest;hips.localRotation=hipRotation;Playing=false;game.enabled=true;}
 }
}
