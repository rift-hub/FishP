using UnityEngine;
using TMPro;

/// <summary>Responsive fishing overlay; all gameplay input is handled by TidehavenGame.</summary>
[DefaultExecutionOrder(300)]
public sealed class FishingFollowHUD : MonoBehaviour
{
    TidehavenGame game;
    Canvas canvas;
    GameObject root, fishingPanel, promptPanel;
    RectTransform track, target, targetZone, arrow, progressFill, escapeFill;
    TMPro.TextMeshProUGUI title, instruction, progressLabel, prompt, catchCount;
    UnityEngine.UI.Image zoneImage;
    FishingArrowGraphic arrowGraphic;
    TMP_FontAsset font;

    static readonly Color Ink = new Color(0.06f, 0.16f, 0.17f, 0.94f);
    static readonly Color Cream = new Color(0.98f, 0.96f, 0.84f);
    static readonly Color Mint = new Color(0.45f, 0.89f, 0.72f);
    static readonly Color Gold = new Color(1f, 0.79f, 0.36f);
    static readonly Color Muted = new Color(0.69f, 0.79f, 0.75f);

    void Awake()
    {
        game = GetComponent<TidehavenGame>();
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (!font) font = TMP_Settings.defaultFontAsset;
        if (!font)
        {
            Debug.LogError("Fishing HUD needs a bundled TextMeshPro font. Import TMP Essential Resources or assign the TMP Settings default font; operating-system fonts are not a portable substitute.", this);
            enabled = false;
            return;
        }

        root = new GameObject("Fishing HUD", typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel = Box("Fish tracking panel", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 44f), new Vector2(620f, 232f), Ink);
        fishingPanel = panel.gameObject;
        Box("Header rule", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -16f), new Vector2(36f, 3f), Gold);
        title = Label("Follow target title", panel, new Vector2(28f, -30f), new Vector2(564f, 28f), 20, Cream);
        title.text = "FOLLOW THE WHITE LINE";
        instruction = Label("Alignment state", panel, new Vector2(28f, -62f), new Vector2(564f, 22f), 14, Muted);

        track = Box("Moving target track", panel, Vector2.zero, Vector2.zero, new Vector2(42f, 100f), new Vector2(536f, 44f), new Color(0.12f, 0.27f, 0.27f));
        for (int i = 1; i < 10; i++)
            Box("Track tick " + i, track, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(i * 53.6f, 0f), new Vector2(1f, 7f), new Color(0.53f, 0.72f, 0.67f, 0.25f));
        targetZone = Box("Alignment band", track, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero,
            new Vector2(536f * TidehavenGame.TrackingTolerance * 2f, 44f), new Color(Mint.r, Mint.g, Mint.b, 0.21f));
        zoneImage = targetZone.GetComponent<UnityEngine.UI.Image>();
        target = Box("Moving white line", track, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 44f), Color.white);
        Box("White line cap", target, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 4f), Color.white);

        var arrowObject = new GameObject("Player arrow", typeof(RectTransform), typeof(CanvasRenderer));
        arrow = arrowObject.GetComponent<RectTransform>();
        arrow.SetParent(track, false);
        SetRect(arrow, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(22f, 17f));
        arrowGraphic = arrowObject.AddComponent<FishingArrowGraphic>();
        arrowGraphic.raycastTarget = false;
        arrowGraphic.color = Gold;

        progressLabel = Label("Catch progress label", panel, new Vector2(42f, -163f), new Vector2(536f, 22f), 13, Cream);
        var progressTrack = Box("Catch progress", panel, Vector2.zero, Vector2.zero, new Vector2(42f, 38f), new Vector2(536f, 8f), new Color(0.19f, 0.32f, 0.31f));
        progressFill = Box("Filled catch progress", progressTrack, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 8f), Mint);
        var escapeTrack = Box("Hook strain", panel, Vector2.zero, Vector2.zero, new Vector2(42f, 31f), new Vector2(536f, 2f), new Color(0.19f, 0.32f, 0.31f));
        escapeFill = Box("Hook strain fill", escapeTrack, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 2f), new Color(0.97f, 0.49f, 0.34f));
        var controls = Label("Tracking controls", panel, new Vector2(42f, -207f), new Vector2(536f, 18f), 12, Muted);
        controls.text = "A / D  or  left / right  ·  Move mouse to steer  ·  E / Esc  Cancel";

        var promptRect = Box("Fishing prompt", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 25f), new Vector2(710f, 40f), new Color(Ink.r, Ink.g, Ink.b, 0.83f));
        promptPanel = promptRect.gameObject;
        prompt = Label("Context prompt", promptRect, new Vector2(18f, -8f), new Vector2(674f, 25f), 14, Cream);
        prompt.alignment = TextAlignmentOptions.Center;
        var countRect = Box("Catch counter", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -24f), new Vector2(156f, 38f), new Color(Ink.r, Ink.g, Ink.b, 0.76f));
        catchCount = Label("Caught fish count", countRect, new Vector2(12f, -8f), new Vector2(132f, 22f), 13, Cream);
        catchCount.alignment = TextAlignmentOptions.Center;
        root.SetActive(true);
    }

    void LateUpdate()
    {
        if (!game || !canvas) return;
        canvas.enabled = game.enabled && !game.ShowingCatch;
        bool reeling = game.phase == "Reeling";
        fishingPanel.SetActive(reeling);
        promptPanel.SetActive(!reeling);
        catchCount.text = "REEF FISH   " + game.catches.ToString("00");
        prompt.text = game.StatusMessage;
        if (!reeling) return;

        float width = track.rect.width;
        target.anchoredPosition = new Vector2(game.TargetPosition * width, 0f);
        targetZone.anchoredPosition = target.anchoredPosition;
        arrow.anchoredPosition = new Vector2(game.ArrowPosition * width, -4f);
        progressFill.sizeDelta = new Vector2(width * Mathf.Clamp01(game.progress), 8f);
        escapeFill.sizeDelta = new Vector2(width * Mathf.Clamp01(game.tension), 2f);
        zoneImage.color = new Color(Mint.r, Mint.g, Mint.b, game.TrackingFish ? 0.29f : 0.13f);
        arrowGraphic.color = game.TrackingFish ? Mint : Gold;
        instruction.text = game.TrackingFish ? "Good! Keep the arrow beneath the moving line." : "Follow the white line — the fish is pulling away.";
        instruction.color = game.TrackingFish ? Mint : Gold;
        progressLabel.text = "CATCH PROGRESS   " + Mathf.RoundToInt(game.progress * 100f) + "%" +
            (game.tension > 0.68f ? "                         LOSING THE FISH!" : "");
    }

    public bool TryGetTrackPosition(Vector2 screenPosition, out float normalizedPosition)
    {
        normalizedPosition = 0.5f;
        if (!track || !RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screenPosition, null, out Vector2 local)) return false;
        normalizedPosition = Mathf.Clamp01((local.x - track.rect.xMin) / track.rect.width);
        return true;
    }

    RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, anchor, pivot, position, size);
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    TMPro.TextMeshProUGUI Label(string name, Transform parent, Vector2 position, Vector2 size, float pointSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        var text = go.AddComponent<TMPro.TextMeshProUGUI>();
        text.font = font;
        text.fontSize = pointSize;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void OnDestroy()
    {
        if (root) Destroy(root);
    }
}

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class FishingArrowGraphic : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        vh.AddVert(new Vector3(r.xMin, r.yMin, 0f), color, Vector2.zero);
        vh.AddVert(new Vector3(r.center.x, r.yMax, 0f), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin, 0f), color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
    }
}
