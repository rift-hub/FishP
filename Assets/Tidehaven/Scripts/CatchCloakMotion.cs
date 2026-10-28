using System.Collections.Generic;
using UnityEngine;

// The shoulder cloak is weighted rigidly to UpperChest in the source model.
// Flex its cloth vertices before skinning, keeping the authored pose and collar intact.
[DefaultExecutionOrder(220), DisallowMultipleComponent]
public class CatchCloakMotion : MonoBehaviour
{
    public Transform visual;
    public CatchCelebration celebration;
    [Range(0f, .06f)] public float breezeAmplitude = .024f;
    [Range(0f, .12f)] public float sleeveLift = .065f;

    sealed class ClothPart
    {
        public SkinnedMeshRenderer renderer;
        public Mesh source, animated;
        public Bounds originalBounds, animatedBounds;
        public Matrix4x4 rootToMesh, bindPose;
        public Transform chestBone;
        public bool strawLayers;
        public Vector3[] vertices, normals, rest, outward;
        public int[] indices, triangles;
        public float[] weight, sideWeight, phase, layerClearance;
        public Vector3[] phaseSin, phaseCos;
    }

    readonly List<ClothPart> parts = new List<ClothPart>();
    Transform leftArm, leftElbow, rightArm, rightElbow;
    float leftLift, rightLift, catchWeight;
    bool initialized;

    public void Initialize(Transform character, CatchCelebration catchScene)
    {
        if (initialized && visual == character)
        {
            celebration = catchScene;
            return;
        }
        ReleaseMeshes();
        visual = character;
        celebration = catchScene;
        if (!visual) return;

        foreach (var bone in visual.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "LeftUpperArm") leftArm = bone;
            else if (bone.name == "LeftLowerArm") leftElbow = bone;
            else if (bone.name == "RightUpperArm") rightArm = bone;
            else if (bone.name == "RightLowerArm") rightElbow = bone;
        }

        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var source = renderer.sharedMesh;
            if (!source) continue;
            var materials = renderer.sharedMaterials;
            var clothSubmeshes = new List<int>();
            for (int s = 0; s < Mathf.Min(source.subMeshCount, materials.Length); s++)
            {
                if (!materials[s]) continue;
                string materialName = materials[s].name;
                if (materialName.Contains("Cloth_Cloak") || materialName.Contains("Shoulder_Wrap") ||
                    materialName.Contains("Cloth_Hem") || materialName.Contains("Drawn_Straw_Layer")) clothSubmeshes.Add(s);
            }
            if (clothSubmeshes.Count == 0) continue;
            if (!source.isReadable)
            {
                Debug.LogWarning("Cloak motion requires Read/Write on the fisherman model importer.", renderer);
                continue;
            }

            var triangles = new List<int>();
            var selected = new HashSet<int>();
            foreach (int submesh in clothSubmeshes)
                foreach (int index in source.GetTriangles(submesh))
                {
                    triangles.Add(index);
                    selected.Add(index);
                }
            if (selected.Count == 0) continue;

            var meshToRoot = visual.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var vertices = source.vertices;
            var indices = new int[selected.Count];
            selected.CopyTo(indices);
            var rest = new Vector3[indices.Length];
            var bounds = new Bounds(meshToRoot.MultiplyPoint3x4(vertices[indices[0]]), Vector3.zero);
            for (int i = 0; i < indices.Length; i++)
            {
                rest[i] = meshToRoot.MultiplyPoint3x4(vertices[indices[i]]);
                bounds.Encapsulate(rest[i]);
            }

            var animated = Instantiate(source);
            animated.name = source.name + " (moving cloak)";
            animated.MarkDynamic();
            var part = new ClothPart
            {
                renderer = renderer, source = source, animated = animated,
                strawLayers = renderer.name.StartsWith("SM_Straw_"),
                originalBounds = renderer.localBounds, rootToMesh = meshToRoot.inverse,
                chestBone = renderer.bones[source.boneWeights[indices[0]].boneIndex0],
                bindPose = source.bindposes[source.boneWeights[indices[0]].boneIndex0],
                vertices = vertices, normals = source.normals, rest = rest,
                indices = indices, triangles = triangles.ToArray(),
                outward = new Vector3[indices.Length], weight = new float[indices.Length],
                sideWeight = new float[indices.Length], phase = new float[indices.Length],
                layerClearance = new float[indices.Length],
                phaseSin = new Vector3[indices.Length], phaseCos = new Vector3[indices.Length]
            };
            var upperWrap = new HashSet<int>();
            foreach (int submesh in clothSubmeshes)
                if (materials[submesh].name.Contains("Shoulder_Wrap") || materials[submesh].name.Contains("Straw_Layer_1"))
                    foreach (int vertex in source.GetTriangles(submesh)) upperWrap.Add(vertex);
            float pinnedHeight = bounds.max.y - bounds.size.y * .18f;
            for (int i = 0; i < indices.Length; i++)
            {
                Vector3 p = rest[i];
                // The original mantle was narrower than the raised sleeves. Add shoulder
                // ease across the whole side panel, including its upper wrap and edge.
                float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.10f, .28f, Mathf.Abs(p.x)));
                if (!part.strawLayers)
                {
                    p.x += Mathf.Sign(p.x) * .075f * shoulder;
                    p.y += .025f * shoulder;
                }
                rest[i] = p;
                part.layerClearance[i] = upperWrap.Contains(indices[i]) ? .012f : 0f;
                // All three cloth materials share this field, including the worn edge,
                // so their duplicated seam vertices remain together as the hem moves.
                part.weight[i] = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(pinnedHeight, bounds.min.y, p.y));
                part.sideWeight[i] = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(bounds.extents.x * .28f, bounds.extents.x * .76f,
                        Mathf.Abs(p.x - bounds.center.x)));
                part.outward[i] = new Vector3(p.x - bounds.center.x, 0f, p.z - bounds.center.z).normalized;
                part.phase[i] = p.x * 13f + p.z * 8f + p.y * 5f;
                float phase = part.phase[i];
                part.phaseSin[i] = new Vector3(Mathf.Sin(phase), Mathf.Sin(-phase * .8f), Mathf.Sin(phase * .65f));
                part.phaseCos[i] = new Vector3(Mathf.Cos(phase), Mathf.Cos(-phase * .8f), Mathf.Cos(phase * .65f));
                if (part.strawLayers)
                {
                    // Use the same field on all three overlapping sheets so their
                    // authored stepped gaps stay open as the mantle moves.
                    part.weight[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.48f, 1.14f, p.y));
                    part.sideWeight[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .35f, Mathf.Abs(p.x)));
                    part.outward[i] = new Vector3(p.x, 0f, p.z).normalized;
                }
            }

            var expandedBounds = part.originalBounds;
            expandedBounds.Expand(meshToRoot.inverse.MultiplyVector(Vector3.one * .2f).magnitude);
            part.animatedBounds = expandedBounds;
            renderer.localBounds = expandedBounds;
            renderer.sharedMesh = animated;
            parts.Add(part);
        }
        initialized = true;
    }

    void Start()
    {
        if (!initialized && visual) Initialize(visual, celebration);
    }

    float ArmLift(Transform shoulder, Transform elbow)
    {
        if (!shoulder || !elbow) return 0f;
        float elevation = Vector3.Dot((elbow.position - shoulder.position).normalized, visual.up);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.75f, .3f, elevation));
    }

    void LateUpdate()
    {
        if (!initialized || !visual || parts.Count == 0) return;
        bool inCelebration = celebration && celebration.Playing;
        // During normal exploration, update every 2nd frame since breeze amplitude is subtle (2.4cm)
        if (!inCelebration && (Time.frameCount & 1) != 0) return;

        // A slight lag lets the cloth settle after the sleeve instead of moving as rigid armour.
        float follow = 1f - Mathf.Exp(-Time.deltaTime * (inCelebration ? 7f : 12f));
        leftLift = Mathf.Lerp(leftLift, ArmLift(leftArm, leftElbow), follow);
        rightLift = Mathf.Lerp(rightLift, ArmLift(rightArm, rightElbow), follow);
        catchWeight = Mathf.Lerp(catchWeight, inCelebration ? celebration.PoseWeight : 0f, follow);
        float time = Time.time;
        float amplitude = breezeAmplitude * (1f + catchWeight * .65f);
        // The phase is constant per vertex. Angle addition moves thousands of trig calls
        // out of the vertex loop without changing the authored cloth motion.
        Vector3 timeSin = new Vector3(Mathf.Sin(time * 3.1f), Mathf.Sin(time * 5.2f), Mathf.Sin(time * 1.55f));
        Vector3 timeCos = new Vector3(Mathf.Cos(time * 3.1f), Mathf.Cos(time * 5.2f), Mathf.Cos(time * 1.55f));

        foreach (var part in parts)
        {
            if (!part.renderer || !part.animated) continue;
            // These cloth panels are authored entirely on UpperChest. Resolve contact in
            // the posed world, then return the correction to mesh bind space for skinning.
            Matrix4x4 skinToWorld = part.strawLayers ? Matrix4x4.identity : part.chestBone.localToWorldMatrix * part.bindPose;
            Matrix4x4 worldToSkin = part.strawLayers ? Matrix4x4.identity : skinToWorld.inverse;
            for (int i = 0; i < part.indices.Length; i++)
            {
                Vector3 p = part.rest[i];
                Vector3 ps = part.phaseSin[i], pc = part.phaseCos[i];
                float flutter = timeSin.x * pc.x + timeCos.x * ps.x + .28f * (timeSin.y * pc.y + timeCos.y * ps.y);
                float gust = .5f + .5f * (timeSin.z * pc.z + timeCos.z * ps.z);
                float lift = (p.x < 0f ? leftLift : rightLift) * part.sideWeight[i];
                Vector3 offset = part.outward[i] * (amplitude * (.32f + flutter * .5f) + lift * .018f);
                offset += new Vector3(amplitude * flutter * .28f,
                    amplitude * flutter * .17f + sleeveLift * lift,
                    -amplitude * gust * .48f);
                if (part.strawLayers) offset *= .35f;
                Vector3 vertex = part.rootToMesh.MultiplyPoint3x4(p + offset * part.weight[i]);
                if (!part.strawLayers)
                {
                    Vector3 world = skinToWorld.MultiplyPoint3x4(vertex);
                    world = ClearSleeve(world, leftArm, leftElbow, part.layerClearance[i]);
                    world = ClearSleeve(world, rightArm, rightElbow, part.layerClearance[i]);
                    vertex = worldToSkin.MultiplyPoint3x4(world);
                }
                part.vertices[part.indices[i]] = vertex;
            }
            part.animated.vertices = part.vertices;
        }
    }

    Vector3 ClearSleeve(Vector3 point, Transform shoulder, Transform elbow, float layerClearance)
    {
        if (!shoulder || !elbow) return point;
        Vector3 a = shoulder.position;
        Vector3 b = Vector3.Lerp(a, elbow.position, .70f);
        Vector3 axis = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, axis) / Mathf.Max(.0001f, axis.sqrMagnitude));
        Vector3 center = a + axis * t;
        Vector3 away = point - center;
        float radius = Mathf.Lerp(.16f, .14f, t) + layerClearance;
        if (away.sqrMagnitude < radius * radius)
            // Preserve separation between inner/outer cloth faces instead of collapsing
            // both onto one capsule surface (which causes striped z-fighting).
            point = center + (away.sqrMagnitude > .000001f ? away.normalized : visual.up) * (radius * .8f + away.magnitude * .2f);
        return point;
    }

    void OnEnable()
    {
        foreach (var part in parts)
            if (part.renderer && part.animated)
            {
                part.renderer.sharedMesh = part.animated;
                part.renderer.localBounds = part.animatedBounds;
            }
    }

    void RestoreMeshes()
    {
        foreach (var part in parts)
            if (part.renderer && part.renderer.sharedMesh == part.animated)
            {
                part.renderer.sharedMesh = part.source;
                part.renderer.localBounds = part.originalBounds;
            }
    }

    void ReleaseMeshes()
    {
        RestoreMeshes();
        foreach (var part in parts)
            if (part.animated) Destroy(part.animated);
        parts.Clear();
        leftArm = leftElbow = rightArm = rightElbow = null;
        leftLift = rightLift = catchWeight = 0f;
        initialized = false;
    }

    void OnDisable() { RestoreMeshes(); }
    void OnDestroy() { ReleaseMeshes(); }
}
