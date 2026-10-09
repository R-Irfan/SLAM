using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D Perspective Minimap (Unity Scene-view style)
/// Renders keyframe frustums + trajectory into a RenderTexture
/// </summary>
public class PoseMinimapPerspective : MonoBehaviour
{
    [Header("References")]
    public PoseGraph poseGraph;
    public Transform arCamera;

    [Header("Minimap Appearance")]
    public Vector2 minimapSize = new Vector2(380, 320);
    public Color backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);
    public Color borderColor = new Color(0.3f, 0.7f, 1f, 0.9f);
    public Color titleColor = Color.white;

    [Header("3D View Settings")]
    public float cameraDistance = 6f;          // How far the minimap camera is
    public float cameraHeight = 3.5f;
    public float cameraPitch = 35f;            // degrees
    public float frustumScale = 0.12f;
    public float currentFrustumScale = 0.18f;
    public Color keyframeColor = new Color(0.2f, 0.95f, 0.4f);
    public Color currentColor = new Color(1f, 0.85f, 0.15f);
    public Color trajectoryColor = new Color(0.3f, 0.75f, 1f);
    public Color loopColor = new Color(1f, 0.35f, 0.35f);

    [Header("Controls")]
    public float zoomSpeed = 1.5f;
    public float minDistance = 2.5f;
    public float maxDistance = 25f;

    // Internal
    private Canvas canvas;
    private RectTransform panelRT;
    private RawImage rawImage;
    private Text titleText;

    private Camera miniCam;
    private RenderTexture renderTex;
    private Transform miniCamPivot;            // We rotate/zoom around this

    private LineRenderer trajectoryLine;
    private List<GameObject> frustumObjects = new List<GameObject>();
    private List<LineRenderer> loopLines = new List<LineRenderer>();
    private GameObject currentFrustumGO;

    private Transform worldRoot;               // Parent for all 3D objects in minimap

    void Start()
    {
        if (poseGraph == null) poseGraph = FindObjectOfType<PoseGraph>();
        if (arCamera == null) arCamera = Camera.main?.transform;

        CreateUI();
        CreateMinimapCamera();
        Create3DObjects();

        if (poseGraph != null)
            poseGraph.OnGraphChanged += Refresh;
    }

    void OnDestroy()
    {
        if (renderTex != null) renderTex.Release();
        if (poseGraph != null) poseGraph.OnGraphChanged -= Refresh;
    }

    void Update()
    {
        UpdateMinimapCamera();
        UpdateCurrentFrustum();
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void CreateUI()
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("MinimapCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
        }

        // Centered panel
        GameObject panel = new GameObject("PerspectiveMinimap");
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
        RectTransform tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 1);
        tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0.5f, 1);
        tRT.anchoredPosition = new Vector2(0, -8);
        tRT.sizeDelta = new Vector2(0, 28);

        titleText = titleGO.AddComponent<Text>();
        titleText.text = "Pose Map  •  Perspective";
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = titleColor;
        titleText.alignment = TextAnchor.MiddleCenter;

        // RawImage
        GameObject imgGO = new GameObject("MapView");
        imgGO.transform.SetParent(panel.transform, false);
        RectTransform imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = new Vector2(10, 42);
        imgRT.offsetMax = new Vector2(-10, -36);

        rawImage = imgGO.AddComponent<RawImage>();

        // Buttons
        CreateButton(panel.transform, "+", new Vector2(50, 10), () => cameraDistance = Mathf.Clamp(cameraDistance - zoomSpeed, minDistance, maxDistance));
        CreateButton(panel.transform, "−", new Vector2(10, 10), () => cameraDistance = Mathf.Clamp(cameraDistance + zoomSpeed, minDistance, maxDistance));
        CreateButton(panel.transform, "Reset", new Vector2(-50, 10), ResetView);
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(60, 30);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.4f, 0.7f, 0.95f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(action);
        btn.targetGraphic = img;

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tr = txtGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 15;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    // ------------------------------------------------------------------
    // Minimap Camera + RenderTexture
    // ------------------------------------------------------------------

    private void CreateMinimapCamera()
    {
        // Pivot (center of the map)
        miniCamPivot = new GameObject("MinimapPivot").transform;
        miniCamPivot.position = Vector3.zero;

        // Camera
        GameObject camGO = new GameObject("MinimapCamera");
        camGO.transform.SetParent(miniCamPivot);
        miniCam = camGO.AddComponent<Camera>();
        miniCam.clearFlags = CameraClearFlags.SolidColor;
        miniCam.backgroundColor = new Color(0.1f, 0.11f, 0.14f);
        miniCam.fieldOfView = 50f;
        miniCam.nearClipPlane = 0.1f;
        miniCam.farClipPlane = 100f;
        miniCam.depth = -10;               // very low so it doesn't interfere

        // RenderTexture
        renderTex = new RenderTexture(512, 512, 16);
        renderTex.antiAliasing = 2;
        miniCam.targetTexture = renderTex;
        rawImage.texture = renderTex;
    }

    private void UpdateMinimapCamera()
    {
        if (poseGraph == null || poseGraph.nodes.Count == 0) return;

        // Center the pivot on the middle of the trajectory
        Vector3 center = Vector3.zero;
        foreach (var n in poseGraph.nodes)
            center += n.pose.position;
        center /= poseGraph.nodes.Count;

        if (arCamera != null)
            center = Vector3.Lerp(center, arCamera.position, 0.3f);

        miniCamPivot.position = center;

        // Position camera
        Quaternion rot = Quaternion.Euler(cameraPitch, 45f, 0f);  // nice isometric-ish angle
        miniCam.transform.localPosition = rot * new Vector3(0, 0, -cameraDistance);
        miniCam.transform.LookAt(miniCamPivot);
    }

    // ------------------------------------------------------------------
    // 3D Content (Frustums + Trajectory)
    // ------------------------------------------------------------------

    private void Create3DObjects()
    {
        worldRoot = new GameObject("MinimapWorld").transform;

        trajectoryLine = CreateLine("Trajectory", trajectoryColor, 0.025f);
        trajectoryLine.transform.SetParent(worldRoot);
    }

    public void Refresh()
    {
        if (poseGraph == null) return;

        // Trajectory
        int count = poseGraph.nodes.Count;
        if (count >= 2)
        {
            trajectoryLine.positionCount = count;
            for (int i = 0; i < count; i++)
                trajectoryLine.SetPosition(i, poseGraph.nodes[i].pose.position);
        }
        else trajectoryLine.positionCount = 0;

        // Keyframe frustums
        foreach (var go in frustumObjects) go.SetActive(false);
        EnsureFrustumPool(count);

        for (int i = 0; i < count; i++)
        {
            var go = frustumObjects[i];
            go.SetActive(true);
            go.transform.SetPositionAndRotation(poseGraph.nodes[i].pose.position, poseGraph.nodes[i].pose.rotation);
        }

        // Loop edges
        foreach (var lr in loopLines) lr.gameObject.SetActive(false);
        var loops = poseGraph.edges.FindAll(e => e.isLoopClosure);
        EnsureLoopPool(loops.Count);

        for (int i = 0; i < loops.Count; i++)
        {
            var e = loops[i];
            var a = poseGraph.nodes.Find(n => n.id == e.fromId);
            var b = poseGraph.nodes.Find(n => n.id == e.toId);
            if (a == null || b == null) continue;

            var lr = loopLines[i];
            lr.gameObject.SetActive(true);
            lr.positionCount = 2;
            lr.SetPosition(0, a.pose.position);
            lr.SetPosition(1, b.pose.position);
        }
    }

    private void UpdateCurrentFrustum()
    {
        if (arCamera == null) return;

        if (currentFrustumGO == null)
            currentFrustumGO = CreateFrustum("Current", currentColor, currentFrustumScale);

        currentFrustumGO.SetActive(true);
        currentFrustumGO.transform.SetPositionAndRotation(arCamera.position, arCamera.rotation);
    }

    private void EnsureFrustumPool(int required)
    {
        while (frustumObjects.Count < required)
        {
            var go = CreateFrustum("KF_" + frustumObjects.Count, keyframeColor, frustumScale);
            go.transform.SetParent(worldRoot);
            go.SetActive(false);
            frustumObjects.Add(go);
        }
    }

    private void EnsureLoopPool(int required)
    {
        while (loopLines.Count < required)
        {
            var lr = CreateLine("Loop_" + loopLines.Count, loopColor, 0.03f);
            lr.transform.SetParent(worldRoot);
            lr.gameObject.SetActive(false);
            loopLines.Add(lr);
        }
    }

    private GameObject CreateFrustum(string name, Color color, float scale)
    {
        GameObject root = new GameObject(name);

        Vector3[] near = {
            new Vector3(-0.5f, -0.4f, 0.8f) * scale,
            new Vector3( 0.5f, -0.4f, 0.8f) * scale,
            new Vector3( 0.5f,  0.4f, 0.8f) * scale,
            new Vector3(-0.5f,  0.4f, 0.8f) * scale
        };
        Vector3[] far = {
            new Vector3(-1.2f, -0.9f, 2.3f) * scale,
            new Vector3( 1.2f, -0.9f, 2.3f) * scale,
            new Vector3( 1.2f,  0.9f, 2.3f) * scale,
            new Vector3(-1.2f,  0.9f, 2.3f) * scale
        };

        for (int i = 0; i < 4; i++)
        {
            CreateEdge(root, near[i], near[(i + 1) % 4], color);
            CreateEdge(root, far[i], far[(i + 1) % 4], color);
            CreateEdge(root, near[i], far[i], color);
        }
        return root;
    }

    private void CreateEdge(GameObject parent, Vector3 a, Vector3 b, Color c)
    {
        var go = new GameObject("E");
        go.transform.SetParent(parent.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = 0.01f;
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private LineRenderer CreateLine(string name, Color c, float w)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = w;
        lr.useWorldSpace = true;
        return lr;
    }

    private void ResetView()
    {
        cameraDistance = 6f;
        cameraPitch = 35f;
    }
}