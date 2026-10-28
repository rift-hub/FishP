using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Encodes fish tail swim weights into UV2 once so the StylizedVertexLit GPU vertex shader
/// animates the swimming motion with zero per-frame CPU overhead.
/// </summary>
public class FishTailMotion : MonoBehaviour
{
    void Start()
    {
        float maxX = 0f, maxZ = 0f;
        var filters = GetComponentsInChildren<MeshFilter>();
        foreach (var f in filters)
        {
            if (!f.sharedMesh) continue;
            var b = f.sharedMesh.bounds.extents;
            maxX = Mathf.Max(maxX, b.x);
            maxZ = Mathf.Max(maxZ, b.z);
        }

        bool axisX = maxX > maxZ;
        float length = Mathf.Max(maxX, maxZ, 0.01f);

        foreach (var f in filters)
        {
            var m = f.sharedMesh;
            if (!m || !m.isReadable) continue;
            var uv2 = new List<Vector4>();
            m.GetUVs(1, uv2);
            if (uv2.Count == m.vertexCount && uv2.Count > 0 && uv2[0].x > 2.5f) continue;

            var verts = m.vertices;
            var data = new Vector4[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float along = (axisX ? v.x : v.z) / length;
                float weight = Mathf.Pow(Mathf.Abs(along), 1.8f) * length;
                data[i] = new Vector4(3f, weight, along, axisX ? 1f : 0f);
            }
            m.SetUVs(1, data);
        }

        // GPU shader handles all swimming motion; disable per-frame CPU loop
        enabled = false;
    }
}
