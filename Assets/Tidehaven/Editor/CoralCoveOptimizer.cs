using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Linq;

public static class CoralCoveOptimizer
{
    const string MeshFolder = "Assets/Tidehaven/Meshes";
    const string MatFolder = "Assets/Tidehaven/Materials";

    static Color Hex(string s)
    {
        ColorUtility.TryParseHtmlString(s, out Color c);
        return c;
    }

    static Color EnhancePaletteColor(string matName, Color original)
    {
        string n = matName.ToLowerInvariant();
        if (n.Contains("rose_coral")) return Hex("#EC7B96");
        if (n.Contains("gold_coral")) return Hex("#E6CC5A");
        if (n.Contains("purple_sponge")) return Hex("#7B5CE0");
        if (n.Contains("sponge_lip")) return Hex("#A992F2");
        if (n.Contains("sponge_interior")) return Hex("#3D297A");
        if (n.Contains("lavender_plates")) return Hex("#C89EEB");
        if (n.Contains("lime_coral")) return Hex("#9FD45A");
        if (n.Contains("seaweed_0") || n.Contains("seagrass")) return Hex("#68B536");
        if (n.Contains("seaweed_1")) return Hex("#84CC45");
        if (n.Contains("seaweed_2")) return Hex("#4E9E48");

        if (n.Contains("teal_stone_0")) return Hex("#8AA8A1");
        if (n.Contains("teal_stone_1")) return Hex("#98B5AE");
        if (n.Contains("teal_stone_2")) return Hex("#A5C0B9");
        if (n.Contains("teal_stone_3")) return Hex("#7F9D96");
        if (n.Contains("rock_cracks") || n.Contains("fallback")) return Hex("#6B8781");

        if (n.Contains("frond sunlit")) return Hex("#88BD3E");
        if (n.Contains("frond ridge")) return Hex("#96C94A");
        if (n.Contains("frond olive")) return Hex("#6DA332");
        if (n.Contains("frond shadow")) return Hex("#538525");
        if (n.Contains("warm bark")) return Hex("#96744E");
        if (n.Contains("bark ridge")) return Hex("#A6835C");
        if (n.Contains("bark groove")) return Hex("#7D5E3D");

        if (n.Contains("leaflet") || n.Contains("lf_stk")) return Hex("#4A852C");
        return original;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(0.0001f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    static float TrailDistance(Vector2 p)
    {
        float d1 = DistToSegment(p, new Vector2(-0.8f, 3.0f), new Vector2(-0.8f, 5.8f));
        float dW1 = DistToSegment(p, new Vector2(-0.8f, 5.8f), new Vector2(-7.5f, 6.0f));
        float dW2 = DistToSegment(p, new Vector2(-7.5f, 6.0f), new Vector2(-14.8f, 6.4f));
        float dE1 = DistToSegment(p, new Vector2(1.8f, 4.8f), new Vector2(7.5f, 5.4f));
        float dE2 = DistToSegment(p, new Vector2(7.5f, 5.4f), new Vector2(12.5f, 3.8f));
        float dE3 = DistToSegment(p, new Vector2(7.5f, 5.4f), new Vector2(17.8f, 6.2f));
        float dStream = DistToSegment(p, new Vector2(0.6f, 8.2f), new Vector2(0.6f, 1.5f));
        return Mathf.Min(Mathf.Min(d1, Mathf.Min(dW1, dW2)), Mathf.Min(dStream, Mathf.Min(dE1, Mathf.Min(dE2, dE3))));
    }

    static bool IsClearOfLandmarksAndPaths(Vector2 p, float extraMargin = 0f)
    {
        // Keep trail paths & waterfall stream clear
        if (TrailDistance(p) < 1.35f + extraMargin) return false;
        // Keep Abandoned House at (-14.8, 7.2) clear
        if (Vector2.Distance(p, new Vector2(-14.8f, 7.2f)) < 4.5f + extraMargin) return false;
        // Keep Fisherman's Shack at (18.0, 7.0) clear
        if (Vector2.Distance(p, new Vector2(18.0f, 7.0f)) < 3.4f + extraMargin) return false;
        // Keep Harbor Pier at (12.5, -2.5..4.5) clear
        if (DistToSegment(p, new Vector2(12.5f, -7.5f), new Vector2(12.5f, 4.8f)) < 3.6f + extraMargin) return false;
        // Keep Mountain Waterfall at (0.6, 7.8) clear
        if (Vector2.Distance(p, new Vector2(0.6f, 7.8f)) < 2.8f + extraMargin) return false;
        // Keep Washed-up Belongings Rock at (-2.6, 4.1) clear
        if (Vector2.Distance(p, new Vector2(-2.6f, 4.1f)) < 2.0f + extraMargin) return false;
        return true;
    }

    static Mesh PrepareKitMesh(Mesh src, string saveName, int windMode)
    {
        string assetPath = $"{MeshFolder}/Kit_{saveName}.asset";
        var opt = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        bool isNew = false;
        if (!opt) { opt = new Mesh(); isNew = true; }
        else opt.Clear();

        var rawVerts = src.vertices;
        var rawNorms = src.normals;
        const float scaleFix = 1f;
        Quaternion rotFix = Quaternion.Euler(-90f, 0f, 0f);

        var vs = new Vector3[rawVerts.Length];
        var ns = new Vector3[rawVerts.Length];
        var uv2 = new Vector4[rawVerts.Length];
        for (int i = 0; i < rawVerts.Length; i++)
        {
            vs[i] = rotFix * (rawVerts[i] * scaleFix);
            ns[i] = (rawNorms != null && i < rawNorms.Length) ? (rotFix * rawNorms[i]).normalized : Vector3.up;
            uv2[i] = new Vector4(windMode, 1f, 0f, 0f);
        }

        opt.name = "Kit_" + saveName;
        opt.vertices = vs;
        opt.normals = ns;
        opt.colors = src.colors;
        opt.SetUVs(1, uv2);
        opt.subMeshCount = 1;
        opt.SetTriangles(src.triangles, 0, true);
        opt.RecalculateBounds();

        if (isNew) AssetDatabase.CreateAsset(opt, assetPath);
        else EditorUtility.SetDirty(opt);
        return opt;
    }

    static Mesh BuildMeadowPatchMesh(string name, float cx, float cz, float rx, float rz, int seed)
    {
        Random.InitState(seed);
        int rings = 18, n = 96;
        var vs = new List<Vector3>();
        var cs = new List<Color>();
        var ts = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            for (int j = 0; j <= n; j++)
            {
                float a = j / (float)n * Mathf.PI * 2f;
                float t = r / (float)rings;
                float wobble = 1f + 0.08f * Mathf.Sin(a * 5f + seed) + 0.06f * Mathf.Cos(a * 8f - seed);
                float x = cx + Mathf.Cos(a) * rx * t * wobble;
                float z = Mathf.Max(TidehavenGame.Shore(x) + 1.95f, cz + Mathf.Sin(a) * rz * t * wobble);

                float y = TidehavenGame.Ground(x, z) + 0.028f;
                vs.Add(new Vector3(x, y, z));

                Color grassCol = Color.Lerp(Hex("#3D732E"), Hex("#74A43C"), Mathf.PerlinNoise(x * 0.35f + 10f, z * 0.35f + 10f));
                Color finalCol = Color.Lerp(grassCol, Hex("#8EA64C"), Mathf.Pow(t, 7f) * 0.45f);
                // Store normalized radial distance t in alpha so CoastalGround.shader draws the grassy terrace border lip!
                finalCol.a = t;
                cs.Add(finalCol);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int j = 0; j < n; j++)
            {
                int i = r * (n + 1) + j;
                ts.AddRange(new[] { i, i + 1, i + n + 1, i + 1, i + n + 2, i + n + 1 });
            }
        }

        string path = $"{MeshFolder}/Island_{name.Replace(" ", "_")}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = false;
        if (!mesh) { mesh = new Mesh(); isNew = true; }
        else mesh.Clear();

        mesh.name = "Island_" + name.Replace(" ", "_");
        mesh.SetVertices(vs);
        mesh.SetColors(cs);
        mesh.SetTriangles(ts, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    public static string RebuildMapAndNature()
    {
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;

        AssetDatabase.ImportAsset("Assets/Tidehaven/StylizedVertexLit.shader", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Tidehaven/CoastalGround.shader", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Tidehaven/Sunbeam.shader", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Tidehaven/Models/CoastalNatureKit.fbx", ImportAssetOptions.ForceUpdate);

        var matOutlined = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/StylizedPropOutlined.mat");
        var matNoOutline = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/StylizedPropNoOutline.mat");

        // 1. Restore 100%-intact SM_BerryBush mesh with World-Space wind mode = 1
        var berryFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tidehaven/Models/BerryBush.fbx");
        Mesh intactBerryMesh = null;
        if (berryFbx)
        {
            var srcMf = berryFbx.GetComponentInChildren<MeshFilter>();
            var srcMr = berryFbx.GetComponentInChildren<MeshRenderer>();
            if (srcMf && srcMf.sharedMesh)
            {
                var src = srcMf.sharedMesh;
                var vs = src.vertices;
                var cols = new Color[vs.Length];
                var tris = new List<int>(src.triangles.Length);
                var uv2 = new Vector4[vs.Length];
                for (int i = 0; i < vs.Length; i++) uv2[i] = new Vector4(1f, 1f, 0f, 0f);

                for (int s = 0; s < src.subMeshCount; s++)
                {
                    string mName = (srcMr && s < srcMr.sharedMaterials.Length && srcMr.sharedMaterials[s]) ? srcMr.sharedMaterials[s].name : "";
                    var mAsset = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/{mName.Replace('/', '_')}.mat");
                    Color c = mAsset ? EnhancePaletteColor(mName, mAsset.color) : Hex("#4A852C");
                    foreach (int idx in src.GetTriangles(s))
                    {
                        cols[idx] = c;
                        tris.Add(idx);
                    }
                }

                foreach (string guid in AssetDatabase.FindAssets("Opt_SM_BerryBush t:Mesh", new[] { MeshFolder }))
                {
                    var opt = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!opt) continue;
                    opt.Clear();
                    opt.vertices = vs;
                    opt.normals = src.normals;
                    opt.colors = cols;
                    opt.SetUVs(1, uv2);
                    opt.subMeshCount = 1;
                    opt.SetTriangles(tris, 0, true);
                    opt.RecalculateBounds();
                    EditorUtility.SetDirty(opt);
                    intactBerryMesh = opt;
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("Opt_SM_Palm t:Mesh", new[] { MeshFolder }))
        {
            var opt = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
            if (!opt) continue;
            var uv2 = new Vector4[opt.vertexCount];
            for (int i = 0; i < uv2.Length; i++) uv2[i] = new Vector4(1f, 1f, 0f, 0f);
            opt.SetUVs(1, uv2);
            EditorUtility.SetDirty(opt);
        }

        // 2. Load the 13 custom Blender meshes from CoastalNatureKit.fbx
        var kitMeshes = new Dictionary<string, Mesh>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/Tidehaven/Models/CoastalNatureKit.fbx").OfType<Mesh>())
        {
            int wind = (obj.name.StartsWith("Tree_") || obj.name.StartsWith("Shrub_") || obj.name.StartsWith("Grass_") || obj.name.StartsWith("Wildflower_"))
                ? 1
                : (obj.name.StartsWith("Fish_") ? 3 : 0);
            kitMeshes[obj.name] = PrepareKitMesh(obj, obj.name, wind);
        }

        // 3. Reposition Harbor buildings (Abandoned House, Fisherman's Shack, Harbor Pier) so NOTHING overlaps!
        var harborRoot = GameObject.Find("Harbor")?.transform;
        if (harborRoot)
        {
            var house = harborRoot.Find("House");
            if (house)
            {
                float hx = -14.8f, hz = 7.2f;
                house.position = new Vector3(hx, TidehavenGame.Ground(hx, hz) - 0.05f, hz);
                house.rotation = Quaternion.Euler(0f, 162f, 0f);
            }
            var shop = harborRoot.Find("Shop");
            if (shop)
            {
                float sx = 18.0f, sz = 7.0f;
                shop.position = new Vector3(sx, TidehavenGame.Ground(sx, sz) - 0.05f, sz);
                shop.rotation = Quaternion.Euler(0f, 198f, 0f);
            }
            var dock = harborRoot.Find("Dock");
            if (dock)
            {
                // Place Harbor Pier cleanly extending from the eastern beach (z = 2.8) out into the lagoon (z = -7.8)
                dock.position = new Vector3(12.5f, -1.18f, -2.5f);
                dock.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        // 4. Expand the lush coastal grass terraces across the FULL island width (-26 to +26)!
        var natureRoot = GameObject.Find("Nature • palms, grasses and shore rocks")?.transform;
        if (natureRoot)
        {
            var westMeadow = natureRoot.Find("Western palm grove")?.GetComponent<MeshFilter>();
            if (westMeadow) { westMeadow.transform.position = Vector3.zero; westMeadow.sharedMesh = BuildMeadowPatchMesh("Western palm grove", -14.0f, 8.2f, 12.0f, 4.8f, 12); }

            var eastMeadow = natureRoot.Find("Eastern berry grove")?.GetComponent<MeshFilter>();
            if (eastMeadow) { eastMeadow.transform.position = Vector3.zero; eastMeadow.sharedMesh = BuildMeadowPatchMesh("Eastern berry grove", 14.0f, 8.2f, 12.0f, 4.8f, 29); }

            var northMeadow = natureRoot.Find("Northern gardens")?.GetComponent<MeshFilter>();
            if (northMeadow) { northMeadow.transform.position = Vector3.zero; northMeadow.sharedMesh = BuildMeadowPatchMesh("Northern gardens", 0f, 12.8f, 24.5f, 3.8f, 41); }
        }

        // 5. Stage existing Boulders, Palms, and Bushes across the entire island (-25 to +25) + build Full Island Expansion!
        if (natureRoot && kitMeshes.Count > 0)
        {
            var boulders = new List<Transform>();
            var palms = new List<Transform>();
            var bushes = new List<Transform>();

            foreach (Transform child in natureRoot)
            {
                if (child.name == "Boulder") boulders.Add(child);
                else if (child.name == "Palm") palms.Add(child);
                else if (child.name == "BerryBush") bushes.Add(child);
            }

            // Stage Boulders (NO barnacles on any rock!)
            Vector2[] boulderCoords = new[]
            {
                new Vector2(3.15f, TidehavenGame.Shore(3.15f) + 0.55f),   // 0: Large smooth shore boulder
                new Vector2(4.30f, TidehavenGame.Shore(4.30f) - 0.65f),   // 1: Submerged ripple rock
                new Vector2(2.05f, TidehavenGame.Shore(2.05f) + 0.85f),   // 2: Companion shore pebble
                new Vector2(-5.8f, 5.2f),  new Vector2(6.0f, 5.2f),
                new Vector2(-9.6f, 6.8f),  new Vector2(9.2f, 6.8f),
                new Vector2(-19.5f, 6.8f), new Vector2(21.5f, 6.8f),
                new Vector2(-22.5f, 5.2f), new Vector2(23.2f, 5.2f),
                new Vector2(-4.4f, 4.1f),  new Vector2(5.2f, 4.2f),
                new Vector2(-10.8f, 5.8f), new Vector2(9.8f, 4.2f),
                new Vector2(-19.2f, 4.2f), new Vector2(19.8f, 4.5f),
                new Vector2(-23.8f, 3.8f), new Vector2(24.0f, 4.0f),
                new Vector2(-8.2f, 7.6f),  new Vector2(8.4f, 7.6f),
                new Vector2(-20.5f, 7.8f)
            };

            for (int i = 0; i < boulders.Count; i++)
            {
                var b = boulders[i];
                var mf = b.GetComponentInChildren<MeshFilter>();
                var mr = b.GetComponentInChildren<MeshRenderer>();
                if (!mf || !mr) continue;

                mf.transform.localPosition = Vector3.zero;
                mf.transform.localRotation = Quaternion.identity;
                mf.transform.localScale = Vector3.one;
                mr.sharedMaterials = new[] { matOutlined };

                Vector2 pos2 = (i < boulderCoords.Length) ? boulderCoords[i] : new Vector2(-20f + i * 1.8f, 6.5f);
                if (i == 0)
                {
                    b.position = new Vector3(pos2.x, TidehavenGame.Ground(pos2.x, pos2.y) - 0.05f, pos2.y);
                    b.rotation = Quaternion.Euler(0f, 12f, 0f);
                    b.localScale = new Vector3(1.15f, 0.95f, 1.05f);
                    mf.sharedMesh = kitMeshes["Rock_SmoothCoastal"];
                }
                else if (i == 1)
                {
                    b.position = new Vector3(pos2.x, TidehavenGame.Ground(pos2.x, pos2.y) - 0.02f, pos2.y);
                    b.rotation = Quaternion.Euler(0f, -18f, 0f);
                    b.localScale = new Vector3(0.50f, 0.46f, 0.50f);
                    mf.sharedMesh = kitMeshes["Rock_SmoothCoastal"];
                }
                else if (i == 2)
                {
                    b.position = new Vector3(pos2.x, TidehavenGame.Ground(pos2.x, pos2.y) - 0.04f, pos2.y);
                    b.rotation = Quaternion.Euler(0f, 140f, 0f);
                    b.localScale = Vector3.one * 0.34f;
                    mf.sharedMesh = kitMeshes["Rock_SmoothCoastal"];
                }
                else if (i < 11)
                {
                    b.position = new Vector3(pos2.x, TidehavenGame.Ground(pos2.x, pos2.y) - 0.08f, pos2.y);
                    b.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                    b.localScale = Vector3.one * (1.05f + (i % 3) * 0.25f);
                    mf.sharedMesh = kitMeshes["Rock_SteppedSlate"];
                }
                else
                {
                    b.position = new Vector3(pos2.x, TidehavenGame.Ground(pos2.x, pos2.y) - 0.04f, pos2.y);
                    b.rotation = Quaternion.Euler(0f, i * 63f, 0f);
                    b.localScale = Vector3.one * (0.68f + (i % 3) * 0.18f);
                    mf.sharedMesh = (i % 2 == 0) ? kitMeshes["Rock_BluePebbleCluster"] : kitMeshes["Rock_SmoothCoastal"];
                }
            }

            // Stage all 18 Palms cleanly across West (-24..-4.5) and East (5.5..24.5) with zero House/Shop/Dock overlap!
            Vector2[] palmCoords = new[]
            {
                new Vector2(-4.5f, 4.8f),   new Vector2(-7.8f, 6.0f),
                new Vector2(5.6f, 5.0f),    new Vector2(8.8f, 6.8f),
                new Vector2(-10.2f, 6.6f),  new Vector2(-19.2f, 6.2f),
                new Vector2(-21.5f, 8.5f),  new Vector2(-23.8f, 6.0f),
                new Vector2(-19.5f, 12.2f), new Vector2(-10.2f, 13.0f),
                new Vector2(10.2f, 6.4f),   new Vector2(14.2f, 7.6f),
                new Vector2(21.5f, 6.8f),   new Vector2(23.8f, 9.2f),
                new Vector2(14.2f, 12.2f),  new Vector2(22.2f, 12.5f),
                new Vector2(-5.2f, 11.8f),  new Vector2(6.2f, 11.8f)
            };
            for (int i = 0; i < palms.Count && i < palmCoords.Length; i++)
            {
                float px = palmCoords[i].x, pz = palmCoords[i].y;
                palms[i].position = new Vector3(px, TidehavenGame.Ground(px, pz) - 0.08f, pz);
                palms[i].rotation = Quaternion.Euler(0f, i * 68f + 25f, 0f);
            }

            // Stage all 22 Bushes/Trees across the full island width (-24 to +24)
            Vector2[] broadleafPos = new[]
            {
                new Vector2(6.8f, 5.2f),   new Vector2(-7.2f, 5.6f),
                new Vector2(11.2f, 9.5f),  new Vector2(-19.2f, 9.8f),
                new Vector2(21.8f, 10.2f)
            };
            Vector2[] pinePos = new[]
            {
                new Vector2(-6.4f, 6.2f),  new Vector2(-3.8f, 7.2f),
                new Vector2(7.6f, 6.6f),   new Vector2(-11.5f, 12.8f),
                new Vector2(11.5f, 12.6f)
            };
            Vector2[] goldenShrubPos = new[]
            {
                new Vector2(-3.4f, 4.9f),  new Vector2(4.2f, 4.8f),
                new Vector2(-5.8f, 4.4f),  new Vector2(6.2f, 4.3f),
                new Vector2(-12.5f, 7.5f)
            };
            Vector2[] berryPos = new[]
            {
                new Vector2(-17.5f, 7.8f), new Vector2(-21.0f, 10.5f),
                new Vector2(10.2f, 7.2f),  new Vector2(15.8f, 8.2f),
                new Vector2(20.8f, 8.2f),  new Vector2(-8.5f, 10.8f),
                new Vector2(8.8f, 10.5f)
            };

            for (int i = 0; i < bushes.Count; i++)
            {
                var bush = bushes[i];
                var mf = bush.GetComponentInChildren<MeshFilter>();
                var mr = bush.GetComponentInChildren<MeshRenderer>();
                if (!mf || !mr) continue;

                if (i < 5)
                {
                    mf.transform.localPosition = Vector3.zero;
                    mf.transform.localRotation = Quaternion.identity;
                    mf.transform.localScale = Vector3.one;
                    mf.sharedMesh = kitMeshes["Tree_CoastalBroadleaf"];
                    mr.sharedMaterials = new[] { matOutlined };
                    float tx = broadleafPos[i].x, tz = broadleafPos[i].y;
                    bush.position = new Vector3(tx, TidehavenGame.Ground(tx, tz) - 0.05f, tz);
                    bush.rotation = Quaternion.Euler(0f, i * 55f, 0f);
                    bush.localScale = Vector3.one * (1.05f + (i % 2) * 0.18f);
                }
                else if (i < 10)
                {
                    mf.transform.localPosition = Vector3.zero;
                    mf.transform.localRotation = Quaternion.identity;
                    mf.transform.localScale = Vector3.one;
                    mf.sharedMesh = kitMeshes["Tree_CoastalPine"];
                    mr.sharedMaterials = new[] { matOutlined };
                    float tx = pinePos[i - 5].x, tz = pinePos[i - 5].y;
                    bush.position = new Vector3(tx, TidehavenGame.Ground(tx, tz) - 0.05f, tz);
                    bush.rotation = Quaternion.Euler(0f, i * 43f, 0f);
                    bush.localScale = Vector3.one * (1.10f + (i % 2) * 0.20f);
                }
                else if (i < 15)
                {
                    mf.transform.localPosition = Vector3.zero;
                    mf.transform.localRotation = Quaternion.identity;
                    mf.transform.localScale = Vector3.one;
                    mf.sharedMesh = kitMeshes["Shrub_GoldenOlive"];
                    mr.sharedMaterials = new[] { matOutlined };
                    float sx = goldenShrubPos[i - 10].x, sz = goldenShrubPos[i - 10].y;
                    bush.position = new Vector3(sx, TidehavenGame.Ground(sx, sz) - 0.04f, sz);
                    bush.rotation = Quaternion.Euler(0f, i * 67f, 0f);
                    bush.localScale = Vector3.one * (0.92f + (i % 3) * 0.12f);
                }
                else
                {
                    if (intactBerryMesh != null) mf.sharedMesh = intactBerryMesh;
                    int bi = Mathf.Min(i - 15, berryPos.Length - 1);
                    float bx = berryPos[bi].x, bz = berryPos[bi].y;
                    bush.position = new Vector3(bx, TidehavenGame.Ground(bx, bz) - 0.04f, bz);
                }
            }

            // 6. Build the Northern Mountain Cliff Wall Range, Waterfall Cascade, Trail Stepping Stones, West/East Grove Trees & Camp Props!
            var oldExpansion = natureRoot.Find("Island Mountain Range and Landmarks");
            if (oldExpansion) Object.DestroyImmediate(oldExpansion.gameObject);
            var expRoot = new GameObject("Island Mountain Range and Landmarks").transform;
            expRoot.SetParent(natureRoot, false);

            GameObject SpawnKitProp(string name, string meshKey, Vector3 pos, float yaw, Vector3 scale, bool collider = false)
            {
                if (!kitMeshes.TryGetValue(meshKey, out var m) || !m) return null;
                var go = new GameObject(name);
                go.transform.SetParent(expRoot, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { matOutlined };
                if (collider)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = m.bounds.center;
                    bc.size = m.bounds.size * 0.88f;
                }
                return go;
            }

            // 6a. Northern Mountain Cliff Range blocking the back side (-26 to +26) + West/East Headland Cliffs!
            float[] cliffXs = new[] { -25.5f, -20.0f, -14.8f, -9.2f, -3.8f, 5.2f, 10.8f, 17.5f, 23.8f };
            for (int i = 0; i < cliffXs.Length; i++)
            {
                float cx = cliffXs[i];
                // Step back behind the Abandoned House (-14.8) and Fisherman's Shack (18.0), otherwise sit right at z = 9.0 so the mountain wall is visible in camera!
                float cz = (Mathf.Abs(cx + 14.8f) < 3.5f || Mathf.Abs(cx - 17.5f) < 3.5f) ? 11.4f : 9.0f;
                Vector3 cpos = new Vector3(cx, TidehavenGame.Ground(cx, cz) - 0.35f, cz);
                SpawnKitProp("Mountain_CliffWall_" + i, "Mountain_CliffWall", cpos, (i % 2 == 0) ? 3f : -4f, new Vector3(1.15f, 1.15f + (i % 3) * 0.15f, 1.10f), true);
            }
            // Far-West & Far-East Headland Cliff Walls framing the ends of the beach!
            SpawnKitProp("West_Headland_Cliff_1", "Mountain_CliffWall", new Vector3(-27.2f, TidehavenGame.Ground(-27.2f, 8.5f) - 0.3f, 8.5f), 75f, new Vector3(1.1f, 1.15f, 1.1f), true);
            SpawnKitProp("West_Headland_Cliff_2", "Mountain_CliffWall", new Vector3(-27.5f, TidehavenGame.Ground(-27.5f, 4.2f) - 0.3f, 4.2f), 82f, new Vector3(1.0f, 0.95f, 1.0f), true);
            SpawnKitProp("East_Headland_Cliff_1", "Mountain_CliffWall", new Vector3(27.2f, TidehavenGame.Ground(27.2f, 8.5f) - 0.3f, 8.5f), -75f, new Vector3(1.1f, 1.15f, 1.1f), true);
            SpawnKitProp("East_Headland_Cliff_2", "Mountain_CliffWall", new Vector3(27.5f, TidehavenGame.Ground(27.5f, 4.2f) - 0.3f, 4.2f), -82f, new Vector3(1.0f, 0.95f, 1.0f), true);

            // 6b. Mountain Waterfall Cascade between the central-back cliffs at (0.6, 7.6)!
            float wfx = 0.6f, wfz = 7.6f;
            SpawnKitProp("Mountain_Waterfall", "Waterfall_Cascade", new Vector3(wfx, TidehavenGame.Ground(wfx, wfz) - 0.12f, wfz), 0f, new Vector3(1.15f, 1.18f, 1.12f), true);

            // 6c. Stepping Stones along the Trail Paths (Beach -> Crossroads -> Abandoned House Steps & Harbor Pier/Shack)
            Vector2[] steppingPts = new[]
            {
                new Vector2(-0.8f, 3.8f),   new Vector2(-0.8f, 5.2f),
                new Vector2(-2.8f, 5.4f),   new Vector2(-5.4f, 5.0f),
                new Vector2(-8.2f, 4.4f),   new Vector2(-11.2f, 3.9f),
                new Vector2(-13.8f, 3.8f),
                new Vector2(2.4f, 4.9f),    new Vector2(5.2f, 5.2f),
                new Vector2(8.0f, 5.3f),    new Vector2(10.6f, 4.4f),
                new Vector2(11.5f, 5.4f),   new Vector2(14.2f, 5.4f),
                new Vector2(16.6f, 5.2f)
            };
            for (int i = 0; i < steppingPts.Length; i++)
            {
                float sx = steppingPts[i].x, sz = steppingPts[i].y;
                SpawnKitProp("Path_SteppingStones_" + i, "Path_SteppingStones", new Vector3(sx, TidehavenGame.Ground(sx, sz) + 0.01f, sz), i * 37f, Vector3.one * 0.92f, false);
            }

            // 6d. Coastal Camp & Driftwood Props near the Abandoned House, West Cape, and East Harbor!
            Vector3[] campSpots = new[]
            {
                new Vector3(-11.5f, 0f, 4.8f),  // Abandoned House front yard
                new Vector3(-17.2f, 0f, 3.5f),  // West beach driftwood camp
                new Vector3(15.2f, 0f, 3.8f),   // East harbor beach near pier
                new Vector3(20.6f, 0f, 5.6f),   // Fisherman's Shack yard
            };
            for (int i = 0; i < campSpots.Length; i++)
            {
                float cx = campSpots[i].x, cz = campSpots[i].z;
                SpawnKitProp("Coastal_Camp_Prop_" + i, "Prop_CoastalCamp", new Vector3(cx, TidehavenGame.Ground(cx, cz) - 0.02f, cz), i * 52f + 15f, Vector3.one * 0.95f, false);
            }

            // 6e. Additional Trees & Shrubs on the Far-West (-25..-12) and Far-East (12..25) so walking left/right is lush and rich!
            Vector2[] extraBroadleaf = new[]
            {
                new Vector2(-18.5f, 11.8f), new Vector2(-23.2f, 9.6f),
                new Vector2(-9.6f, 7.5f),   new Vector2(15.2f, 12.6f),
                new Vector2(22.8f, 11.8f),  new Vector2(24.2f, 7.6f)
            };
            for (int i = 0; i < extraBroadleaf.Length; i++)
            {
                float tx = extraBroadleaf[i].x, tz = extraBroadleaf[i].y;
                SpawnKitProp("Extra_Broadleaf_" + i, "Tree_CoastalBroadleaf", new Vector3(tx, TidehavenGame.Ground(tx, tz) - 0.05f, tz), i * 61f, Vector3.one * (1.05f + (i % 2) * 0.2f), true);
            }

            Vector2[] extraPines = new[]
            {
                new Vector2(-16.2f, 13.8f), new Vector2(-21.8f, 12.8f),
                new Vector2(-24.5f, 7.8f),  new Vector2(-0.8f, 12.8f),
                new Vector2(5.2f, 12.8f),   new Vector2(16.8f, 13.4f),
                new Vector2(21.5f, 13.2f),  new Vector2(24.5f, 10.5f)
            };
            for (int i = 0; i < extraPines.Length; i++)
            {
                float tx = extraPines[i].x, tz = extraPines[i].y;
                SpawnKitProp("Extra_Pine_" + i, "Tree_CoastalPine", new Vector3(tx, TidehavenGame.Ground(tx, tz) - 0.05f, tz), i * 49f, Vector3.one * (1.10f + (i % 2) * 0.22f), true);
            }

            Vector2[] extraShrubs = new[]
            {
                new Vector2(-11.8f, 8.8f),  new Vector2(-16.8f, 9.2f),
                new Vector2(-20.2f, 6.8f),  new Vector2(-22.8f, 5.8f),
                new Vector2(9.8f, 5.8f),    new Vector2(16.8f, 7.8f),
                new Vector2(20.2f, 6.8f),   new Vector2(22.8f, 6.2f)
            };
            for (int i = 0; i < extraShrubs.Length; i++)
            {
                float sx = extraShrubs[i].x, sz = extraShrubs[i].y;
                SpawnKitProp("Extra_GoldenShrub_" + i, "Shrub_GoldenOlive", new Vector3(sx, TidehavenGame.Ground(sx, sz) - 0.04f, sz), i * 73f, Vector3.one * (0.90f + (i % 3) * 0.14f), false);
            }

            // 7. Generate Combined Single-Draw-Call 3D Grass Tufts & Wildflower Patches across the FULL Island (-25 to +25)!
            var oldFlora = natureRoot.Find("Combined Meadow Grasses and Wildflowers");
            if (oldFlora) Object.DestroyImmediate(oldFlora.gameObject);

            var grassSrc = kitMeshes["Grass_TuftCluster"];
            var flowerSrc = kitMeshes["Wildflower_Patch"];
            if (grassSrc && flowerSrc)
            {
                var cVerts = new List<Vector3>();
                var cNorms = new List<Vector3>();
                var cCols = new List<Color>();
                var cUV2 = new List<Vector4>();
                var cTris = new List<int>();

                void AppendInstance(Mesh m, Vector3 pos, float yaw, float scale)
                {
                    Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                    Matrix4x4 mat4 = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
                    var vs = m.vertices;
                    var ns = m.normals;
                    var cs = m.colors;
                    var ts = m.triangles;
                    int baseIdx = cVerts.Count;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        Vector3 wp = mat4.MultiplyPoint3x4(vs[i]);
                        cVerts.Add(wp);
                        cNorms.Add(mat4.MultiplyVector(ns[i]).normalized);
                        cCols.Add(cs[i]);
                        float hAbove = Mathf.Max(0f, wp.y - pos.y);
                        cUV2.Add(new Vector4(1f, hAbove, 0f, 0f));
                    }
                    for (int i = 0; i < ts.Length; i++)
                        cTris.Add(baseIdx + ts[i]);
                }

                Random.InitState(777);
                int placed = 0, attempts = 0;
                while (placed < 360 && attempts < 1500)
                {
                    attempts++;
                    float x, z;
                    if (placed < 90)
                    {
                        // Center-West foreground meadow
                        x = Random.Range(-10.0f, -1.6f);
                        z = Random.Range(Mathf.Max(3.5f, TidehavenGame.Shore(x) + 2.05f), 9.5f);
                    }
                    else if (placed < 180)
                    {
                        // Center-East foreground meadow
                        x = Random.Range(1.8f, 10.0f);
                        z = Random.Range(Mathf.Max(3.5f, TidehavenGame.Shore(x) + 2.05f), 9.5f);
                    }
                    else if (placed < 260)
                    {
                        // Far-West Abandoned House & Cape meadow (-24.5..-10)
                        x = Random.Range(-24.5f, -10.0f);
                        z = Random.Range(Mathf.Max(4.0f, TidehavenGame.Shore(x) + 2.15f), 13.5f);
                    }
                    else if (placed < 340)
                    {
                        // Far-East Harbor & Shack meadow (10..24.5)
                        x = Random.Range(10.0f, 24.5f);
                        z = Random.Range(Mathf.Max(4.0f, TidehavenGame.Shore(x) + 2.15f), 13.5f);
                    }
                    else
                    {
                        // Upper mountain-base meadow
                        x = Random.Range(-20f, 20f);
                        z = Random.Range(9.5f, 14.0f);
                    }

                    if (!IsClearOfLandmarksAndPaths(new Vector2(x, z), -0.25f))
                        continue;

                    Vector3 p = new Vector3(x, TidehavenGame.Ground(x, z) + 0.042f, z);
                    bool isFlower = (placed % 2 == 0);
                    AppendInstance(isFlower ? flowerSrc : grassSrc, p, Random.Range(0f, 360f), isFlower ? Random.Range(0.85f, 1.18f) : Random.Range(0.80f, 1.15f));
                    placed++;
                }

                string floraPath = $"{MeshFolder}/Combined_MeadowFlora.asset";
                var floraMesh = AssetDatabase.LoadAssetAtPath<Mesh>(floraPath);
                bool isNew = false;
                if (!floraMesh) { floraMesh = new Mesh(); isNew = true; }
                else floraMesh.Clear();

                floraMesh.name = "Combined_MeadowFlora";
                if (cVerts.Count > 65535) floraMesh.indexFormat = IndexFormat.UInt32;
                floraMesh.SetVertices(cVerts);
                floraMesh.SetNormals(cNorms);
                floraMesh.SetColors(cCols);
                floraMesh.SetUVs(1, cUV2);
                floraMesh.SetTriangles(cTris, 0);
                floraMesh.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(floraMesh, floraPath);
                else EditorUtility.SetDirty(floraMesh);

                var floraGO = new GameObject("Combined Meadow Grasses and Wildflowers");
                floraGO.transform.SetParent(natureRoot, false);
                floraGO.AddComponent<MeshFilter>().sharedMesh = floraMesh;
                var floraMR = floraGO.AddComponent<MeshRenderer>();
                floraMR.sharedMaterials = new[] { matNoOutline };
                floraMR.shadowCastingMode = ShadowCastingMode.Off;
            }

            // 8. Morning Sunbeams across the cove
            var oldBeams = natureRoot.Find("Morning Sunbeams");
            if (oldBeams) Object.DestroyImmediate(oldBeams.gameObject);

            var sunbeamShader = Shader.Find("Tidehaven/Sunbeam");
            if (sunbeamShader)
            {
                string sbMatPath = $"{MatFolder}/MorningSunbeams.mat";
                var sbMat = AssetDatabase.LoadAssetAtPath<Material>(sbMatPath);
                if (!sbMat)
                {
                    sbMat = new Material(sunbeamShader);
                    AssetDatabase.CreateAsset(sbMat, sbMatPath);
                }
                sbMat.shader = sunbeamShader;
                sbMat.SetColor("_Color", new Color(1.0f, 0.96f, 0.80f, 0.075f));
                EditorUtility.SetDirty(sbMat);

                var bVerts = new List<Vector3>();
                var bUVs = new List<Vector2>();
                var bCols = new List<Color>();
                var bTris = new List<int>();

                Vector3[] beamCenters = new[]
                {
                    new Vector3(-5.5f, 3.2f, 4.5f),
                    new Vector3(-2.0f, 3.0f, 2.5f),
                    new Vector3(2.2f, 2.8f, 1.5f),
                    new Vector3(6.2f, 3.2f, 4.0f)
                };
                float[] beamWidths = new[] { 2.8f, 2.2f, 2.5f, 2.6f };
                Vector3 beamDir = new Vector3(0.52f, -0.72f, -0.45f).normalized;
                Vector3 beamSide = Vector3.Cross(beamDir, Vector3.forward).normalized;

                for (int i = 0; i < beamCenters.Length; i++)
                {
                    Vector3 c = beamCenters[i];
                    float w = beamWidths[i];
                    float len = 11.5f;
                    int bi = bVerts.Count;
                    bVerts.Add(c - beamDir * (len * 0.5f) - beamSide * w);
                    bVerts.Add(c - beamDir * (len * 0.5f) + beamSide * w);
                    bVerts.Add(c + beamDir * (len * 0.5f) + beamSide * w);
                    bVerts.Add(c + beamDir * (len * 0.5f) - beamSide * w);
                    bUVs.Add(new Vector2(0f, 0f));
                    bUVs.Add(new Vector2(1f, 0f));
                    bUVs.Add(new Vector2(1f, 1f));
                    bUVs.Add(new Vector2(0f, 1f));
                    Color col = new Color(i / 4f, 1f, 1f, 1f);
                    bCols.Add(col); bCols.Add(col); bCols.Add(col); bCols.Add(col);
                    bTris.AddRange(new[] { bi, bi + 1, bi + 2, bi, bi + 2, bi + 3 });
                }

                string sbMeshPath = $"{MeshFolder}/MorningSunbeams.asset";
                var sbMesh = AssetDatabase.LoadAssetAtPath<Mesh>(sbMeshPath);
                bool sbNew = false;
                if (!sbMesh) { sbMesh = new Mesh(); sbNew = true; }
                else sbMesh.Clear();
                sbMesh.name = "MorningSunbeams";
                sbMesh.SetVertices(bVerts);
                sbMesh.SetUVs(0, bUVs);
                sbMesh.SetColors(bCols);
                sbMesh.SetTriangles(bTris, 0);
                sbMesh.RecalculateBounds();
                if (sbNew) AssetDatabase.CreateAsset(sbMesh, sbMeshPath);
                else EditorUtility.SetDirty(sbMesh);

                var sbGO = new GameObject("Morning Sunbeams");
                sbGO.transform.SetParent(natureRoot, false);
                sbGO.AddComponent<MeshFilter>().sharedMesh = sbMesh;
                var sbMR = sbGO.AddComponent<MeshRenderer>();
                sbMR.sharedMaterials = new[] { sbMat };
                sbMR.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // 9. Submerge ALL Corals, Reefs, and Seagrass strictly below water level (top <= -0.32f) + add extra Fish & Reefs across West/East!
        var reefRoot = GameObject.Find("Living reef")?.transform;
        var gameComp = Object.FindFirstObjectByType<TidehavenGame>();
        if (reefRoot)
        {
            var oldExtraReef = reefRoot.Find("Expanded Lagoon Reefs and Fish");
            if (oldExtraReef) Object.DestroyImmediate(oldExtraReef.gameObject);

            // Clean up any null fish entries first
            if (gameComp && gameComp.fish != null)
                gameComp.fish.RemoveAll(f => f == null);

            Mesh reefMesh = null, coralMesh = null;
            foreach (Transform child in reefRoot)
            {
                if (child.name == "Reef" || child.name == "Coral" || child.name == "Seagrass")
                {
                    var mf = child.GetComponentInChildren<MeshFilter>();
                    if (child.name == "Reef" && mf && !reefMesh) reefMesh = mf.sharedMesh;
                    if (child.name == "Coral" && mf && !coralMesh) coralMesh = mf.sharedMesh;

                    // Ensure every reef/coral sits in deep enough lagoon water (z <= Shore(x) - 3.2f) and its highest point is <= -0.34f!
                    Vector3 p = child.position;
                    float maxAllowedZ = TidehavenGame.Shore(p.x) - 3.2f;
                    if (p.z > maxAllowedZ) p.z = maxAllowedZ - Mathf.Abs(p.x % 2.5f);
                    // Avoid the Harbor Pier at x = 13.5
                    if (Mathf.Abs(p.x - 13.5f) < 3.5f && p.z > -8.5f) p.x -= 5.0f;

                    p.y = TidehavenGame.Ground(p.x, p.z) - 0.15f;
                    child.position = p;

                    var r = child.GetComponentInChildren<Renderer>();
                    if (r)
                    {
                        float topY = r.bounds.max.y;
                        if (topY > -0.34f)
                        {
                            p.y -= (topY - (-0.34f));
                            child.position = p;
                        }
                    }
                }
                else if (child.name.Contains("Fish"))
                {
                    Vector3 p = child.position;
                    float maxZ = TidehavenGame.Shore(p.x) - 2.2f;
                    if (p.z > maxZ) p.z = maxZ;
                    p.y = Mathf.Clamp(p.y, -0.85f, -0.38f);
                    child.position = p;
                }
            }

            // Spawn additional submerged reefs & 16 new tropical fish across the West (-24..-9) and East (9..24) lagoon!
            var extraReefRoot = new GameObject("Expanded Lagoon Reefs and Fish").transform;
            extraReefRoot.SetParent(reefRoot, false);

            Vector2[] extraReefPts = new[]
            {
                new Vector2(-12.5f, -3.8f), new Vector2(-16.8f, -4.5f),
                new Vector2(-21.2f, -3.6f), new Vector2(-24.0f, -5.2f),
                new Vector2(10.2f, -4.2f),  new Vector2(17.5f, -4.0f),
                new Vector2(21.0f, -3.5f),  new Vector2(24.2f, -4.8f)
            };
            for (int i = 0; i < extraReefPts.Length; i++)
            {
                Mesh m = (i % 2 == 0) ? (reefMesh ? reefMesh : coralMesh) : (coralMesh ? coralMesh : reefMesh);
                if (!m) continue;
                var go = new GameObject((i % 2 == 0) ? "Reef_Expanded_" + i : "Coral_Expanded_" + i);
                go.transform.SetParent(extraReefRoot, false);
                float rx = extraReefPts[i].x, rz = extraReefPts[i].y;
                go.transform.position = new Vector3(rx, -1.25f, rz);
                go.transform.rotation = Quaternion.Euler(-90f, i * 53f, 0f);
                go.transform.localScale = Vector3.one * 72f;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { matOutlined };
                float topY = mr.bounds.max.y;
                if (topY > -0.36f)
                    go.transform.position -= new Vector3(0f, topY - (-0.36f), 0f);
            }

            if (kitMeshes.TryGetValue("Fish_BlueTang", out var blueTangMesh) && blueTangMesh && gameComp)
            {
                Random.InitState(909);
                for (int i = 0; i < 16; i++)
                {
                    float fx = (i < 6) ? Random.Range(-23.5f, -8.5f) : (i < 11 ? Random.Range(8.5f, 23.5f) : Random.Range(-8.0f, 8.0f));
                    float fz = Random.Range(TidehavenGame.Shore(fx) - 7.5f, TidehavenGame.Shore(fx) - 2.4f);
                    var fgo = new GameObject("BlueTangFish_" + i);
                    fgo.transform.SetParent(extraReefRoot, false);
                    fgo.transform.position = new Vector3(fx, Random.Range(-0.78f, -0.40f), fz);
                    fgo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    fgo.transform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
                    fgo.AddComponent<MeshFilter>().sharedMesh = blueTangMesh;
                    fgo.AddComponent<MeshRenderer>().sharedMaterials = new[] { matOutlined };
                    if (!fgo.GetComponent<FishTailMotion>()) fgo.AddComponent<FishTailMotion>();
                    gameComp.fish.Add(fgo.transform);
                }
                EditorUtility.SetDirty(gameComp);
            }
        }

        // 10. Tune CoveWater.mat so deeper water transitions to sky-blue & shallow water stays crystal clear
        var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Tidehaven/Materials/CoveWater.mat");
        if (water)
        {
            water.SetColor("_Color_Shallow", new Color(0.45f, 0.86f, 0.78f, 0.08f));
            water.SetColor("_Color_Deep", new Color(0.16f, 0.58f, 0.90f, 0.34f));
            water.SetFloat("_Water_Depth", 1.6f);
            water.SetFloat("_ENABLESHORELINE", 0f);
            water.DisableKeyword("_ENABLESHORELINE");
            EditorUtility.SetDirty(water);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        return "Rebuilt full CoralCove island map (-27 to +27): removed barnacles, submerged all corals below water level, added back Mountain Cliff Range & Waterfall, added textured ground grass & wind-rippled sand with winding trail paths to the Abandoned House & Harbor Pier, added 16 new Blue Tang fish, added interactive sand footprint/puff trail, and added catch-to-backpack animation.";
    }
}
