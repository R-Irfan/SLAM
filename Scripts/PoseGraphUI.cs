using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple runtime UI for controlling the Pose Graph SLAM visualisation.
/// Attach this to a Canvas and wire the UI elements in the Inspector
/// (or let the script auto-create a basic panel if none are assigned).
/// </summary>
public class PoseGraphUI : MonoBehaviour
{
    [Header("System References")]
    public KeyframeManager keyframeManager;
    public PoseGraph poseGraph;
    public PoseGraphVisualizerTemp visualizer;

    [Header("UI Elements (assign in Inspector)")]
    public Toggle toggleRaw;
    public Toggle toggleOptimized;
    public Toggle toggleNodes;
    public Toggle toggleLoops;
    public Button btnClear;
    public Button btnForceRefresh;
    public Text statusText;
    public TextMeshProUGUI statusTextTMP;

    [Header("Auto-create UI if missing")]
    public bool autoCreateUI = true;

    private float statusUpdateInterval = 0.5f;
    private float nextStatusUpdate = 0f;

    void Start()
    {
        if (keyframeManager == null) keyframeManager = FindObjectOfType<KeyframeManager>();
        if (poseGraph == null) poseGraph = FindObjectOfType<PoseGraph>();
        if (visualizer == null) visualizer = FindObjectOfType<PoseGraphVisualizerTemp>();

        if (autoCreateUI && (toggleRaw == null || btnClear == null))
        {
            CreateBasicUI();
        }

        if (toggleRaw != null)
        {
            toggleRaw.isOn = visualizer != null && visualizer.showRawTrajectory;
            toggleRaw.onValueChanged.AddListener(OnToggleRaw);
        }
        if (toggleOptimized != null)
        {
            toggleOptimized.isOn = visualizer != null && visualizer.showOptimizedTrajectory;
            toggleOptimized.onValueChanged.AddListener(OnToggleOptimized);
        }
        if (toggleNodes != null)
        {
            toggleNodes.isOn = visualizer != null && visualizer.showNodes;
            toggleNodes.onValueChanged.AddListener(OnToggleNodes);
        }
        if (toggleLoops != null)
        {
            toggleLoops.isOn = visualizer != null && visualizer.showLoopEdges;
            toggleLoops.onValueChanged.AddListener(OnToggleLoops);
        }
        if (btnClear != null)
            btnClear.onClick.AddListener(OnClear);
        if (btnForceRefresh != null)
            btnForceRefresh.onClick.AddListener(OnForceRefresh);
    }

    void Update()
    {
        if (Time.time >= nextStatusUpdate)
        {
            UpdateStatusText();
            nextStatusUpdate = Time.time + statusUpdateInterval;
        }
    }

    public void OnToggleRaw(bool value)
    {
        if (visualizer != null) visualizer.SetShowRaw(value);
    }

    public void OnToggleOptimized(bool value)
    {
        if (visualizer != null) visualizer.SetShowOptimized(value);
    }

    public void OnToggleNodes(bool value)
    {
        if (visualizer != null) visualizer.SetShowNodes(value);
    }

    public void OnToggleLoops(bool value)
    {
        if (visualizer != null) visualizer.SetShowLoops(value);
    }

    public void OnClear()
    {
        if (keyframeManager != null) keyframeManager.Clear();
        if (poseGraph != null) poseGraph.Clear();
        if (visualizer != null) visualizer.Refresh();
        Debug.Log("[PoseGraphUI] Graph cleared.");
    }

    public void OnForceRefresh()
    {
        if (visualizer != null) visualizer.Refresh();
    }

    private void UpdateStatusText()
    {
        if (poseGraph == null) return;

        int nodeCount = poseGraph.nodes.Count;
        int edgeCount = poseGraph.edges.Count;
        int loopCount = poseGraph.edges.FindAll(e => e.isLoopClosure).Count;

        string msg = $"Nodes: {nodeCount}  |  Edges: {edgeCount}  |  Loops: {loopCount}";

        if (statusText != null) statusText.text = msg;
        if (statusTextTMP != null) statusTextTMP.text = msg;
    }

    private void CreateBasicUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("PoseGraphCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject panel = new GameObject("ControlPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 1);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = new Vector2(10, -10);
        panelRT.sizeDelta = new Vector2(280, 220);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.65f);

        float y = -15;
        toggleRaw = CreateToggle(panel.transform, "Raw Trajectory", y); y -= 35;
        toggleOptimized = CreateToggle(panel.transform, "Optimized Trajectory", y); y -= 35;
        toggleNodes = CreateToggle(panel.transform, "Nodes", y); y -= 35;
        toggleLoops = CreateToggle(panel.transform, "Loop Edges", y); y -= 40;

        btnClear = CreateButton(panel.transform, "Clear Graph", y); y -= 40;
        btnForceRefresh = CreateButton(panel.transform, "Force Refresh", y);

        GameObject statusGO = new GameObject("Status");
        statusGO.transform.SetParent(panel.transform, false);
        RectTransform sRT = statusGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0, 0);
        sRT.anchorMax = new Vector2(1, 0);
        sRT.pivot = new Vector2(0.5f, 0);
        sRT.anchoredPosition = new Vector2(0, 8);
        sRT.sizeDelta = new Vector2(-20, 30);
        statusText = statusGO.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 14;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
    }

    private Toggle CreateToggle(Transform parent, string label, float yPos)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(-20, 30);

        Toggle toggle = go.AddComponent<Toggle>();
        toggle.isOn = true;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        RectTransform bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        toggle.targetGraphic = bgImg;

        GameObject check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        RectTransform cRT = check.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 0.2f);
        cRT.anchorMax = new Vector2(0.15f, 0.8f);
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;
        Image checkImg = check.AddComponent<Image>();
        checkImg.color = Color.green;
        toggle.graphic = checkImg;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform lRT = labelGO.AddComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.18f, 0);
        lRT.anchorMax = new Vector2(1, 1);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        Text txt = labelGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 16;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        return toggle;
    }

    private Button CreateButton(Transform parent, string label, float yPos)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(-20, 34);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.45f, 0.7f, 0.9f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform lRT = labelGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero;
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        Text txt = labelGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 16;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }
}