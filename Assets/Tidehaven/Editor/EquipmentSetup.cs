using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
public static class EquipmentSetup {
 [System.Serializable]class Palette {public Entry[] items;}
 [System.Serializable]class Entry {public string name;public float r,g,b,a;}
 static Dictionary<string,Material> mats;
 static GameObject Import(string name,float height){var holder=new GameObject(name);var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/"+name+".fbx");var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(holder.transform,false);var renderers=holder.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);float scale=height/b.size.y;model.transform.localScale*=scale;b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);foreach(var r in renderers){r.sharedMaterials=r.sharedMaterials.Select(m=>mats.TryGetValue(m.name,out var n)?n:m).ToArray();}return holder;}
 public static string Run(){var game=Object.FindFirstObjectByType<TidehavenGame>();if(game.GetComponent<StrandedGear>())return "Equipment setup already exists";
 string scene=game.gameObject.scene.path;File.Copy(scene,"Assets/Tidehaven/Scenes/CoralCove-before-pickups.unity",true);
 mats=new Dictionary<string,Material>();foreach(var e in JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Tidehaven/Models/gear-palette.json")).items){string p="Assets/Tidehaven/Materials/"+e.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=new Color(e.r,e.g,e.b,1).gamma;m.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(m,p);}mats[e.name]=m;}
 var shore=new GameObject("Washed-up belongings • E to recover");var rock=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/Boulder.fbx"));rock.name="Stranded belongings rock";rock.transform.SetParent(shore.transform,false);rock.transform.localScale*=.65f;rock.transform.position=new Vector3(-2.6f,TidehavenGame.Ground(-2.6f,4.1f),4.1f);foreach(var r in rock.GetComponentsInChildren<Renderer>()){r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Materials/Fallback.mat");}var bounds=rock.GetComponentInChildren<Renderer>().bounds;var col=rock.AddComponent<BoxCollider>();col.center=rock.transform.InverseTransformPoint(bounds.center);col.size=rock.transform.InverseTransformVector(bounds.size)*.85f;
 var bag=Import("StrawBag",.72f);bag.transform.SetParent(shore.transform,true);bag.transform.position=new Vector3(-2.9f,bounds.max.y-.08f,4.05f);bag.transform.rotation=Quaternion.Euler(0,-25,12);
 var rod=Import("BambooRod",2.5f);rod.transform.SetParent(shore.transform,true);rod.transform.position=new Vector3(-1.65f,TidehavenGame.Ground(-1.65f,3.9f)+.04f,3.9f);rod.transform.rotation=Quaternion.Euler(-16,0,28);
 var tip=new GameObject("Bamboo rod tip").transform;tip.SetParent(rod.transform,false);tip.localPosition=Vector3.up*2.5f;
 var old=game.player.Find("Fishing rod");if(old)Object.DestroyImmediate(old.gameObject);old=game.player.Find("Rod tip");if(old)Object.DestroyImmediate(old.gameObject);
 var gear=game.gameObject.AddComponent<StrandedGear>();gear.game=game;gear.bag=bag.transform;gear.rod=rod.transform;gear.rodTip=tip;game.rodTip=tip;
 game.view.fieldOfView=38;game.view.transform.position=game.player.position+Vector3.up*.6f+Vector3.back*2.3f+new Vector3(0,17,-11.9f);game.view.transform.rotation=Quaternion.LookRotation(new Vector3(0,-17,11.9f));
 game.fishingLine.startWidth=.018f;game.fishingLine.endWidth=.012f;
 EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();return "Closer camera; separate bag and rod staged at shore rock; equipment gating enabled.";}
}
