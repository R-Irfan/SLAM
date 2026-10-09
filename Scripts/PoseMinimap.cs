using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Advanced on-screen Pose Minimap (Centered)
/// - Top-Down & First-Person modes
/// - Zoom controls
/// - Border + Title
/// </summary>
public class PoseMinimap : MonoBehaviour
{
    public enum MinimapMode { TopDown, FirstPerson }

    [Header("References")]
    public PoseGraph poseGraph;
    public Transform arCamera;

    [Header("Minimap Settings")]
    public Vector2 minimapSize = new Vector2(340, 340);          // Size of the minimap
    public Color backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
    public Color borderColor = new Color(0.3f, 0.7f, 1f, 0.9f);
    public Color titleColor = Color.white;
    public Color trajectoryColor = new Color(0.3f, 0.75f, 1f);
    public Color keyframeColor = new Color(0.2f, 0.95f, 0.4f);
    public Color currentColor = new Color(1f, 0.85f, 0.15f);
    public Color loopColor = new Color(1f, 0.35f, 0.35f);

    [Header("Mode & Zoom")]
    public MinimapMode mode = MinimapMode.TopDown;
    public float zoom = 1.0f;
    public float minZoom = 0.6f;
    public float maxZoom = 4.0f;
    public float zoomStep = 0.25f;

    [Header("Visibility")]
    public bool showMinimap = true;

    // Internal
    private Canvas canvas;
    private RectTransform panelRT;
    private RawImage mapImage;
    private Text titleText;
    private Texture2D texture;
    private Color32[] pixels;
    private int texSize = 512;

    private Vector3 mapMin, mapMax;
    private bool hasBounds = false;

    void Start()
    {
        if (poseGraph == null) poseGraph = FindObjectOfType<PoseGraph>();
        if (arCamera == null) arCamera = Camera.main?.transform;

        CreateUI();

        if (poseGraph != null)
            poseGraph.OnGraphChanged += () => RecalculateBounds();
    }

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    void Update()
    {
        if (!showMinimap || poseGraph == null) return;
        DrawMinimap();
    }

    // ------------------------------------------------------------------
    // UI Creation - CENTERED
    // ------------------------------------------------------------------

    private void CreateUI()
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("MinimapCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
        }

        // Main panel - CENTER of screen
        GameObject panel = new GameObject("PoseMinimapPanel");
        panel.transform.SetParent(canvas.transform, false);

        panelRT = panel.AddComponent<RectTransform>();

        // === CENTER ANCHOR ===
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;          // exactly center
        panelRT.sizeDelta = minimapSize;

        Image bg = panel.AddComponent<Image>();
        bg.color = backgroundColor;

        // Border
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2.5f, 2.5f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -8);
        titleRT.sizeDelta = new Vector2(0, 30);

        titleText = titleGO.AddComponent<Text>();
        titleText.text = "Pose Map  •  Top-Down";
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = titleColor;
        titleText.alignment = TextAnchor.MiddleCenter;

        // Map image
        GameObject imgGO = new GameObject("Map");
        imgGO.transform.SetParent(panel.transform, false);
        RectTransform imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = new Vector2(12, 48);
        imgRT.offsetMax = new Vector2(-12, -40);

        mapImage = imgGO.AddComponent<RawImage>();
        texture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        pixels = new Color32[texSize * texSize];
        mapImage.texture = texture;

        // Buttons (bottom of the panel)
        CreateButton(panel.transform, "+", new Vector2(40, 12), OnZoomIn);
        CreateButton(panel.transform, "−", new Vector2(0, 12), OnZoomOut);
        CreateButton(panel.transform, "Mode", new Vector2(-50, 12), OnToggleMode);
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(48, 32);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.4f, 0.7f, 0.95f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 16;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    // ------------------------------------------------------------------
    // Drawing (same as before)
    // ------------------------------------------------------------------

    private void RecalculateBounds()
    {
        if (poseGraph.nodes.Count == 0) { hasBounds = false; return; }

        mapMin = mapMax = poseGraph.nodes[0].pose.position;
        foreach (var n in poseGraph.nodes)
        {
            mapMin = Vector3.Min(mapMin, n.pose.position);
            mapMax = Vector3.Max(mapMax, n.pose.position);
        }
        if (arCamera != null)
        {
            mapMin = Vector3.Min(mapMin, arCamera.position);
            mapMax = Vector3.Max(mapMax, arCamera.position);
        }

        Vector3 size = mapMax - mapMin;
        float maxDim = Mathf.Max(size.x, size.z, 0.5f);
        Vector3 pad = Vector3.one * maxDim * 0.2f;
        mapMin -= pad;
        mapMax += pad;
        hasBounds = true;
    }

    private void DrawMinimap()
    {
        Color32 clear = backgroundColor;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        if (!hasBounds || poseGraph.nodes.Count == 0)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            return;
        }

        if (mode == MinimapMode.TopDown)
            DrawTopDown();
        else
            DrawFirstPerson();

        texture.SetPixels32(pixels);
        texture.Apply();
    }

    private void DrawTopDown()
    {
        for (int i = 1; i < poseGraph.nodes.Count; i++)
        {
            Vector2 a = WorldToMap(poseGraph.nodes[i - 1].pose.position);
            Vector2 b = WorldToMap(poseGraph.nodes[i].pose.position);
            DrawLine(a, b, trajectoryColor, 2.5f);
        }

        foreach (var e in poseGraph.edges)
        {
            if (!e.isLoopClosure) continue;
            var na = poseGraph.nodes.Find(n => n.id == e.fromId);
            var nb = poseGraph.nodes.Find(n => n.id == e.toId);
            if (na == null || nb == null) continue;
            DrawLine(WorldToMap(na.pose.position), WorldToMap(nb.pose.position), loopColor, 3.5f);
        }

        for (int i = 0; i < poseGraph.nodes.Count; i++)
        {
            Vector2 p = WorldToMap(poseGraph.nodes[i].pose.position);
            bool last = i == poseGraph.nodes.Count - 1;
            FillCircle(p, last ? 5f : 3.5f, last ? currentColor : keyframeColor);
        }

        if (arCamera != null)
        {
            Vector2 cur = WorldToMap(arCamera.position);
            FillCircle(cur, 7f, currentColor);

            Vector3 fwd = arCamera.forward;
            Vector2 dir = new Vector2(fwd.x, fwd.z).normalized * 14f;
            DrawLine(cur, cur + dir, currentColor, 2.8f);
        }
    }

    private void DrawFirstPerson()
    {
        if (arCamera == null) return;

        Vector3 origin = arCamera.position;
        Vector3 forward = arCamera.forward;
        forward.y = 0;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float range = 8f / zoom;

        foreach (var n in poseGraph.nodes)
        {
            Vector3 rel = n.pose.position - origin;
            float x = Vector3.Dot(rel, right);
            float z = Vector3.Dot(rel, forward);

            if (Mathf.Abs(x) > range || Mathf.Abs(z) > range) continue;

            float u = (x / range + 1f) * 0.5f;
            float v = (z / range + 1f) * 0.5f;
            Vector2 p = new Vector2(u * (texSize - 1), v * (texSize - 1));
            FillCircle(p, 4f, keyframeColor);
        }

        FillCircle(new Vector2(texSize * 0.5f, texSize * 0.5f), 8f, currentColor);
        DrawLine(new Vector2(texSize * 0.5f, texSize * 0.5f),
                 new Vector2(texSize * 0.5f, texSize * 0.5f + 20),
                 currentColor, 3f);
    }

    private Vector2 WorldToMap(Vector3 world)
    {
        Vector3 center = (mapMin + mapMax) * 0.5f;
        Vector3 half = (mapMax - mapMin) * 0.5f / zoom;

        float u = Mathf.InverseLerp(center.x - half.x, center.x + half.x, world.x);
        float v = Mathf.InverseLerp(center.z - half.z, center.z + half.z, world.z);
        return new Vector2(u * (texSize - 1), v * (texSize - 1));
    }

    private void DrawLine(Vector2 a, Vector2 b, Color c, float thickness)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.5f) return;
        dir /= len;
        int steps = Mathf.CeilToInt(len);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            FillCircle(Vector2.Lerp(a, b, t), thickness * 0.5f, c);
        }
    }

    private void FillCircle(Vector2 center, float radius, Color c)
    {
        int r = Mathf.CeilToInt(radius);
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= radius * radius)
                    SetPixel(cx + x, cy + y, c);
    }

    private void SetPixel(int x, int y, Color c)
    {
        if (x < 0 || x >= texSize || y < 0 || y >= texSize) return;
        pixels[y * texSize + x] = c;
    }

    // ------------------------------------------------------------------
    // Controls
    // ------------------------------------------------------------------

    private void OnZoomIn() { zoom = Mathf.Clamp(zoom + zoomStep, minZoom, maxZoom); }
    private void OnZoomOut() { zoom = Mathf.Clamp(zoom - zoomStep, minZoom, maxZoom); }

    private void OnToggleMode()
    {
        mode = (mode == MinimapMode.TopDown) ? MinimapMode.FirstPerson : MinimapMode.TopDown;
        titleText.text = mode == MinimapMode.TopDown ? "Pose Map  •  Top-Down" : "Pose Map  •  First-Person";
        RecalculateBounds();
    }

    public void SetVisible(bool v)
    {
        showMinimap = v;
        if (panelRT != null) panelRT.gameObject.SetActive(v);
    }
}