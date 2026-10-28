using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.IO;
using System.Collections.Generic;
public static class CoastalLifeSetup {
 [System.Serializable]class Palette{public Entry[] items;} [System.Serializable]class Entry{public string name;public float r,g,b,a;}
 public static string Run(){var g=Object.FindFirstObjectByType<TidehavenGame>();var life=g.GetComponent<CoastalLife>();if(!life)life=g.gameObject.AddComponent<CoastalLife>();var mats=new Dictionary<string,Material>();
 string folder="Assets/Tidehaven/Materials/Wildlife";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Tidehaven/Materials","Wildlife");
 foreach(var e in JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/Tidehaven/Models/wildlife-palette.json")).items){string path=folder+"/"+e.name.Replace("/","_")+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=new Color(e.r,e.g,e.b,1).gamma;m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);}mats[e.name]=m;}
 foreach(var name in new[]{"WalkingCrab","Seagull"}){var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/"+name+".fbx"));PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);foreach(var r in obj.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>mats.TryGetValue(m.name,out var n)?n:m).ToArray();var rs=obj.GetComponentsInChildren<Renderer>();Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);var wrapper=new GameObject(name);obj.transform.SetParent(wrapper.transform);obj.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);if(name=="Seagull")wrapper.AddComponent<WingMotion>();var asset=PrefabUtility.SaveAsPrefabAsset(wrapper,"Assets/Tidehaven/Models/"+name+".prefab");if(name=="WalkingCrab")life.crabPrefab=asset;else life.gullPrefab=asset;Object.DestroyImmediate(wrapper);}
 var foam=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));foam.SetColor("_BaseColor",new Color(.9f,1,.95f,.55f));foam.SetFloat("_Surface",1);foam.SetFloat("_Blend",0);foam.SetFloat("_SrcBlend",5);foam.SetFloat("_DstBlend",10);foam.SetFloat("_ZWrite",0);foam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");foam.renderQueue=3000;AssetDatabase.CreateAsset(foam,folder+"/WaveFoam.mat");life.foamMaterial=foam;
 var water=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).First(r=>r.sharedMaterial&&r.sharedMaterial.shader.name=="UberStylizedWater").sharedMaterial;water.SetFloat("_ENABLEWAVE",1);water.EnableKeyword("_ENABLEWAVE");water.SetFloat("_1st_Wave_Height",.035f);water.SetFloat("_2nd_Wave_Height",.022f);water.SetFloat("_1st_Wave_Speed",.7f);water.SetFloat("_2nd_Wave_Speed",.9f);water.SetVector("_InterSec_Foam_Pan",new Vector4(.035f,.02f,0,0));EditorUtility.SetDirty(water);EditorUtility.SetDirty(life);EditorSceneManager.MarkSceneDirty(g.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();return "Wildlife, foliage breeze and shoreline waves installed";
 }
}

