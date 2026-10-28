using UnityEngine;
using System.Collections.Generic;

// Plays the authored Blender clips on the original generic skeleton. Equipment IK runs afterwards.
[DefaultExecutionOrder(90)]
public class FishermanMotion : MonoBehaviour {
 public TidehavenGame game;
 public AnimationClip idle,walk,pickupRod,kneelBag,stow,draw,cast,reel;
 public string Action {get;private set;}
 public float ActionTime => string.IsNullOrEmpty(Action)?0:Time.time-actionStart;
 public float ActionDuration => actionClip?actionClip.length:0;
 public bool Acting => actionClip && ActionTime<actionClip.length;
 public float Speed {get;private set;}
 AnimationClip actionClip;float actionStart,walkTime,blend;Vector3 last;
 Transform[] thighs=new Transform[2],knees=new Transform[2],feet=new Transform[2];float[] standingFootY=new float[2];Transform[] bones;Vector3[] idlePosition;Quaternion[] idleRotation;
 void Awake(){if(!game)game=GetComponent<TidehavenGame>();bones=game.visual.GetComponentsInChildren<Transform>();idlePosition=new Vector3[bones.Length];idleRotation=new Quaternion[bones.Length];last=game.player.position;for(int i=0;i<2;i++){string side=i==0?"Left":"Right";foreach(var b in bones){if(b.name==side+"UpperLeg")thighs[i]=b;if(b.name==side+"LowerLeg")knees[i]=b;if(b.name==side+"Foot")feet[i]=b;}}}
 public void PlayAction(string name){Action=name;actionClip=name=="Cast"?cast:name=="PickupRod"?pickupRod:name=="KneelBag"?kneelBag:name=="Stow"?stow:draw;actionStart=Time.time;}
 public void ClearAction(){Action=null;actionClip=null;}
 public void Evaluate(float dt){
  Vector3 delta=game.player.position-last;delta.y=0;last=game.player.position;Speed=delta.magnitude/Mathf.Max(.0001f,dt);
  var intro=GetComponent<ShoreIntroduction>();if(intro&&intro.Playing)return;
  if(!idle||!walk)return;
  walkTime+=delta.magnitude/1.02f;blend=Mathf.MoveTowards(blend,Mathf.Clamp01(Speed/.5f),dt*6);
  if(blend<=0.001f){
   idle.SampleAnimation(game.visual.gameObject,Time.time%idle.length);
   for(int i=0;i<2;i++)if(feet[i])standingFootY[i]=feet[i].position.y;
  }else{
   idle.SampleAnimation(game.visual.gameObject,Time.time%idle.length);for(int i=0;i<2;i++)if(feet[i])standingFootY[i]=feet[i].position.y;
   for(int i=0;i<bones.Length;i++){idlePosition[i]=bones[i].localPosition;idleRotation[i]=bones[i].localRotation;}
   walk.SampleAnimation(game.visual.gameObject,(walkTime%1)*walk.length);
   for(int i=0;i<bones.Length;i++){bones[i].localPosition=Vector3.Lerp(idlePosition[i],bones[i].localPosition,blend);bones[i].localRotation=Quaternion.Slerp(idleRotation[i],bones[i].localRotation,blend);}
  }
  var celebration=GetComponent<CatchCelebration>();
  if(celebration&&celebration.Playing&&celebration.reveal){
   for(int i=0;i<bones.Length;i++){idlePosition[i]=bones[i].localPosition;idleRotation[i]=bones[i].localRotation;}
   celebration.reveal.SampleAnimation(game.visual.gameObject,celebration.Elapsed);
   for(int i=0;i<bones.Length;i++){bones[i].localPosition=Vector3.Lerp(idlePosition[i],bones[i].localPosition,celebration.PoseWeight);bones[i].localRotation=Quaternion.Slerp(idleRotation[i],bones[i].localRotation,celebration.PoseWeight);}return;
  }
  if(game.phase=="Reeling"&&reel)reel.SampleAnimation(game.visual.gameObject,Time.time%reel.length);
  if(actionClip&&Action=="Cast"&&!Acting)ClearAction();
  if(actionClip){
   for(int i=0;i<bones.Length;i++){idlePosition[i]=bones[i].localPosition;idleRotation[i]=bones[i].localRotation;}
   actionClip.SampleAnimation(game.visual.gameObject,Mathf.Min(ActionTime,actionClip.length));
   float w=Mathf.SmoothStep(0,1,Mathf.Clamp01(ActionTime/.14f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((actionClip.length-ActionTime)/.18f));
   for(int i=0;i<bones.Length;i++){bones[i].localPosition=Vector3.Lerp(idlePosition[i],bones[i].localPosition,w);bones[i].localRotation=Quaternion.Slerp(idleRotation[i],bones[i].localRotation,w);}
  }
 }
 void GroundFeet(){for(int i=0;i<2;i++){
  if(!feet[i]||!thighs[i]||!knees[i])continue;
  var foot=feet[i];var p=foot.position;var toe=p+game.player.forward*.13f;var heel=p-game.player.forward*.04f;
  float maxStep=game.player.position.y+.4f;
  float floor=Mathf.Max(CoastalWalkableSurface.Height(toe.x,toe.z,maxStep),CoastalWalkableSurface.Height(heel.x,heel.z,maxStep));
  float lift=Mathf.Clamp(p.y-standingFootY[i],0,.17f);
  p.y=floor+.155f+lift;
  var rotation=foot.rotation;
  var pole=knees[i].position;
  LimbContact.Solve(thighs[i],knees[i],foot,p,pole);
  foot.rotation=rotation;
 }}
 void LateUpdate(){Evaluate(Time.deltaTime);var intro=GetComponent<ShoreIntroduction>();if(!intro||!intro.Playing)GroundFeet();}
}
