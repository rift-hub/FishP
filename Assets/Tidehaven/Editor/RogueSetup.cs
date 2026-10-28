using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
public static class RogueSetup {
 [System.Serializable] class Palette {public Entry[] items;}
 [System.Serializable] class Entry {public string name;public float r,g,b,a;}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new System.Exception("Stop Play before replacing the character.");
  var game=Object.FindFirstObjectByType<TidehavenGame>();
  if(game.visual.name=="Fisherman Rogue")return "Already replaced";
  var old=game.visual;
  var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/FishermanRogue.fbx"));
  PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  model.name="Fisherman Rogue";var t=model.transform;t.SetParent(game.player,false);t.localRotation=old.localRotation;
  foreach(var a in model.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
  var bones=model.GetComponentsInChildren<Transform>();
  foreach(var name in new[]{"LeftUpperArm","RightUpperArm"}){
   var b=bones.First(x=>x.name==name);var prior=old.GetComponentsInChildren<Transform>().First(x=>x.name==name);b.localRotation=prior.localRotation;
  }
  var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
  t.localScale*=2.05f/bounds.size.y;bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
  t.position+=new Vector3(game.player.position.x-bounds.center.x,game.player.position.y+.02f-bounds.min.y,game.player.position.z-bounds.center.z);
  string folder="Assets/Tidehaven/Materials/Rogue";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Tidehaven/Materials","Rogue");
  var mats=new Dictionary<string,Material>();
  foreach(var e in JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Tidehaven/Models/rogue-palette.json")).items){
   var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=e.name;m.color=new Color(e.r,e.g,e.b,1).gamma;m.SetFloat("_Smoothness",.16f);
   string path=folder+"/"+e.name.Replace("/","_")+".mat";AssetDatabase.CreateAsset(m,path);mats[e.name]=m;
  }
  foreach(var r in rs){r.sharedMaterials=r.sharedMaterials.Select(m=>mats.TryGetValue(m.name,out var n)?n:m).ToArray();if(r is SkinnedMeshRenderer sk)sk.updateWhenOffscreen=true;}
  game.visual=t;Object.DestroyImmediate(old.gameObject);
  EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
  return "Rogue character installed, height 2.05m; source colors mapped to URP; relaxed arms; original gameplay and shore gear retained.";
 }
}
