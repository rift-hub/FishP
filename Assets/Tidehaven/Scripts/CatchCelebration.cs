using UnityEngine;
using System.Collections.Generic;

// Blender supplies the full-body lift/hold/lower. This component attaches props, animates stowing the catch into the backpack, and directs the shot.
[DefaultExecutionOrder(180)]
public class CatchCelebration : MonoBehaviour
{
    public AnimationClip reveal;
    public float duration = 6.5f;
    public bool Playing => active;
    public float Elapsed => active ? Mathf.Clamp(Time.time - started, 0f, duration) : 0f;
    public float PoseWeight => active ? Ease(Elapsed / 0.25f) * (1f - Ease((Elapsed - 6.1f) / 0.4f)) : 0f;

    TidehavenGame game;
    StrandedGear gear;
    Transform left, right, leftArm, leftElbow, rightArm, rightElbow;
    GameObject prize, displayFloat;
    LineRenderer line;
    readonly Dictionary<Transform, Quaternion> fingerRest = new Dictionary<Transform, Quaternion>();
    readonly List<Mesh> floatMeshes = new List<Mesh>();
    UnityEngine.Rendering.Universal.DepthOfField depthOfField;
    UnityEngine.Rendering.Volume shotVolume;
    UnityEngine.Rendering.VolumeProfile shotProfile;
    float started;
    bool active;
    int lastCatch;
    Vector3 cameraStart, returnPosition, fishMouth, baseBagScale = Vector3.one;
    Quaternion rotationStart, returnRotation;
    float sizeStart, fovStart;

    static float Ease(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (6f * x - 15f) + 10f);
    }

    static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    void Awake()
    {
        game = GetComponent<TidehavenGame>();
        gear = GetComponent<StrandedGear>();
        foreach (var t in game.visual.GetComponentsInChildren<Transform>())
        {
            switch (t.name)
            {
                case "LeftHand": left = t; break;
                case "RightHand": right = t; break;
                case "LeftUpperArm": leftArm = t; break;
                case "LeftLowerArm": leftElbow = t; break;
                case "RightUpperArm": rightArm = t; break;
                case "RightLowerArm": rightElbow = t; break;
            }
            if ((t.name.StartsWith("Left") || t.name.StartsWith("Right")) &&
                (t.name.EndsWith("Proximal") || t.name.EndsWith("Intermediate") || t.name.EndsWith("Distal")) &&
                (t.name.Contains("Index") || t.name.Contains("Middle") || t.name.Contains("Ring") || t.name.Contains("Little") || t.name.Contains("Thumb")))
            {
                fingerRest[t] = t.localRotation;
            }
        }
        var cloak = GetComponent<CatchCloakMotion>();
        if (!cloak) cloak = gameObject.AddComponent<CatchCloakMotion>();
        cloak.Initialize(game.visual, this);
        lastCatch = game.catches;
    }

    void PoseFingers(float weight)
    {
        foreach (var entry in fingerRest)
        {
            var bone = entry.Key;
            bool lineHand = bone.name.StartsWith("Left");
            bool proximal = bone.name.EndsWith("Proximal"), middle = bone.name.EndsWith("Intermediate");
            Quaternion curl;
            if (bone.name.Contains("Thumb"))
                curl = Quaternion.Euler(proximal ? 35f : middle ? 25f : 20f, 0f, proximal ? (lineHand ? -55f : 55f) : 0f);
            else
                curl = Quaternion.Euler(lineHand ? (proximal ? 55f : middle ? 65f : 25f) : (proximal ? 55f : middle ? 75f : 45f), 0f, 0f);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, entry.Value * curl, weight);
        }
    }

    void Update()
    {
        if (game.catches > lastCatch)
        {
            lastCatch = game.catches;
            Begin();
        }
    }

    public void Begin()
    {
        if (active) return;
        active = true;
        started = Time.time;
        cameraStart = game.view.transform.position;
        rotationStart = game.view.transform.rotation;
        sizeStart = game.view.orthographicSize;
        fovStart = game.view.fieldOfView;
        if (gear && gear.bag) baseBagScale = gear.bag.localScale;

        var focus = game.player.position + Vector3.up * 0.8f + Vector3.back * 1.5f;
        var offset = new Vector3(0f, 11.6f, -9.0f);
        returnPosition = focus + offset * game.zoom;
        returnRotation = Quaternion.LookRotation(-offset);

        if (!prize && game.fish.Count > 0)
        {
            prize = Instantiate(game.fish[0].gameObject);
            prize.name = "Presented catch";
            foreach (var c in prize.GetComponentsInChildren<Collider>()) c.enabled = false;
            var verts = new List<Vector3>();
            foreach (var mf in prize.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices)
                    verts.Add(prize.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));

            float minX = float.MaxValue;
            foreach (var v in verts) minX = Mathf.Min(minX, v.x);
            Vector3 mouthSum = Vector3.zero;
            int mouthCount = 0;
            foreach (var v in verts)
                if (v.x <= minX + 0.001f) { mouthSum += v; mouthCount++; }
            fishMouth = mouthCount > 0 ? mouthSum / mouthCount : new Vector3(minX, 0f, 0f);
        }

        if (!line)
        {
            line = new GameObject("Catch display line").AddComponent<LineRenderer>();
            line.sharedMaterial = game.fishingLine.sharedMaterial;
            line.startWidth = line.endWidth = 0.005f;
            line.positionCount = 25;
        }

        if (!shotVolume)
        {
            var go = new GameObject("Catch focus");
            go.layer = 0;
            shotVolume = go.AddComponent<UnityEngine.Rendering.Volume>();
            shotVolume.isGlobal = true;
            shotVolume.priority = 100;
            shotProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            shotVolume.sharedProfile = shotProfile;
            depthOfField = shotProfile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
            depthOfField.mode.Override(game.view.orthographic ? UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian : UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh);
            depthOfField.aperture.Override(4f);
            depthOfField.focalLength.Override(70f);
            depthOfField.gaussianMaxRadius.Override(1.5f);
            depthOfField.highQualitySampling.Override(true);
        }

        if (!displayFloat && gear.carriedFloat)
        {
            displayFloat = Instantiate(gear.carriedFloat.gameObject);
            displayFloat.name = "Reveal float";
            foreach (var mf in displayFloat.GetComponentsInChildren<MeshFilter>(true))
            {
                var copy = Instantiate(mf.sharedMesh);
                floatMeshes.Add(copy);
                var vertices = copy.vertices;
                for (int sub = 0; sub < copy.subMeshCount; sub++)
                {
                    var old = copy.GetTriangles(sub);
                    var kept = new List<int>();
                    for (int i = 0; i < old.Length; i += 3)
                    {
                        bool keep = true;
                        for (int k = 0; k < 3; k++)
                            if (displayFloat.transform.InverseTransformPoint(mf.transform.TransformPoint(vertices[old[i + k]])).y < -0.075f)
                                keep = false;
                        if (keep) { kept.Add(old[i]); kept.Add(old[i + 1]); kept.Add(old[i + 2]); }
                    }
                    copy.SetTriangles(kept, sub);
                }
                copy.RecalculateBounds();
                mf.sharedMesh = copy;
            }
        }
    }

    void LateUpdate()
    {
        if (!active) return;
        float t = Elapsed;
        float lift = Ease(t / 1.15f);
        float lower = Ease((t - 5.2f) / 1.25f);
        float pose = lift * (1f - lower);
        var p = game.player;

        // Phase 2 (t = 3.4s..5.1s): Left hand arcs over the left shoulder and puts the caught fish into the backpack!
        float toBag = Ease((t - 3.4f) / 1.25f);
        float fromBag = Ease((t - 4.95f) / 1.10f);
        float bagReach = toBag * (1f - fromBag);

        float settle = Mathf.Sin(t * 1.7f) * 0.008f * pose;
        LimbContact.Solve(rightArm, rightElbow, right, p.TransformPoint(new Vector3(0.68f, 1.45f, 0.04f)), p.TransformPoint(new Vector3(0.65f, 1.22f, -0.24f)), pose);

        // Compute left hand trajectory: front display -> over left shoulder -> top mouth of straw bag on back!
        Vector3 frontHand = p.TransformPoint(new Vector3(-0.50f, 1.46f + settle, 0.32f));
        Vector3 shoulderArc = p.TransformPoint(new Vector3(-0.67f, 1.83f, -0.02f));
        Vector3 bagOpening = (gear && gear.bag && gear.hasBag)
            ? gear.bag.TransformPoint(new Vector3(-0.19f, 0.36f, -0.02f))
            : p.TransformPoint(new Vector3(0.0f, 1.38f, -0.26f));

        Vector3 leftTarget = QuadBezier(frontHand, shoulderArc, bagOpening + Vector3.up * 0.05f, bagReach);
        Vector3 leftPole = Vector3.Lerp(
            p.TransformPoint(new Vector3(-0.64f, 1.16f, 0.03f)),
            p.TransformPoint(new Vector3(-0.70f, 1.48f, 0.08f)),
            bagReach);
        LimbContact.Solve(leftArm, leftElbow, left, leftTarget, leftPole, pose);

        var shaft = Vector3.Slerp(new Vector3(0f, 0.85f, 0.65f), new Vector3(0.16f, 1f, -0.20f), pose).normalized;
        var rodRotation = p.rotation * Quaternion.FromToRotation(Vector3.up, shaft);
        Vector3 normal = Vector3.ProjectOnPlane(p.right, rodRotation * Vector3.up).normalized;
        right.rotation = Quaternion.LookRotation(normal, Vector3.Cross(normal, rodRotation * Vector3.up));
        gear.rod.SetPositionAndRotation(right.TransformPoint(new Vector3(0f, 0.06f, 0.025f)), rodRotation);

        // The rig's +Y runs from wrist to knuckles. Follow the solved forearm
        // instead of forcing a world-space pose that folds the wrist backward.
        Vector3 handDirection = (left.position - leftElbow.position).normalized;
        Vector3 palmNormal = Vector3.ProjectOnPlane(p.right, handDirection).normalized;
        if (palmNormal.sqrMagnitude < 0.001f)
            palmNormal = Vector3.ProjectOnPlane(p.forward, handDirection).normalized;
        Quaternion lineGrip = Quaternion.LookRotation(palmNormal, handDirection);
        left.rotation = Quaternion.Slerp(left.rotation, lineGrip, PoseWeight);
        PoseFingers(PoseWeight);

        Vector3 pinch = left.TransformPoint(new Vector3(-0.020f, 0.052f, 0.025f));
        float appear = Ease((t - 0.30f) / 0.65f);
        float dropInBag = Ease((t - 4.45f) / 0.55f); // Fish slides down inside the bag opening between 4.45s and 5.0s
        float visible = appear * (1f - dropInBag);

        Vector3 handMouth = pinch - Vector3.up * 0.18f + p.right * Mathf.Sin(t * 2.6f) * 0.014f;
        Vector3 insideBag = bagOpening - Vector3.up * 0.18f;
        Vector3 mouth = Vector3.Lerp(handMouth, insideBag, dropInBag);

        if (prize)
        {
            prize.SetActive(visible > 0.01f);
            prize.transform.localScale = Vector3.one * (0.56f * visible);
            // Tilt nose-down into the bag as toBag approaches 1
            Quaternion holdRot = p.rotation * Quaternion.Euler(0f, 0f, -90f + Mathf.Sin(t * 3f) * 3f);
            Quaternion diveRot = p.rotation * Quaternion.Euler(0f, 0f, 90f);
            prize.transform.rotation = Quaternion.Slerp(holdRot, diveRot, toBag);
            prize.transform.position = mouth - prize.transform.TransformVector(fishMouth);
        }

        // Elastic squash-and-stretch bounce on the straw backpack when the fish lands inside (t = 4.5s..5.5s)!
        if (gear && gear.bag && gear.hasBag)
        {
            float bagImpact = Mathf.Max(0f, t - 4.52f);
            float bounce = (bagImpact > 0f && bagImpact < 1.1f)
                ? Mathf.Sin(bagImpact * 16f) * Mathf.Exp(-bagImpact * 4.5f) * 0.18f
                : 0f;
            gear.bag.localScale = new Vector3(
                baseBagScale.x * (1f + bounce * 0.85f),
                baseBagScale.y * (1f - bounce * 0.55f),
                baseBagScale.z * (1f + bounce * 0.85f));
        }

        // Keep fishing line attached while admiring the fish, then release line as hand carries fish into the bag
        bool showLine = visible > 0.01f && toBag < 0.25f;
        line.enabled = showLine;
        Vector3 tip = gear.rodTip.position;
        const float lineSag = 0.72f;
        if (showLine)
        {
            for (int i = 0; i < 20; i++)
            {
                float u = i / 19f;
                line.SetPosition(i, Vector3.Lerp(tip, pinch, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * lineSag);
            }
            for (int i = 20; i < 25; i++)
                line.SetPosition(i, Vector3.Lerp(pinch, mouth, (i - 19) / 5f));
        }

        if (displayFloat)
        {
            displayFloat.SetActive(showLine);
            if (showLine)
            {
                displayFloat.transform.position = Vector3.Lerp(tip, pinch, 0.91f) + Vector3.down * Mathf.Sin(0.91f * Mathf.PI) * lineSag;
                Vector3 tangent = pinch - tip - Vector3.up * (Mathf.Cos(0.91f * Mathf.PI) * Mathf.PI * lineSag);
                displayFloat.transform.rotation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                displayFloat.transform.localScale = Vector3.one * 0.5f;
            }
        }

        // Orbit camera slightly toward the left-back when stowing the fish so the straw backpack is clearly visible!
        Vector3 focus = p.position + Vector3.up * 1.12f - p.right * 0.10f + Vector3.Lerp(p.forward * 0.10f, -p.forward * 0.14f, bagReach);
        float distance = 3.05f * Mathf.Max(1f, 1.55f / game.view.aspect);
        Vector3 closeDir = Vector3.Slerp(p.forward * 0.80f - p.right * 0.60f, p.forward * 0.25f - p.right * 0.92f, bagReach * 0.75f).normalized;
        Vector3 close = focus + closeDir * distance + Vector3.up * 0.42f;
        Quaternion closeRotation = Quaternion.LookRotation(focus - close);

        float enter = Ease(t / 1.35f), leave = Ease((t - 5.1f) / 1.4f);
        game.view.transform.position = Vector3.Lerp(Vector3.Lerp(cameraStart, close, enter), returnPosition, leave);
        game.view.transform.rotation = Quaternion.Slerp(Quaternion.Slerp(rotationStart, closeRotation, enter), returnRotation, leave);

        if (depthOfField)
        {
            var subject = p.position + Vector3.up * 1.48f + p.forward * 0.10f;
            float focusDistance = Mathf.Max(0.1f, Vector3.Dot(subject - game.view.transform.position, game.view.transform.forward));
            depthOfField.focusDistance.Override(focusDistance);
            depthOfField.gaussianStart.Override(focusDistance + 0.45f);
            depthOfField.gaussianEnd.Override(focusDistance + 2f);
        }

        if (shotVolume) shotVolume.weight = enter * (1f - leave);
        game.view.fieldOfView = fovStart;
        if (game.view.orthographic) game.view.orthographicSize = Mathf.Lerp(Mathf.Lerp(sizeStart, 1.24f, enter), sizeStart, leave);

        if (t >= duration)
        {
            active = false;
            if (gear && gear.bag) gear.bag.localScale = baseBagScale;
            if (shotVolume) shotVolume.weight = 0f;
            if (prize) prize.SetActive(false);
            if (line) line.enabled = false;
            if (displayFloat) displayFloat.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (gear && gear.bag) gear.bag.localScale = baseBagScale;
        if (shotVolume) shotVolume.weight = 0f;
        if (prize) prize.SetActive(false);
        if (line) line.enabled = false;
        if (displayFloat) displayFloat.SetActive(false);
        if (active && game && game.view)
        {
            game.view.transform.SetPositionAndRotation(returnPosition, returnRotation);
            game.view.fieldOfView = fovStart;
            game.view.orthographicSize = sizeStart;
        }
        active = false;
    }

    void OnDestroy()
    {
        if (prize) Destroy(prize);
        if (line) Destroy(line.gameObject);
        if (displayFloat) Destroy(displayFloat);
        foreach (var mesh in floatMeshes) if (mesh) Destroy(mesh);
        if (shotVolume) Destroy(shotVolume.gameObject);
        if (shotProfile) Destroy(shotProfile);
    }
}

