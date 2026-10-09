using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PoseMapVisualizer : MonoBehaviour
{
    [Header("References")]
    public PoseGraph poseGraph;
    public Transform arCamera;

    [Header("Frustum Settings")]
    public float frustumScale = 0.07f;
    public float currentScale = 0.12f;
    public Color keyframeColor = new Color(0.2f, 0.9f, 0.35f);
    public Color currentColor = new Color(1f, 0.85f, 0.15f);
    public Color trajectoryColor = new Color(0.3f, 0.7f, 1f);
    public Color loopColor = new Color(1f, 0.3f, 0.3f);
    public float trajectoryWidth = 0.012f;

    public bool showKeyframes = true;
    public bool showTrajectory = true;
    public bool showLoops = true;
    public bool showCurrent = true;

    private LineRenderer trajectoryLine;
    private List<GameObject> frustums = new List<GameObject>();
    private List<LineRenderer> loopLines = new List<LineRenderer>();
    private GameObject currentFrustum;
    private Transform kfParent, loopParent;

    void Awake()
    {
        kfParent = new GameObject("Keyframes3D").transform;
        kfParent.SetParent(transform);
        loopParent = new GameObject("Loops3D").transform;
        loopParent.SetParent(transform);

        trajectoryLine = CreateLine("Trajectory3D", trajectoryColor, trajectoryWidth);
    }

    void Start()
    {
        if (poseGraph == null) poseGraph = GetComponent<PoseGraph>();
        if (arCamera == null) arCamera = Camera.main?.transform;
        if (poseGraph != null) poseGraph.OnGraphChanged += Refresh;
    }

    void Update()
    {
        if (showCurrent && arCamera != null)
        {
            if (currentFrustum == null)
                currentFrustum = CreateFrustum("Current", currentColor, currentScale);
            currentFrustum.transform.SetPositionAndRotation(arCamera.position, arCamera.rotation);
            currentFrustum.SetActive(true);
        }
        else if (currentFrustum != null) currentFrustum.SetActive(false);
    }

    public void Refresh()
    {
        if (poseGraph == null) return;
        UpdateTrajectory();
        UpdateKeyframes();
        UpdateLoops();
    }

    private void UpdateTrajectory()
    {
        int n = poseGraph.nodes.Count;
        if (!showTrajectory || n < 2) { trajectoryLine.positionCount = 0; return; }

        trajectoryLine.positionCount = n;
        for (int i = 0; i < n; i++)
            trajectoryLine.SetPosition(i, poseGraph.nodes[i].pose.position);
    }

    private void UpdateKeyframes()
    {
        foreach (var go in frustums) go.SetActive(false);
        if (!showKeyframes) return;

        EnsureFrustumPool(poseGraph.nodes.Count);
        for (int i = 0; i < poseGraph.nodes.Count; i++)
        {
            var go = frustums[i];
            go.SetActive(true);
            go.transform.SetPositionAndRotation(poseGraph.nodes[i].pose.position, poseGraph.nodes[i].pose.rotation);
        }
    }

    private void UpdateLoops()
    {
        foreach (var lr in loopLines) lr.gameObject.SetActive(false);
        if (!showLoops) return;

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

    private void EnsureFrustumPool(int count)
    {
        while (frustums.Count < count)
        {
            var go = CreateFrustum($"KF_{frustums.Count}", keyframeColor, frustumScale);
            go.transform.SetParent(kfParent);
            go.SetActive(false);
            frustums.Add(go);
        }
    }

    private void EnsureLoopPool(int count)
    {
        while (loopLines.Count < count)
        {
            var lr = CreateLine($"Loop_{loopLines.Count}", loopColor, 0.02f);
            lr.transform.SetParent(loopParent);
            lr.gameObject.SetActive(false);
            loopLines.Add(lr);
        }
    }

    private GameObject CreateFrustum(string name, Color color, float scale)
    {
        GameObject root = new GameObject(name);

        Vector3[] near = {
            new Vector3(-0.5f,-0.4f,0.8f)*scale, new Vector3(0.5f,-0.4f,0.8f)*scale,
            new Vector3(0.5f,0.4f,0.8f)*scale,  new Vector3(-0.5f,0.4f,0.8f)*scale
        };
        Vector3[] far = {
            new Vector3(-1.1f,-0.85f,2.1f)*scale, new Vector3(1.1f,-0.85f,2.1f)*scale,
            new Vector3(1.1f,0.85f,2.1f)*scale,  new Vector3(-1.1f,0.85f,2.1f)*scale
        };

        // Near + Far rectangles + connectors
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
        lr.startWidth = lr.endWidth = 0.007f;
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private LineRenderer CreateLine(string name, Color c, float w)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = w;
        lr.useWorldSpace = true;
        return lr;
    }
}