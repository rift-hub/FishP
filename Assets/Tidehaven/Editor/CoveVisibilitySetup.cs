using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Editor-only spatial partitioning; Unity performs per-camera culling at runtime.</summary>
public static class CoveVisibilitySetup
{
    const string Folder = "Assets/Tidehaven/VisibilityChunks";
    public const string RootName = "Camera-cullable sections";
    static readonly string[] MeshNames = { "MeadowCoastTerrain", "LowMeadowFlowers", "TimberDock" };

    sealed class Section
    {
        public readonly Dictionary<int,int> remap = new Dictionary<int,int>();
        public readonly List<int> originalIndices = new List<int>();
        public readonly List<int>[] triangles;
        public Section(int count) { triangles = Enumerable.Range(0,count).Select(_=>new List<int>()).ToArray(); }
        public int Index(int original)
        {
            if (remap.TryGetValue(original,out int index)) return index;
            index=originalIndices.Count;remap.Add(original,index);originalIndices.Add(original);return index;
        }
    }

    static T[] Subset<T>(T[] data,List<int> indices,int count)
    {
        return data.Length==count ? indices.Select(i=>data[i]).ToArray() : Array.Empty<T>();
    }

    static object Partition(MeshFilter source,float cellSize)
    {
        var old=source.transform.Find(RootName);
        if(old)Object.DestroyImmediate(old.gameObject);
        var mesh=source.sharedMesh;
        if(!mesh.isReadable)throw new InvalidOperationException("Mesh must be readable: "+mesh.name);
        var originalRenderer=source.GetComponent<MeshRenderer>();
        var vertices=mesh.vertices;
        var sections=new Dictionary<Vector2Int,Section>();
        int sourceTriangles=0;
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            if(mesh.GetTopology(sub)!=MeshTopology.Triangles)throw new InvalidOperationException("Expected triangle mesh");
            var indices=mesh.GetTriangles(sub);
            sourceTriangles+=indices.Length/3;
            for(int i=0;i<indices.Length;i+=3)
            {
                var center=source.transform.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3f);
                var key=new Vector2Int(Mathf.FloorToInt(center.x/cellSize),Mathf.FloorToInt(center.z/cellSize));
                if(!sections.TryGetValue(key,out var section)){section=new Section(mesh.subMeshCount);sections.Add(key,section);}
                for(int j=0;j<3;j++)section.triangles[sub].Add(section.Index(indices[i+j]));
            }
        }
        var normals=mesh.normals;var tangents=mesh.tangents;var colors=mesh.colors;
        var uvs=new List<Vector4>[8];
        for(int i=0;i<8;i++){uvs[i]=new List<Vector4>();mesh.GetUVs(i,uvs[i]);}
        var root=new GameObject(RootName);root.transform.SetParent(source.transform,false);
        root.layer=source.gameObject.layer;
        int generatedTriangles=0;
        foreach(var pair in sections)
        {
            var section=pair.Value;var ids=section.originalIndices;
            string name=mesh.name+"_"+pair.Key.x+"_"+pair.Key.y;
            var part=new Mesh{name=name,indexFormat=ids.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            part.vertices=Subset(vertices,ids,vertices.Length);
            if(normals.Length==vertices.Length)part.normals=Subset(normals,ids,vertices.Length);
            if(tangents.Length==vertices.Length)part.tangents=Subset(tangents,ids,vertices.Length);
            if(colors.Length==vertices.Length)part.colors=Subset(colors,ids,vertices.Length);
            for(int channel=0;channel<8;channel++)
                if(uvs[channel].Count==vertices.Length)part.SetUVs(channel,ids.Select(i=>uvs[channel][i]).ToList());
            part.subMeshCount=mesh.subMeshCount;
            for(int sub=0;sub<mesh.subMeshCount;sub++){part.SetTriangles(section.triangles[sub],sub);generatedTriangles+=section.triangles[sub].Count/3;}
            // Keep authored normals at borders to avoid seams. Margin covers shader foliage motion.
            part.RecalculateBounds();var bounds=part.bounds;bounds.Expand(.16f);part.bounds=bounds;
            string path=Folder+"/"+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(asset){EditorUtility.CopySerialized(part,asset);Object.DestroyImmediate(part);part=asset;}
            else AssetDatabase.CreateAsset(part,path);
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.layer=source.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh=part;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=originalRenderer.sharedMaterials;
            renderer.shadowCastingMode=originalRenderer.shadowCastingMode;renderer.receiveShadows=originalRenderer.receiveShadows;
            renderer.lightProbeUsage=originalRenderer.lightProbeUsage;renderer.reflectionProbeUsage=originalRenderer.reflectionProbeUsage;
            renderer.renderingLayerMask=originalRenderer.renderingLayerMask;renderer.motionVectorGenerationMode=originalRenderer.motionVectorGenerationMode;
            renderer.lightmapIndex=originalRenderer.lightmapIndex;renderer.lightmapScaleOffset=originalRenderer.lightmapScaleOffset;
            renderer.sortingLayerID=originalRenderer.sortingLayerID;renderer.sortingOrder=originalRenderer.sortingOrder;
            var block=new MaterialPropertyBlock();originalRenderer.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
        }
        if(generatedTriangles!=sourceTriangles)throw new InvalidOperationException("Geometry count changed for "+mesh.name);
        // Only the drawing component changes. The original mesh and all colliders remain intact.
        originalRenderer.enabled=false;EditorUtility.SetDirty(originalRenderer);
        return new {mesh=mesh.name,sections=sections.Count,triangles=generatedTriangles,cellSize};
    }

    [MenuItem("Tidehaven/Optimize camera visibility")]
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before partitioning scenery.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(!scene.path.EndsWith("/CoralCove.unity"))throw new InvalidOperationException("Open CoralCove.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Tidehaven","VisibilityChunks");
        var sources=Object.FindObjectsByType<MeshFilter>().Where(f=>f.sharedMesh&&MeshNames.Contains(f.sharedMesh.name)).ToArray();
        var result=new List<object>();
        foreach(var source in sources)result.Add(Partition(source,source.sharedMesh.name=="MeadowCoastTerrain"?10f:source.sharedMesh.name=="TimberDock"?4f:6f));
        var camera=Object.FindAnyObjectByType<TidehavenGame>().view;
        camera.useOcclusionCulling=true;EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        return result;
    }

    public static object Report()
    {
        var game=Object.FindAnyObjectByType<TidehavenGame>();var cam=game.view;
        var oldPosition=cam.transform.position;var oldRotation=cam.transform.rotation;float oldSize=cam.orthographicSize;
        var sources=Object.FindObjectsByType<MeshFilter>().Where(f=>f.sharedMesh&&MeshNames.Contains(f.sharedMesh.name)).ToArray();
        var samples=new List<object>();
        try
        {
            foreach(float zoom in new[]{1f,2.2f})foreach(float x in new[]{-20f,0f,20f})
            {
                cam.orthographicSize=9.5f*zoom;cam.transform.position=new Vector3(x,1.8f,4f)+new Vector3(0,11.6f,-9)*zoom;
                cam.transform.rotation=Quaternion.LookRotation(new Vector3(0,-11.6f,9));
                var planes=GeometryUtility.CalculateFrustumPlanes(cam);
                int original=0,sectionTriangles=0,total=0,visible=0;
                foreach(var source in sources)
                {
                    if(GeometryUtility.TestPlanesAABB(planes,source.GetComponent<Renderer>().bounds))original+=source.sharedMesh.triangles.Length/3;
                    var root=source.transform.Find(RootName);if(!root)continue;
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                    {
                        total++;if(!GeometryUtility.TestPlanesAABB(planes,filter.GetComponent<Renderer>().bounds))continue;
                        visible++;sectionTriangles+=filter.sharedMesh.triangles.Length/3;
                    }
                }
                samples.Add(new{x,zoom,totalSections=total,visibleSections=visible,offscreenSections=total-visible,originalSubmittedTriangles=original,sectionSubmittedTriangles=sectionTriangles});
            }
        }
        finally{cam.transform.SetPositionAndRotation(oldPosition,oldRotation);cam.orthographicSize=oldSize;}
        return samples;
    }
}
