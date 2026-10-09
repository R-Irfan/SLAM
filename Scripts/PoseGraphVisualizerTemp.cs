using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the pose graph in the scene:
/// - Raw ARCore trajectory (cyan)
/// - Optimised trajectory (white)
/// - Keyframe nodes (yellow spheres, red for the newest)
/// - Loop-closure edges (green)
/// Uses simple object pooling for nodes to avoid constant Instantiate/Destroy.
/// </summary>
public class PoseGraphVisualizerTemp : MonoBehaviour
{
    [Header("References")]
    public PoseGraph poseGraph;

    [Header("Visual Settings")]
    public Material lineMaterial;
    public Color odometryColor = new Color(0.2f, 0.8f, 1f);
    public Color optimizedColor = Color.white;
    public Color loopColor = Color.green;
    public Color nodeColor = Color.yellow;
    public Color currentNodeColor = Color.red;
    public float nodeScale = 0.04f;
    public float lineWidth = 0.015f;

    [Header("Visibility Toggles")]
    public bool showRawTrajectory = true;
    public bool showOptimizedTrajectory = true;
    public bool showNodes = true;
    public bool showLoopEdges = true;

    private LineRenderer rawLine;
    private LineRenderer optimizedLine;
    private readonly List<GameObject> nodePool = new List<GameObject>();
    private readonly List<LineRenderer> loopEdgePool = new List<LineRenderer>();
    private Transform nodeParent;
    private Transform edgeParent;

    void Awake()
    {
        nodeParent = new GameObject("Nodes").transform;
        nodeParent.SetParent(transform);
        edgeParent = new GameObject("LoopEdges").transform;
        edgeParent.SetParent(transform);

        rawLine = CreateLineRenderer("RawTrajectory", odometryColor);
        optimizedLine = CreateLineRenderer("OptimizedTrajectory", optimizedColor);
    }

    void Start()
    {
        if (poseGraph == null)
            poseGraph = FindObjectOfType<PoseGraph>();

        if (poseGraph != null)
            poseGraph.OnGraphChanged += Refresh;
        else
            Debug.LogError("[PoseGraphVisualizer] No PoseGraph found.");
    }

    void OnDestroy()
    {
        if (poseGraph != null)
            poseGraph.OnGraphChanged -= Refresh;
    }

    public void SetShowRaw(bool value) { showRawTrajectory = value; Refresh(); }
    public void SetShowOptimized(bool value) { showOptimizedTrajectory = value; Refresh(); }
    public void SetShowNodes(bool value) { showNodes = value; Refresh(); }
    public void SetShowLoops(bool value) { showLoopEdges = value; Refresh(); }

    public void Refresh()
    {
        if (poseGraph == null) return;

        UpdateTrajectories();
        UpdateNodes();
        UpdateLoopEdges();
    }

    private LineRenderer CreateLineRenderer(string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial != null
            ? lineMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = lineWidth;
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.numCapVertices = 4;
        return lr;
    }

    private void UpdateTrajectories()
    {
        int count = poseGraph.nodes.Count;

        if (showRawTrajectory && count > 0)
        {
            rawLine.positionCount = count;
            for (int i = 0; i < count; i++)
                rawLine.SetPosition(i, poseGraph.nodes[i].originalPose.position);
        }
        else
        {
            rawLine.positionCount = 0;
        }

        if (showOptimizedTrajectory && count > 0)
        {
            optimizedLine.positionCount = count;
            for (int i = 0; i < count; i++)
                optimizedLine.SetPosition(i, poseGraph.nodes[i].pose.position);
        }
        else
        {
            optimizedLine.positionCount = 0;
        }
    }

    private void UpdateNodes()
    {
        foreach (var go in nodePool)
            go.SetActive(false);

        if (!showNodes) return;

        int count = poseGraph.nodes.Count;
        EnsureNodePoolSize(count);

        for (int i = 0; i < count; i++)
        {
            GameObject sphere = nodePool[i];
            sphere.SetActive(true);
            sphere.transform.position = poseGraph.nodes[i].pose.position;
            sphere.transform.localScale = Vector3.one * nodeScale;

            var rend = sphere.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = (i == count - 1) ? currentNodeColor : nodeColor;
        }
    }

    private void EnsureNodePoolSize(int required)
    {
        while (nodePool.Count < required)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Node_{nodePool.Count}";
            sphere.transform.SetParent(nodeParent);
            sphere.GetComponent<Collider>().enabled = false;

            var rend = sphere.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Standard"));

            sphere.SetActive(false);
            nodePool.Add(sphere);
        }
    }

    private void UpdateLoopEdges()
    {
        foreach (var lr in loopEdgePool)
            lr.gameObject.SetActive(false);

        if (!showLoopEdges) return;

        var loops = poseGraph.edges.FindAll(e => e.isLoopClosure);
        EnsureLoopEdgePoolSize(loops.Count);

        for (int i = 0; i < loops.Count; i++)
        {
            var edge = loops[i];
            PoseNode a = poseGraph.nodes.Find(n => n.id == edge.fromId);
            PoseNode b = poseGraph.nodes.Find(n => n.id == edge.toId);
            if (a == null || b == null) continue;

            LineRenderer lr = loopEdgePool[i];
            lr.gameObject.SetActive(true);
            lr.positionCount = 2;
            lr.SetPosition(0, a.pose.position);
            lr.SetPosition(1, b.pose.position);
            lr.startWidth = lr.endWidth = lineWidth * 1.8f;
            lr.startColor = lr.endColor = loopColor;
        }
    }

    private void EnsureLoopEdgePoolSize(int required)
    {
        while (loopEdgePool.Count < required)
        {
            LineRenderer lr = CreateLineRenderer($"LoopEdge_{loopEdgePool.Count}", loopColor);
            lr.transform.SetParent(edgeParent);
            lr.gameObject.SetActive(false);
            loopEdgePool.Add(lr);
        }
    }
}