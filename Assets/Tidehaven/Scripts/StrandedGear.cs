using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[DefaultExecutionOrder(100)]
public class StrandedGear : MonoBehaviour {
 public TidehavenGame game;public Transform bag,rod,rodTip;public bool hasBag,hasRod;public float pickupRadius=2.1f;
 public bool rodEquipped=true;public Transform carriedFloat,reelCrank;float reelAngle;Transform reelGrip;
 public bool Ready=>hasBag&&hasRod;
 public bool Busy=>pending!=null||!string.IsNullOrEmpty(transition);
 public Transform pending;
 public string Prompt {get{if(Busy)return pending==bag?"Recovering your bag...":pending==rod?"Picking up the rod...":rodEquipped?"Stowing the rod...":"Drawing the rod...";var next=Nearest();if(next)return next==bag?"E   Pick up bag":"E   Pick up fishing rod";if(!Ready)return hasBag?"Find your fishing rod beside the rock.":"Find your belongings beside the shore rock.";return rodEquipped?"Aim + E   Cast     R   Stow rod":"R   Draw rod to fish";}}
 FishermanMotion motion;Transform hips,chest,rArm,rElbow,rHand,lArm,lElbow,lHand;Transform[,] fingers=new Transform[5,3];
 Dictionary<Transform,Quaternion> fingerRest=new Dictionary<Transform,Quaternion>();
 Transform[] leftFingers;
 Quaternion[] leftGripRotations;
 static readonly Vector3 palmGrip=new Vector3(0,.060f,.025f);
 Vector3 pickupOrigin;Quaternion pickupRotation;string transition;bool transferred;float castStart=-100;string oldPhase="Explore";
 LineRenderer[] straps;Material strapMaterial;LineRenderer looseLine;Vector3 lineEnd,lineVelocity,lastTip;bool lineInitialized,wearsCloak;
 float RodLateralOffset=>reelCrank&&reelCrank.localPosition.x>0?-.11f:0f;
 Quaternion TorsoRotation=>Quaternion.LookRotation(Vector3.ProjectOnPlane(game.player.forward,chest.up).normalized,chest.up);
 Quaternion BagRotation=>TorsoRotation;
 Vector3 BagPosition=>chest.position+TorsoRotation*new Vector3(0,-.24f,-.255f);
 Vector3 BackPosition=>hasBag?bag.TransformPoint(new Vector3(.15f,.30f,-.15f)):chest.position+TorsoRotation*new Vector3(.18f,-.24f,-.33f);
 Quaternion BackRotation=>TorsoRotation*Quaternion.FromToRotation(Vector3.up,new Vector3(-.22f,1,0).normalized);
 void Awake(){if(!game)game=GetComponent<TidehavenGame>();motion=GetComponent<FishermanMotion>();var all=game.visual.GetComponentsInChildren<Transform>();
  foreach(var t in all){switch(t.name){case "Hips":hips=t;break;case "Chest":chest=t;break;case "RightUpperArm":rArm=t;break;case "RightLowerArm":rElbow=t;break;case "RightHand":rHand=t;break;case "LeftUpperArm":lArm=t;break;case "LeftLowerArm":lElbow=t;break;case "LeftHand":lHand=t;break;}
   var fn=new[]{"Index","Middle","Ring","Little","Thumb"};var sn=new[]{"Proximal","Intermediate","Distal"};for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(t.name=="Right"+fn[f]+sn[j]){fingers[f,j]=t;fingerRest[t]=t.localRotation;}
  }leftFingers=lHand?lHand.GetComponentsInChildren<Transform>():System.Array.Empty<Transform>();
  leftGripRotations=new Quaternion[leftFingers.Length];for(int i=0;i<leftFingers.Length;i++){string n=leftFingers[i].name;leftGripRotations[i]=Quaternion.AngleAxis(n.Contains("Thumb")?35:n.Contains("Proximal")?55:65,Vector3.right);}
  game.rodTip=rodTip;reelGrip=reelCrank?reelCrank.Find("Reel grip contact"):null;
  foreach(var renderer in game.visual.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var material in renderer.sharedMaterials)if(material&&(material.name.Contains("Cloth_Cloak")||material.name.Contains("Drawn_Straw_Layer")))wearsCloak=true;
 }
 Transform Nearest(){Transform best=null;float d=pickupRadius*pickupRadius;for(int i=0;i<2;i++){var t=i==0?(hasBag?null:bag):(hasRod?null:rod);if(!t)continue;var delta=t.position-game.player.position;delta.y=0;if(delta.sqrMagnitude<d){d=delta.sqrMagnitude;best=t;}}return best;}
 public bool TryPickup(){var intro=GetComponent<ShoreIntroduction>();if(Busy||(intro&&intro.Playing)||!motion)return false;var target=Nearest();if(!target)return false;
  var dir=target.position-game.player.position;dir.y=0;if(dir.sqrMagnitude>.01f)game.player.rotation=Quaternion.LookRotation(dir);
  pending=target;pickupOrigin=target.position;pickupRotation=target.rotation;transferred=false;motion.PlayAction(target==bag?"KneelBag":"PickupRod");return true;
 }
 public bool ToggleRod(){if(GetComponent<CatchCelebration>()?.Playing==true)return false;if(!hasRod||Busy||game.phase!="Explore"||!motion)return false;transition=rodEquipped?"Stow":"Draw";transferred=false;motion.PlayAction(transition);return true;}
 void Update(){var k=Keyboard.current;if(k!=null&&k.rKey.wasPressedThisFrame)ToggleRod();}
 Quaternion RodRotation(bool fishing){Vector3 d=fishing?new Vector3(.04f,.60f,.90f):new Vector3(0,.85f,.65f);float cast=game.CastElapsed;if(fishing&&cast<1.35f){float back=Mathf.SmoothStep(0,1,Mathf.Clamp01(cast/.45f));float forward=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.85f,cast));d=Vector3.Slerp(d,new Vector3(0,1,-.65f),back*(1-forward));}
  // Reel projects to the character's left: the right hand grips, the left turns the crank.
  return game.player.rotation*Quaternion.FromToRotation(Vector3.up,d.normalized);
 }
 Quaternion RodHandRotation(Quaternion rotation){var shaft=rotation*Vector3.up;var normal=Vector3.ProjectOnPlane(game.player.right,shaft).normalized;return Quaternion.LookRotation(normal,Vector3.Cross(normal,shaft).normalized);}
 void GripRod(Quaternion rotation,float weight=1){rHand.rotation=Quaternion.Slerp(rHand.rotation,RodHandRotation(rotation),weight);for(int f=0;f<5;f++)for(int j=0;j<3;j++){var b=fingers[f,j];if(b)b.localRotation=fingerRest[b]*Quaternion.AngleAxis((f==4?(j==0?35:j==1?45:25):(j==0?55:j==1?65:45))*weight,Vector3.right);}}
 void RightReach(Vector3 wrist,float weight=1){LimbContact.Solve(rArm,rElbow,rHand,wrist,rArm.position+game.player.right*.35f-game.player.forward*.18f-Vector3.up*.18f,weight);}
 void AttachRod(bool inHand){rod.SetParent(inHand?rHand:(hasBag?bag:chest),true);}
 static Vector3 StrapCurve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);}
 void UpdateStraps(){
  // Backpack straps sit beneath this character's mantle; the billboard loops pierced it.
  if(!hasBag||wearsCloak){if(straps!=null)foreach(var l in straps)l.enabled=false;return;}
  if(straps==null){
   strapMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));strapMaterial.color=new Color(.18f,.12f,.055f);
   straps=new LineRenderer[2];for(int i=0;i<2;i++){var go=new GameObject("Shoulder strap "+i);go.transform.SetParent(transform);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=strapMaterial;l.useWorldSpace=true;l.positionCount=40;l.loop=true;l.startWidth=l.endWidth=.032f;l.numCornerVertices=3;straps[i]=l;}
  }
  for(int i=0;i<2;i++){
   var l=straps[i];l.enabled=true;float x=(i==0?-1:1)*.145f;
   var points=new[]{new Vector3(x,.18f,-.23f),new Vector3(x,.28f,-.015f),new Vector3(x,.10f,.15f),new Vector3(x,-.22f,.12f),new Vector3(x,-.32f,-.23f)};
   for(int j=0;j<40;j++){float u=j/40f*points.Length;int k=(int)u;Vector3 p=StrapCurve(points[(k+4)%5],points[k],points[(k+1)%5],points[(k+2)%5],u-k);l.SetPosition(j,chest.position+TorsoRotation*p);}
  }
 }
 void OnDestroy(){if(straps!=null)foreach(var l in straps)if(l)Destroy(l.gameObject);if(strapMaterial)Destroy(strapMaterial);if(looseLine)Destroy(looseLine.gameObject);}

 void UpdateLooseLine(){
  if(!rodTip)return;
  if(!looseLine){var o=new GameObject("Loose fishing line");o.transform.SetParent(transform);looseLine=o.AddComponent<LineRenderer>();looseLine.sharedMaterial=game.fishingLine.sharedMaterial;looseLine.positionCount=20;looseLine.startWidth=.005f;looseLine.endWidth=.003f;looseLine.useWorldSpace=true;}
  looseLine.enabled=hasRod&&rodEquipped&&game.phase=="Explore"&&!(GetComponent<CatchCelebration>()?.Playing==true);if(carriedFloat)carriedFloat.gameObject.SetActive(looseLine.enabled);
  if(!looseLine.enabled){lineInitialized=false;return;}
  var tip=rodTip.position;var target=tip+Vector3.down*.62f;
  if(!lineInitialized||(tip-lastTip).sqrMagnitude>1){lineEnd=target;lineVelocity=Vector3.zero;lineInitialized=true;}
  float dt=Mathf.Min(Time.deltaTime,.033f);lineVelocity+=(target-lineEnd)*40*dt;lineVelocity*=Mathf.Exp(-5*dt);lineEnd+=lineVelocity*dt;
  var offset=lineEnd-tip;if(offset.magnitude>.72f)lineEnd=tip+offset.normalized*.72f;
  for(int i=0;i<20;i++){float t=i/19f;looseLine.SetPosition(i,Vector3.Lerp(tip,lineEnd,t)+Vector3.down*Mathf.Sin(t*Mathf.PI)*.06f);}lastTip=tip;if(carriedFloat){carriedFloat.position=lineEnd;carriedFloat.rotation=Quaternion.identity;}
 }
 void LateUpdate(){var intro=GetComponent<ShoreIntroduction>();if(intro&&intro.Playing)return;if(!motion||!rHand)return;
  bool fishing=game.phase!="Explore";if(oldPhase=="Explore"&&fishing)castStart=Time.time;oldPhase=game.phase;
  bool specialHand=false;
  if(pending){float t=motion.ActionTime;float u=t/Mathf.Max(.01f,motion.ActionDuration);bool pickingBag=pending==bag;
   // Move into reach during anticipation; the authored crouch supplies the body weight.
   if(u<.20f){var v=pickupOrigin-game.player.position;v.y=0;float extra=Mathf.Max(0,v.magnitude-.42f);if(extra>0){var step=v.normalized*Mathf.Min(extra,Time.deltaTime*3);step.y=TidehavenGame.Ground(game.player.position.x+step.x,game.player.position.z+step.z)-game.player.position.y;game.player.GetComponent<CharacterController>().Move(step);}}
   if(pickingBag){if(u>=.47f&&!transferred){hasBag=true;transferred=true;if(hasRod&&!rodEquipped)AttachRod(false);}}
   else{
    float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.17f,.36f,u));float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.44f,.83f,u));var rotation=Quaternion.Slerp(pickupRotation,RodRotation(false),lift);
    var grip=Vector3.Lerp(pickupOrigin,chest.position+game.player.right*RodLateralOffset+game.player.forward*.24f-Vector3.up*.20f,lift)+Vector3.up*(Mathf.Sin(lift*Mathf.PI)*.12f);
    RightReach(grip-RodHandRotation(rotation)*Vector3.Scale(palmGrip,rHand.lossyScale),reach);GripRod(rotation,reach);specialHand=true;
    if(u>=.42f){rod.SetPositionAndRotation(rHand.TransformPoint(palmGrip),rotation);if(!transferred){transferred=true;hasRod=true;rodEquipped=true;AttachRod(true);}}
   }
   if(!motion.Acting){pending=null;motion.ClearAction();if(Ready)game.EquipmentRecovered();}
  }
  if(!string.IsNullOrEmpty(transition)){
   float u=Mathf.Clamp01(motion.ActionTime/Mathf.Max(.01f,motion.ActionDuration));bool stowing=transition=="Stow";
   // Reach follows an arc around the right shoulder before the mount takes ownership.
   float travel=stowing?Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.62f)):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,1,u));
   var held=chest.position+game.player.right*RodLateralOffset+game.player.forward*.24f-Vector3.up*.20f;
   var grip=Vector3.Lerp(held,BackPosition,travel)+Vector3.up*(Mathf.Sin(travel*Mathf.PI)*.48f)+game.player.right*(Mathf.Sin(travel*Mathf.PI)*.12f);
   var rotation=Quaternion.Slerp(RodRotation(false),BackRotation,travel);float handWeight=stowing?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1,u)):Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.28f));
   RightReach(grip-RodHandRotation(rotation)*Vector3.Scale(palmGrip,rHand.lossyScale),handWeight);GripRod(rotation,handWeight);specialHand=true;
   bool carries=stowing?u<.62f:u>=.38f;
   if(carries)rod.SetPositionAndRotation(rHand.TransformPoint(palmGrip),rotation);else rod.SetPositionAndRotation(BackPosition,BackRotation);
   if(!transferred&&((stowing&&u>=.62f)||(!stowing&&u>=.38f))){rodEquipped=!stowing;AttachRod(rodEquipped);transferred=true;}
   if(!motion.Acting){transition=null;motion.ClearAction();}
  }
  if(hasBag&&bag)bag.SetPositionAndRotation(BagPosition,BagRotation);
  UpdateStraps();
  if(hasRod&&!specialHand&&!(GetComponent<CatchCelebration>()?.Playing==true)){
   if(rodEquipped){var rotation=RodRotation(fishing);var wrist=chest.position+game.player.right*RodLateralOffset+game.player.forward*(fishing?.08f:.14f)-Vector3.up*(fishing?.12f:.27f);if(fishing&&game.CastElapsed<1.35f)wrist+=Vector3.up*(Mathf.Sin(Mathf.Clamp01(game.CastElapsed/1.35f)*Mathf.PI)*.22f);RightReach(wrist);GripRod(rotation);rod.SetPositionAndRotation(rHand.TransformPoint(palmGrip),rotation);
        // Left palm holds the outer crank knob in both the carry and fishing poses.
    
    if(game.phase=="Reeling"&&game.ReelHeld)reelAngle+=Time.deltaTime*360;
    if(reelCrank)reelCrank.localRotation=Quaternion.AngleAxis(reelAngle,Vector3.right);
    Vector3 contact=reelGrip?reelGrip.position:reelCrank?reelCrank.TransformPoint(new Vector3(0,.0383f,-.0383f)):rod.TransformPoint(new Vector3(-.126f,.190f,-.045f));
    Vector3 normal=reelCrank&&rod.InverseTransformPoint(reelCrank.position).x>0?-rod.right:rod.right;
    Quaternion leftRotation=Quaternion.LookRotation(normal,rod.up);
    var wristTarget=contact-leftRotation*Vector3.Scale(palmGrip,lHand.lossyScale);
    LimbContact.Solve(lArm,lElbow,lHand,wristTarget,lArm.position-game.player.right*.24f-game.player.forward*.12f);
    lHand.rotation=leftRotation;
    for(int i=0;i<leftFingers.Length;i++){var finger=leftFingers[i];if(finger&&finger!=lHand)finger.localRotation=leftGripRotations[i];}
   }else rod.SetPositionAndRotation(BackPosition,BackRotation);
  }
  UpdateLooseLine();
 }
}











