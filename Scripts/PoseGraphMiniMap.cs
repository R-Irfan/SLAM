using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Advanced on-screen Pose Minimap (Centered)
/// Views: Top-Down, Side, Perspective
/// + Zoom controls
/// No camera frustum
/// </summary>
public class PoseGraphMiniMap : MonoBehaviour
{
    public enum MinimapMode { TopDown, Side, Perspective }

    [Header("References")]
    public PoseGraph poseGraph;
    public Transform arCamera;

    [Header("Minimap Settings")]
    public Vector2 minimapSize = new Vector2(340, 340);
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
    public float minZoom = 0.5f;
    public float maxZoom = 5.0f;
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
            poseGraph.OnGraphChanged += RecalculateBounds;
    }

    void OnDestroy()
    {
        if (texture != null) Destroy(texture);
        if (poseGraph != null) poseGraph.OnGraphChanged -= RecalculateBounds;
    }

    void Update()
    {
        if (!showMinimap || poseGraph == null) return;
        DrawMinimap();
    }

    // ------------------------------------------------------------------
    // UI Creation
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

        // Main panel - CENTER
        GameObject panel = new GameObject("PoseMinimapPanel");
        panel.transform.SetParent(canvas.transform, false);

        panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = minimapSize;

        Image bg = panel.AddComponent<Image>();
        bg.color = backgroundColor;

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
        titleRT.anchoredPosition = new Vector2(0, -6);
        titleRT.sizeDelta = new Vector2(0, 26);

        titleText = titleGO.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 15;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = titleColor;
        titleText.alignment = TextAnchor.MiddleCenter;
        UpdateTitle();

        // Map image
        GameObject imgGO = new GameObject("Map");
        imgGO.transform.SetParent(panel.transform, false);
        RectTransform imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = new Vector2(10, 70);   // leave space for buttons
        imgRT.offsetMax = new Vector2(-10, -34);

        mapImage = imgGO.AddComponent<RawImage>();
        texture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        pixels = new Color32[texSize * texSize];
        mapImage.texture = texture;

        // ===== Buttons =====
        // View mode buttons
        CreateButton(panel.transform, "Top", new Vector2(-110, 18), () => SetMode(MinimapMode.TopDown));
        CreateButton(panel.transform, "Side", new Vector2(-50, 18), () => SetMode(MinimapMode.Side));
        CreateButton(panel.transform, "Persp", new Vector2(10, 18), () => SetMode(MinimapMode.Perspective));

        // Zoom buttons
        CreateButton(panel.transform, "+", new Vector2(70, 18), OnZoomIn);
        CreateButton(panel.transform, "−", new Vector2(120, 18), OnZoomOut);
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(52, 32);

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
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void RecalculateBounds()
    {
        if (poseGraph == null || poseGraph.nodes.Count == 0)
        {
            hasBounds = false;
            return;
        }

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
        float maxDim = Mathf.Max(size.x, size.y, size.z, 0.5f);
        Vector3 pad = Vector3.one * maxDim * 0.25f;
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

        // Trajectory
        for (int i = 1; i < poseGraph.nodes.Count; i++)
        {
            Vector2 a = WorldToMap(poseGraph.nodes[i - 1].pose.position);
            Vector2 b = WorldToMap(poseGraph.nodes[i].pose.position);
            DrawLine(a, b, trajectoryColor, 2.5f);
        }

        // Loop closures
        foreach (var e in poseGraph.edges)
        {
            if (!e.isLoopClosure) continue;
            var na = poseGraph.nodes.Find(n => n.id == e.fromId);
            var nb = poseGraph.nodes.Find(n => n.id == e.toId);
            if (na == null || nb == null) continue;
            DrawLine(WorldToMap(na.pose.position), WorldToMap(nb.pose.position), loopColor, 3.5f);
        }

        // Keyframes
        for (int i = 0; i < poseGraph.nodes.Count; i++)
        {
            Vector2 p = WorldToMap(poseGraph.nodes[i].pose.position);
            bool last = i == poseGraph.nodes.Count - 1;
            FillCircle(p, last ? 5f : 3.5f, last ? currentColor : keyframeColor);
        }

        // Current camera position (no frustum)
        if (arCamera != null)
        {
            Vector2 cur = WorldToMap(arCamera.position);
            FillCircle(cur, 7f, currentColor);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
    }

    // ------------------------------------------------------------------
    // Projection (Top / Side / Perspective)
    // ------------------------------------------------------------------

    private Vector2 WorldToMap(Vector3 world)
    {
        Vector3 center = (mapMin + mapMax) * 0.5f;
        Vector3 half = (mapMax - mapMin) * 0.5f / zoom;

        float u, v;

        switch (mode)
        {
            case MinimapMode.TopDown:
                // XZ plane
                u = Mathf.InverseLerp(center.x - half.x, center.x + half.x, world.x);
                v = Mathf.InverseLerp(center.z - half.z, center.z + half.z, world.z);
                break;

            case MinimapMode.Side:
                // ZY plane (side view looking along X)
                u = Mathf.InverseLerp(center.z - half.z, center.z + half.z, world.z);
                v = Mathf.InverseLerp(center.y - half.y, center.y + half.y, world.y);
                break;

            case MinimapMode.Perspective:
            default:
                // Isometric + mild perspective
                Vector3 rel = world - center;
                float angle = 45f * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float px = rel.x * cos + rel.z * sin;
                float py = rel.y;
                float pz = -rel.x * sin + rel.z * cos;

                // Mild perspective
                float persp = 1f / (1f + pz * 0.08f / zoom);
                px *= persp;
                py *= persp;

                float range = Mathf.Max(half.x, half.y, half.z);
                u = Mathf.InverseLerp(-range, range, px);
                v = Mathf.InverseLerp(-range, range, py);
                break;
        }

        return new Vector2(u * (texSize - 1), v * (texSize - 1));
    }

    // ------------------------------------------------------------------
    // Drawing helpers
    // ------------------------------------------------------------------

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

    private void SetMode(MinimapMode newMode)
    {
        mode = newMode;
        UpdateTitle();
        RecalculateBounds();
    }

    private void UpdateTitle()
    {
        string modeName = mode switch
        {
            MinimapMode.TopDown => "Top-Down",
            MinimapMode.Side => "Side",
            MinimapMode.Perspective => "Perspective",
            _ => "Unknown"
        };
        titleText.text = $"Pose Map  •  {modeName}";
    }

    private void OnZoomIn() => zoom = Mathf.Clamp(zoom + zoomStep, minZoom, maxZoom);
    private void OnZoomOut() => zoom = Mathf.Clamp(zoom - zoomStep, minZoom, maxZoom);

    public void SetVisible(bool v)
    {
        showMinimap = v;
        if (panelRT != null) panelRT.gameObject.SetActive(v);
    }
}