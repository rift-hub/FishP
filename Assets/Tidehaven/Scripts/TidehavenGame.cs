using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[DefaultExecutionOrder(150)]
public class TidehavenGame : MonoBehaviour
{
    public Transform player, visual;
    public Camera view;
    public LineRenderer fishingLine;
    public Transform bobber, rodTip;
    public List<Transform> fish = new List<Transform>();
    public bool ReelHeld { get; private set; }
    public int catches;
    public string phase = "Explore";
    public float progress, tension;

    // Fisch-style horizontal minigame state
    public float barPos = 0.5f;
    public float barVel;
    public float barWidth = 0.30f;
    public float fishPos = 0.5f;
    public float fishVel;
    public float fishTarget = 0.65f;
    public float fishNextMoveTime;
    public bool isCatchingZone;

    // Compatibility properties
    public const float TrackingTolerance = 0.10f;
    public bool TrackingFish => isCatchingZone;
    public float TargetPosition => fishPos;
    public float ArrowPosition => barPos;
    public string StatusMessage => phase == "Reeling" ? "Click & Hold Anywhere!" : (phase == "Bite" ? "FISH ON!" : "Cast with Left Click");
    public bool ShowingCatch => celebration && celebration.Playing;

    StrandedGear gear;
    CatchCelebration celebration;
    FishermanMotion fisherMotion;

    static readonly int BobberDataId = Shader.PropertyToID("_BobberData");
    static readonly int SandTrailPointsId = Shader.PropertyToID("_SandTrailPoints");
    static readonly int SandTrailCountId = Shader.PropertyToID("_SandTrailCount");

    IslandFootprints footprints;

    // Procedural UI textures for the Fisch-style minigame HUD
    Texture2D texWhite;
    Texture2D texFishLinePill;
    Texture2D texFishIcon;
    Texture2D texMouseIcon;
    Texture2D texArrowLeft;
    Texture2D texArrowRight;
    Texture2D texTriLeftCap;
    Texture2D texTriRightCap;
    GUIStyle promptStyle;
    GUIStyle promptShadowStyle;
    GUIStyle biteBangStyle;

    void Awake()
    {
        gear = GetComponent<StrandedGear>();
        celebration = GetComponent<CatchCelebration>();
        fisherMotion = GetComponent<FishermanMotion>();
    }

    const float WheelZoomStep = 0.12f;
    public float zoom = 1f;
    float zoomTarget = 1f;
    float initialOrthographicSize;

    public void SetZoom(float value) { zoomTarget = Mathf.Clamp(value, 0.7f, 2.2f); }

    void UpdateZoom(Mouse mouse)
    {
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                scroll /= 120f;
#endif
            if (!Mathf.Approximately(scroll, 0f)) SetZoom(zoomTarget - scroll * WheelZoomStep);
            if (mouse.middleButton.wasPressedThisFrame) SetZoom(1f);
        }
        zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-Time.deltaTime * 8f));
    }

    CharacterController controller;
    Vector3 cameraOffset = new Vector3(0f, 11.6f, -9.0f);
    Vector3 castPoint;
    float castStarted = -100f;
    Vector3 castRelease;
    public float CastElapsed => Time.time - castStarted;
    float biteAt, phaseTime;
    Vector3[] fishOrigins;
    string message = "Walk to the shore and cast into the shallows.";
    float messageUntil;
    Transform leftLeg, rightLeg, leftArm, rightArm;
    Quaternion ll, rl, la, ra;

    public static float Shore(float x)
    {
        return CoastalIslandLayout.Shore(x);
    }

    public static float Hill(float x, float z)
    {
        return (2.5f * Mathf.Exp(-((x + 12f) * (x + 12f) / 65f + (z - 12f) * (z - 12f) / 42f)) +
                3.1f * Mathf.Exp(-((x - 13f) * (x - 13f) / 48f + (z - 16f) * (z - 16f) / 55f))) *
               Mathf.SmoothStep(0f, 1f, (z - Shore(x) - 3f) / 5f);
    }

    public static float Ground(float x, float z)
    {
        return CoastalIslandLayout.Height(x, z);
    }

    void Start()
    {
        Application.runInBackground = true;
        zoom = Mathf.Clamp(zoom, 0.7f, 2.2f);
        zoomTarget = zoom;
        initialOrthographicSize = view.orthographicSize;
        controller = player.GetComponent<CharacterController>();


        fishOrigins = new Vector3[fish.Count];
        for (int i = 0; i < fish.Count; i++)
            if (fish[i]) fishOrigins[i] = fish[i].position;

        foreach (var t in visual.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("upperleg") || n.Contains("thigh"))
            {
                if (n.Contains("left") || n.EndsWith(".l")) leftLeg = t;
                else if (n.Contains("right") || n.EndsWith(".r")) rightLeg = t;
            }
            if (n.Contains("upperarm"))
            {
                if (n.Contains("left") || n.EndsWith(".l")) leftArm = t;
                else if (n.Contains("right") || n.EndsWith(".r")) rightArm = t;
            }
        }
        if (leftLeg) ll = leftLeg.localRotation;
        if (rightLeg) rl = rightLeg.localRotation;
        if (leftArm) la = leftArm.localRotation;
        if (rightArm) ra = rightArm.localRotation;

        InitSandPuffs();
        InitMinigameUI();
    }

    void InitMinigameUI()
    {
        texWhite = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var wCols = new Color[16];
        for (int i = 0; i < 16; i++) wCols[i] = Color.white;
        texWhite.SetPixels(wCols);
        texWhite.Apply();

        // 1. Vertical Fish Line Pill (dark slate-navy fill with crisp light slate-grey border)
        int pw = 28, ph = 112;
        texFishLinePill = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
        texFishLinePill.wrapMode = TextureWrapMode.Clamp;
        Color pillFill = new Color(0.16f, 0.20f, 0.27f, 0.98f);
        Color pillBorder = new Color(0.56f, 0.62f, 0.72f, 1.0f);
        float rad = pw * 0.44f;
        for (int y = 0; y < ph; y++)
        {
            for (int x = 0; x < pw; x++)
            {
                float cx = pw * 0.5f;
                float cy = Mathf.Clamp(y + 0.5f, rad + 1.5f, ph - rad - 1.5f);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - rad;
                float outerAlpha = Mathf.Clamp01(0.5f - d);
                float borderBlend = Mathf.Clamp01(d + 3.2f);
                Color c = Color.Lerp(pillFill, pillBorder, borderBlend);
                c.a *= outerAlpha;
                texFishLinePill.SetPixel(x, y, c);
            }
        }
        texFishLinePill.Apply();

        // 2. Diagonal Swimming Fish Silhouette Icon (tilted at +45 degrees like Fisch!)
        int fs = 96;
        texFishIcon = new Texture2D(fs, fs, TextureFormat.RGBA32, false);
        texFishIcon.wrapMode = TextureWrapMode.Clamp;
        Color fishFill = new Color(0.48f, 0.62f, 0.75f, 0.98f);
        Color fishHilite = new Color(0.65f, 0.78f, 0.90f, 1.0f);
        Color fishOutline = new Color(0.08f, 0.12f, 0.18f, 0.96f);

        float fishAngle = 45f * Mathf.Deg2Rad;
        float cosF = Mathf.Cos(fishAngle), sinF = Mathf.Sin(fishAngle);

        for (int y = 0; y < fs; y++)
        {
            for (int x = 0; x < fs; x++)
            {
                float nx = (x + 0.5f - fs * 0.5f) / (fs * 0.44f);
                float ny = (y + 0.5f - fs * 0.5f) / (fs * 0.44f);

                float u = nx * cosF + ny * sinF;
                float v = -nx * sinF + ny * cosF;
                v -= 0.04f * Mathf.Sin(u * 3.14159f);

                float dBody = 10f;
                if (u >= -0.45f && u <= 0.75f)
                {
                    float t = Mathf.InverseLerp(-0.45f, 0.75f, u);
                    float halfW = 0.26f * Mathf.Sin(Mathf.Pow(t, 0.65f) * Mathf.PI);
                    dBody = Mathf.Abs(v) - halfW;
                }

                float dTail = 10f;
                if (u >= -0.85f && u <= -0.35f)
                {
                    float tTail = Mathf.InverseLerp(-0.35f, -0.85f, u);
                    float tailSpread = Mathf.Lerp(0.05f, 0.32f, tTail);
                    float notch = Mathf.Lerp(0.0f, 0.12f, Mathf.Clamp01((tTail - 0.4f) / 0.6f));
                    float lat = Mathf.Abs(v);
                    dTail = Mathf.Max(lat - tailSpread, notch - lat);
                }

                float dDorsal = 10f;
                if (u >= -0.15f && u <= 0.40f && v > 0f)
                {
                    float tD = Mathf.InverseLerp(-0.15f, 0.40f, u);
                    float finH = 0.22f * Mathf.Sin(tD * Mathf.PI) + 0.05f;
                    dDorsal = v - (0.22f + finH * 0.4f);
                }

                float sd = Mathf.Min(dBody, dTail);
                sd = Mathf.Min(sd, dDorsal);
                float sdPx = sd * (fs * 0.44f);

                Color c = Color.clear;
                if (sdPx <= 0f)
                {
                    float tHi = Mathf.Clamp01((v + 0.15f) / 0.3f);
                    c = Color.Lerp(fishFill, fishHilite, tHi);
                }
                else if (sdPx <= 2.2f)
                {
                    float a = Mathf.Clamp01((2.2f - sdPx) / 0.9f);
                    c = fishOutline;
                    c.a *= a;
                }
                texFishIcon.SetPixel(x, y, c);
            }
        }
        texFishIcon.Apply();

        // 3. Mouse Click & Hold Icon (2-button mouse with bright white Left Button)
        int mw = 48, mh = 68;
        texMouseIcon = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
        texMouseIcon.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < mh; y++)
        {
            for (int x = 0; x < mw; x++)
            {
                float nx = (x + 0.5f - mw * 0.5f) / (mw * 0.42f);
                float ny = (y + 0.5f - mh * 0.5f) / (mh * 0.44f);
                // Rounded rectangle / capsule SDF
                float cy = Mathf.Clamp(ny, -0.38f, 0.38f);
                float dist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, cy)) - 0.88f;
                float alpha = Mathf.Clamp01((0.02f - dist) * 20f);

                bool topHalf = ny > 0.08f;
                bool leftBtn = topHalf && nx < -0.05f;
                bool gapLine = (Mathf.Abs(ny - 0.08f) < 0.045f) || (topHalf && Mathf.Abs(nx) < 0.055f);
                bool wheel = (ny > 0.16f && ny < 0.48f && Mathf.Abs(nx) < 0.11f);

                Color c = leftBtn ? new Color(0.98f, 0.99f, 1.0f, 0.98f) : new Color(0.76f, 0.80f, 0.84f, 0.88f);
                if (gapLine || wheel) c = new Color(0.15f, 0.18f, 0.20f, 0.90f);
                c.a *= alpha;
                texMouseIcon.SetPixel(x, y, c);
            }
        }
        texMouseIcon.Apply();

        // 4. Directional Arrows inside Control Bar (<- and ->)
        texArrowLeft = BuildArrowTexture(48, false);
        texArrowRight = BuildArrowTexture(48, true);

        // 5. End-cap triangles (► on left, ◄ on right)
        texTriLeftCap = BuildTriangleTexture(32, true);
        texTriRightCap = BuildTriangleTexture(32, false);
    }

    static Texture2D BuildArrowTexture(int s, bool pointRight)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s - 0.5f;
                float v = (y + 0.5f) / s - 0.5f;
                if (pointRight) u = -u;
                // Left-pointing arrow: tip at u = -0.34, shaft to u = +0.34
                float dShaft = (u > -0.22f && u < 0.34f) ? Mathf.Abs(v) - 0.075f : 1f;
                // Chevron wings from (-0.34, 0) to (0.02, +-0.30)
                float tWing = Mathf.Clamp01((u - (-0.34f)) / 0.34f);
                float wingY = tWing * 0.28f;
                float dChevron = (u >= -0.36f && u <= 0.02f) ? Mathf.Abs(Mathf.Abs(v) - wingY) - 0.075f : 1f;
                float d = Mathf.Min(dShaft, dChevron);
                float a = Mathf.Clamp01((0.02f - d) * 28f);
                tex.SetPixel(x, y, new Color(0.42f, 0.45f, 0.47f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D BuildTriangleTexture(int s, bool pointRight)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color fill = new Color(0.42f, 0.18f, 0.17f, 0.95f);
        Color border = new Color(0.62f, 0.28f, 0.26f, 1.0f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s;
                float v = Mathf.Abs((y + 0.5f) / s - 0.5f) * 2f;
                if (!pointRight) u = 1f - u;
                // Triangle from u=0.15 (base) to u=0.85 (tip)
                float t = Mathf.InverseLerp(0.85f, 0.15f, u);
                bool inside = (u >= 0.15f && u <= 0.85f && v <= t * 0.82f);
                bool edge = inside && (u < 0.23f || Mathf.Abs(v - t * 0.82f) < 0.12f);
                Color c = edge ? border : fill;
                c.a = inside ? c.a : 0f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    void InitSandPuffs()
    {
        footprints = gameObject.AddComponent<IslandFootprints>();
        footprints.Initialize(player.position);
    }
    void UpdateSandTrail(float dt)
    {
        if (footprints) footprints.Tick(player);
    }
    public bool CastAt(Vector3 p)
    {
        if (celebration && celebration.Playing) return false;
        if ((gear && (!gear.Ready || gear.Busy || !gear.rodEquipped)) || phase != "Explore" || LighthouseIslandShape.Edge(p.x, p.z) > -0.3f || Vector3.Distance(player.position, p) > 16f)
            return false;
        castPoint = new Vector3(p.x, 0.08f, p.z);
        castRelease = rodTip.position;
        bobber.position = rodTip.position + Vector3.down * 0.55f;
        bobber.gameObject.SetActive(true);
        fishingLine.enabled = true;
        phase = "Waiting";
        castStarted = Time.time;
        fisherMotion?.PlayAction("Cast");
        biteAt = Time.time + 2f + Random.Range(2.0f, 4.2f);
        phaseTime = Time.time;
        message = "Watch the float. Click when a fish bites.";
        player.rotation = Quaternion.LookRotation(new Vector3(p.x - player.position.x, 0f, p.z - player.position.z));
        return true;
    }

    public void EquipmentRecovered()
    {
        message = "Belongings recovered! Aim at the water and press E to cast.";
        messageUntil = Time.time + 5f;
    }

    public bool Hook()
    {
        if (phase != "Bite") return false;
        phase = "Reeling";
        progress = 0.28f;
        tension = 0.20f;
        barWidth = 0.30f;
        barPos = 0.50f;
        barVel = 0f;
        fishPos = 0.50f;
        fishVel = 0f;
        fishTarget = Random.value > 0.5f ? 0.72f : 0.28f;
        fishNextMoveTime = Time.time + 0.65f;
        isCatchingZone = true;
        message = "Click & Hold to keep the Control Bar on the Fish Line!";
        return true;
    }

    public void ReelStep(bool held, float dt)
    {
        ReelHeld = held;
        if (phase != "Reeling") return;
        dt = Mathf.Clamp(dt, 0f, 0.05f);

        // 1. Control Bar Physics (Hold Click -> accelerates Right; Release -> accelerates Left)
        float accel = held ? 1.75f : -1.75f;
        barVel += accel * dt;
        barVel *= Mathf.Exp(-3.0f * dt);
        barPos += barVel * dt;

        float minBar = barWidth * 0.5f;
        float maxBar = 1f - barWidth * 0.5f;
        if (barPos < minBar)
        {
            barPos = minBar;
            if (barVel < 0f) barVel *= -0.32f;
        }
        else if (barPos > maxBar)
        {
            barPos = maxBar;
            if (barVel > 0f) barVel *= -0.32f;
        }

        // 2. Fish Line AI Movement (Darts and glides left & right along the track)
        if (Time.time >= fishNextMoveTime)
        {
            float shift = Random.Range(0.18f, 0.45f) * (Random.value > 0.5f ? 1f : -1f);
            fishTarget = Mathf.Clamp(fishPos + shift, 0.08f, 0.92f);
            if (Mathf.Abs(fishTarget - fishPos) < 0.14f)
                fishTarget = fishPos < 0.5f ? Random.Range(0.58f, 0.88f) : Random.Range(0.12f, 0.42f);
            fishNextMoveTime = Time.time + Random.Range(0.65f, 1.45f);
        }
        fishPos = Mathf.SmoothDamp(fishPos, fishTarget, ref fishVel, 0.36f, 0.78f, dt);

        // 3. Progress Bar Fill / Drain based on whether Fish Line is inside Control Bar
        float dist = Mathf.Abs(fishPos - barPos);
        isCatchingZone = dist <= (barWidth * 0.5f + 0.015f);

        if (isCatchingZone)
        {
            progress = Mathf.Clamp01(progress + dt * 0.22f);
            tension = Mathf.MoveTowards(tension, 0.35f, dt * 0.5f);
        }
        else
        {
            progress = Mathf.Clamp01(progress - dt * 0.14f);
            tension = Mathf.MoveTowards(tension, 0.85f, dt * 0.5f);
        }

        if (progress <= 0f)
        {
            Cancel("The fish slipped away! Keep the Fish Line inside the Control Bar.");
        }
        else if (progress >= 1f)
        {
            catches++;
            Cancel("Caught a reef fish!  +1 to your catch");
        }
    }

    void Cancel(string text)
    {
        ReelHeld = false;
        fisherMotion?.ClearAction();
        phase = "Explore";
        progress = tension = 0f;
        fishingLine.enabled = false;
        bobber.gameObject.SetActive(false);
        message = text;
        messageUntil = Time.time + 4f;
    }

    void Update()
    {
        if (!player || !view) return;
        if (celebration && celebration.Playing) return;
        var k = Keyboard.current;
        var m = Mouse.current;
        UpdateZoom(m);
        float dt = Time.deltaTime;
        bool action = k != null && k.eKey.wasPressedThisFrame;

        if (phase == "Explore")
        {
            Vector2 input = Vector2.zero;
            if (k != null)
            {
                input.x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                input.y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            }
            if (gear && gear.Busy) input = Vector2.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();
            MovePlayer(input, dt);
            if (input.sqrMagnitude > 0.01f)
                player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(new Vector3(input.x, 0f, input.y)), 1f - Mathf.Exp(-dt * 16f));

            if (action && !(gear && (gear.Busy || gear.TryPickup())))
            {
                Vector3 aim = player.position + Vector3.back * 8f;
                if (m != null)
                {
                    Ray ray = view.ScreenPointToRay(m.position.ReadValue());
                    if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t))
                        aim = ray.GetPoint(t);
                }
                if (!CastAt(aim))
                {
                    message = (gear && !gear.Ready)
                        ? "Recover both your bag and rod before fishing."
                        : "Draw your rod with R, then aim at nearby water and press E.";
                    messageUntil = Time.time + 4f;
                }
            }
        }
        else
        {
            if (action && phase != "Bite")
                Cancel("Line reeled in. Find another fishing spot.");
            else if (phase == "Waiting" && Time.time >= biteAt)
            {
                phase = "Bite";
                phaseTime = Time.time;
                message = "BITE! Click to hook the fish!";
            }
            else if (phase == "Bite")
            {
                if ((m != null && m.leftButton.wasPressedThisFrame) || action)
                    Hook();
                else if (Time.time - phaseTime > 3.2f)
                    Cancel("The fish slipped away. Try another cast.");
            }
            else if (phase == "Reeling")
            {
                bool held = (m != null && m.leftButton.isPressed) || (k != null && k.spaceKey.isPressed);
                ReelStep(held, dt);
            }
        }

        UpdateSandTrail(dt);

        if (phase != "Explore")
        {
            float bobSway = phase == "Bite" ? Mathf.Sin(Time.time * 24f) * 0.13f : Mathf.Sin(Time.time * 3f) * 0.025f;
            Vector3 lateralFight = phase == "Reeling" ? new Vector3((fishPos - 0.5f) * 0.85f, 0f, Mathf.Sin(Time.time * 6f) * 0.12f) : Vector3.zero;
            bobber.position = castPoint + lateralFight + Vector3.up * bobSway;
        }

        float fTime = Time.time * 0.23f;
        if (fishOrigins == null || fishOrigins.Length != fish.Count)
        {
            fishOrigins = new Vector3[fish.Count];
            for (int i = 0; i < fish.Count; i++)
                if (fish[i]) fishOrigins[i] = fish[i].position;
        }
        for (int i = 0; i < fish.Count; i++)
        {
            var f = fish[i];
            if (!f) continue;
            float a = fTime + i * 1.7f;
            float sinA = Mathf.Sin(a), cosA = Mathf.Cos(a);
            Vector3 p = fishOrigins[i] + new Vector3(sinA * 1.8f, Mathf.Sin(a * 2f) * 0.06f, cosA * 0.7f);
            // Keep schools in the lagoon and above the seabed around the irregular coast.
            float edge = LighthouseIslandShape.Edge(p.x, p.z);
            if (edge > -2.2f)
            {
                float angle = Mathf.Atan2(p.z / .84f, p.x);
                float radius = LighthouseIslandShape.Radius(angle) + 2.2f;
                p.x = Mathf.Cos(angle) * radius;
                p.z = Mathf.Sin(angle) * radius * .84f;
            }
            p.y = Mathf.Clamp(p.y, Ground(p.x, p.z) + .22f, -.32f);
            f.position = p;
            f.rotation = Quaternion.LookRotation(new Vector3(cosA, 0f, -sinA * 0.4f)) * Quaternion.Euler(0f, 90f, 0f);
        }
    }

    // Substeps keep collision and ground sampling stable through long Editor frames.
    public void MovePlayer(Vector2 input, float dt)
    {
        if (!controller || !controller.enabled) return;
        input = Vector2.ClampMagnitude(input, 1f);
        float elapsed = Mathf.Min(dt, .1f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed / (1f / 60f)));
        Vector3 delta = new Vector3(input.x, 0, input.y) * (2.15f * elapsed / steps);
        for (int i = 0; i < steps; i++)
        {
            Vector3 next = player.position + delta;
            next.x = Mathf.Clamp(next.x, CoastalIslandLayout.MinX, CoastalIslandLayout.MaxX);
            next.z = Mathf.Clamp(next.z, CoastalIslandLayout.MinZ, CoastalIslandLayout.MaxZ);
            if (LighthouseIslandShape.Edge(next.x, next.z) < 0.25f) break;
            float floor = CoastalWalkableSurface.Height(next.x, next.z, player.position.y + controller.stepOffset + .05f);
            // Steep terrace faces must be climbed using the ramp, not the height snap.
            if (floor > player.position.y + controller.stepOffset + .05f) break;
            Vector3 motion = next - player.position;
            motion.y = floor - player.position.y - .015f;
            controller.Move(motion);
            Vector3 actual = player.position;
            float resolved = CoastalWalkableSurface.Height(actual.x, actual.z, actual.y + controller.stepOffset + .05f);
            if (actual.y < resolved - .005f)
                controller.Move(Vector3.up * (resolved - actual.y));
        }
    }

    void LateUpdate()
    {
        if (!player || !view) return;
        if (celebration && celebration.Playing) return;

        if (phase != "Explore")
        {
            float flight = Mathf.Clamp01((CastElapsed - 0.70f) / 0.85f);
            if (CastElapsed < 0.70f)
            {
                castRelease = rodTip.position;
                bobber.position = rodTip.position + Vector3.down * 0.55f;
            }
            else if (flight < 1f)
            {
                bobber.position = Vector3.Lerp(castRelease, castPoint, flight) + Vector3.up * Mathf.Sin(flight * Mathf.PI) * 1.8f;
            }
            Vector3 start = rodTip.position;
            float sag = phase == "Reeling" ? (isCatchingZone ? 0.14f : 0.06f) : 0.35f;
            for (int i = 0; i < 24; i++)
            {
                float t = i / 23f;
                fishingLine.SetPosition(i, Vector3.Lerp(start, bobber.position, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag);
            }
            Shader.SetGlobalVector(BobberDataId, new Vector4(bobber.position.x, bobber.position.z, (phase == "Bite" || phase == "Reeling") ? 2.2f : 1.0f, flight >= 0.98f ? 1.0f : 0.0f));
        }
        else
        {
            Shader.SetGlobalVector(BobberDataId, Vector4.zero);
        }

        if (view.orthographic) view.orthographicSize = initialOrthographicSize * zoom;
        float shoreProximity = Mathf.Clamp01(1f - (player.position.z - Shore(player.position.x) - 1.5f)/ 4.2f);
        Vector3 focus = player.position + Vector3.up * 0.8f + Vector3.back * (shoreProximity * 1.75f);
        if (phase != "Explore") focus = Vector3.Lerp(focus, castPoint, 0.35f);
        view.transform.position = Vector3.Lerp(view.transform.position, focus + cameraOffset * zoom, 1f - Mathf.Exp(-Time.deltaTime * 10f));
        view.transform.rotation = Quaternion.LookRotation(-cameraOffset);
    }

    void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, texWhite);
        GUI.color = prev;
    }

    void DrawBorderedRect(Rect r, Color fill, Color border, float borderPx)
    {
        DrawRect(new Rect(r.x - borderPx, r.y - borderPx, r.width + borderPx * 2f, r.height + borderPx * 2f), border);
        DrawRect(r, fill);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (celebration && celebration.Playing) return;
        if (!texWhite) InitMinigameUI();

        float uiScale = Mathf.Clamp(Screen.height / 720f, 0.75f, 2.2f);

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.97f, 0.99f, 0.96f) }
            };
            promptShadowStyle = new GUIStyle(promptStyle)
            {
                normal = { textColor = new Color(0.05f, 0.07f, 0.09f, 0.85f) }
            };
            biteBangStyle = new GUIStyle(promptStyle)
            {
                normal = { textColor = new Color(1.0f, 0.92f, 0.32f, 1.0f) }
            };
        }

        // Bite exclamation indicator above bobber when a fish bites!
        if (phase == "Bite" && view && bobber)
        {
            Vector3 sp = view.WorldToScreenPoint(bobber.position + Vector3.up * 0.65f);
            if (sp.z > 0f)
            {
                float guiY = Screen.height - sp.y - Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 10f * uiScale;
                biteBangStyle.fontSize = Mathf.RoundToInt(28f * uiScale);
                promptShadowStyle.fontSize = biteBangStyle.fontSize;
                Rect br = new Rect(sp.x - 100f * uiScale, guiY - 25f * uiScale, 200f * uiScale, 50f * uiScale);
                GUI.Label(new Rect(br.x + 2f, br.y + 2f, br.width, br.height), "! CLICK !", promptShadowStyle);
                GUI.Label(br, "! CLICK !", biteBangStyle);
            }
            return;
        }

        if (phase != "Reeling") return;

        // -------------------------------------------------------------------------
        // FISCH-STYLE HORIZONTAL REELING MINIGAME HUD (Matches Reference Image 1:1)
        // -------------------------------------------------------------------------
        float cx = Screen.width * 0.5f;
        float trackY = Screen.height * 0.77f;
        float trackW = 540f * uiScale;
        float trackH = 32f * uiScale;
        Rect trackRect = new Rect(cx - trackW * 0.5f, trackY - trackH * 0.5f, trackW, trackH);

        // 1. Top Mouse Icon & "Click & Hold Anywhere!" Prompt
        float mouseW = 22f * uiScale;
        float mouseH = 31f * uiScale;
        float bobPrompt = Mathf.Sin(Time.time * 5.5f) * 2.0f * uiScale;
        Rect mouseRect = new Rect(cx - mouseW * 0.5f, trackRect.y - 92f * uiScale + bobPrompt, mouseW, mouseH);
        Color prevCol = GUI.color;
        GUI.color = ReelHeld ? new Color(1f, 1f, 1f, 1f) : new Color(0.90f, 0.93f, 0.96f, 0.94f);
        GUI.DrawTexture(mouseRect, texMouseIcon);
        GUI.color = prevCol;

        promptStyle.fontSize = Mathf.RoundToInt(15.5f * uiScale);
        promptShadowStyle.fontSize = promptStyle.fontSize;
        Rect textRect = new Rect(cx - 160f * uiScale, trackRect.y - 62f * uiScale + bobPrompt, 320f * uiScale, 26f * uiScale);
        GUI.Label(new Rect(textRect.x + 1.5f, textRect.y + 1.5f, textRect.width, textRect.height), "Click & Hold Anywhere!", promptShadowStyle);
        GUI.Label(textRect, "Click & Hold Anywhere!", promptStyle);

        // 2. Left & Right Inward-Pointing Dark-Red Triangle End-Caps (► and ◄)
        float capSize = 15f * uiScale;
        GUI.DrawTexture(new Rect(trackRect.x - capSize - 6f * uiScale, trackY - capSize * 0.5f, capSize, capSize), texTriLeftCap);
        GUI.DrawTexture(new Rect(trackRect.xMax + 6f * uiScale, trackY - capSize * 0.5f, capSize, capSize), texTriRightCap);

        // 3. Main Horizontal Dark Track with Subtle Outer Border
        DrawBorderedRect(
            trackRect,
            new Color(0.065f, 0.075f, 0.080f, 0.95f),
            new Color(0.24f, 0.21f, 0.19f, 0.92f),
            2.0f * uiScale);

        // 4. White Player "Control Bar" inside the Track + Directional Arrow (← or →)
        float cBarW = trackW * barWidth;
        float cBarCenterX = trackRect.x + Mathf.Clamp(barPos, barWidth * 0.5f, 1f - barWidth * 0.5f) * trackW;
        float pad = 1.5f * uiScale;
        Rect controlBarRect = new Rect(cBarCenterX - cBarW * 0.5f, trackRect.y + pad, cBarW, trackH - pad * 2f);
        Color barCol = isCatchingZone
            ? new Color(0.95f, 0.96f, 0.97f, 0.99f)
            : new Color(0.84f, 0.86f, 0.88f, 0.92f);
        DrawRect(controlBarRect, barCol);

        // Directional arrow inside Control Bar (← on left when releasing/moving left, → on right when holding/moving right)
        float arrowSize = 21f * uiScale;
        bool movingRight = ReelHeld || barVel > 0.05f;
        if (!movingRight)
        {
            Rect arrowLRect = new Rect(controlBarRect.x + 12f * uiScale, trackY - arrowSize * 0.5f, arrowSize, arrowSize);
            GUI.DrawTexture(arrowLRect, texArrowLeft);
        }
        else
        {
            Rect arrowRRect = new Rect(controlBarRect.xMax - arrowSize - 12f * uiScale, trackY - arrowSize * 0.5f, arrowSize, arrowSize);
            GUI.DrawTexture(arrowRRect, texArrowRight);
        }

        // 5. Vertical "Fish Line" Pill + Diagonal Swimming Fish Silhouette Above It
        float fishX = trackRect.x + Mathf.Clamp(fishPos, 0.03f, 0.97f) * trackW;
        float pillW = 12f * uiScale;
        float pillH = 48f * uiScale;
        Rect pillRect = new Rect(fishX - pillW * 0.5f, trackY - pillH * 0.5f, pillW, pillH);
        GUI.DrawTexture(pillRect, texFishLinePill);

        // Diagonal Fish Icon right above the Fish Line pill (wiggling gently as it swims!)
        float fishIconSize = 46f * uiScale;
        Vector2 fishIconCenter = new Vector2(fishX, pillRect.y - fishIconSize * 0.38f);
        Matrix4x4 oldMat = GUI.matrix;
        float swimWiggle = Mathf.Sin(Time.time * 9.5f) * 6.5f - fishVel * 18f;
        GUIUtility.RotateAroundPivot(swimWiggle, fishIconCenter);
        GUI.DrawTexture(new Rect(fishIconCenter.x - fishIconSize * 0.5f, fishIconCenter.y - fishIconSize * 0.5f, fishIconSize, fishIconSize), texFishIcon);
        GUI.matrix = oldMat;

        // 6. Lower "Progress Bar" Centered Below the Main Track
        float progW = 290f * uiScale;
        float progH = 9.5f * uiScale;
        float progY = trackRect.yMax + 18f * uiScale;
        Rect progBgRect = new Rect(cx - progW * 0.5f, progY, progW, progH);
        DrawBorderedRect(
            progBgRect,
            new Color(0.07f, 0.08f, 0.09f, 0.95f),
            new Color(0.48f, 0.49f, 0.47f, 0.92f),
            1.5f * uiScale);

        float fillW = Mathf.Clamp01(progress) * progW;
        if (fillW > 1f)
        {
            DrawRect(new Rect(progBgRect.x, progBgRect.y, fillW, progH), new Color(0.95f, 0.97f, 0.98f, 0.99f));
        }
    }
}



