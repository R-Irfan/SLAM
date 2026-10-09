using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight pose-graph that stores nodes and edges,
/// performs simple proximity-based loop detection,
/// and applies a basic drift-correction optimisation.
/// Designed for real-time mobile use; not a full SLAM back-end.
/// </summary>
public class PoseGraph : MonoBehaviour
{
    [Header("Loop-Closure Parameters")]
    [Tooltip("Maximum distance (metres) to consider a potential loop.")]
    public float loopDistanceThreshold = 0.60f;

    [Tooltip("Maximum angular difference (degrees) to consider a potential loop.")]
    public float loopAngleThreshold = 25f;

    [Tooltip("Minimum time (seconds) that must separate two keyframes for a loop to be accepted.")]
    public float minLoopTimeDiff = 4.0f;

    // Public data
    public readonly List<PoseNode> nodes = new List<PoseNode>();
    public readonly List<PoseEdge> edges = new List<PoseEdge>();

    /// <summary>Fired whenever the graph structure or poses change.</summary>
    public event Action OnGraphChanged;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public void AddNode(PoseNode node)
    {
        if (node == null)
        {
            Debug.LogWarning("[PoseGraph] Attempted to add a null node.");
            return;
        }

        // Prevent accidental duplicate IDs
        if (nodes.Exists(n => n.id == node.id))
        {
            Debug.LogWarning($"[PoseGraph] Node with id {node.id} already exists. Ignoring.");
            return;
        }

        nodes.Add(node);
        TryDetectLoop(node);
        RaiseGraphChanged();
    }

    public void AddOdometryEdge(int fromId, int toId, Pose relative)
    {
        if (!NodeExists(fromId) || !NodeExists(toId))
        {
            Debug.LogWarning($"[PoseGraph] Cannot add odometry edge {fromId}→{toId}: node missing.");
            return;
        }

        edges.Add(new PoseEdge(fromId, toId, relative, isLoop: false));
    }

    public void AddLoopEdge(int fromId, int toId, Pose relative)
    {
        if (!NodeExists(fromId) || !NodeExists(toId))
        {
            Debug.LogWarning($"[PoseGraph] Cannot add loop edge {fromId}→{toId}: node missing.");
            return;
        }

        // Avoid duplicate loop edges between the same pair
        if (edges.Exists(e => e.isLoopClosure &&
                              ((e.fromId == fromId && e.toId == toId) ||
                               (e.fromId == toId && e.toId == fromId))))
        {
            return;
        }

        edges.Add(new PoseEdge(fromId, toId, relative, isLoop: true));
        OptimizeSimple(fromId, toId);
        RaiseGraphChanged();
    }

    public void Clear()
    {
        nodes.Clear();
        edges.Clear();
        RaiseGraphChanged();
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private bool NodeExists(int id) => nodes.Exists(n => n.id == id);

    private PoseNode GetNode(int id) => nodes.Find(n => n.id == id);

    private void RaiseGraphChanged() => OnGraphChanged?.Invoke();

    /// <summary>
    /// Very simple proximity + orientation loop detector.
    /// For a production system replace this with feature matching or Cloud Anchors.
    /// </summary>
    private void TryDetectLoop(PoseNode newNode)
    {
        foreach (var old in nodes)
        {
            if (old.id == newNode.id) continue;
            if (newNode.timestamp - old.timestamp < minLoopTimeDiff) continue;

            float dist = Vector3.Distance(newNode.pose.position, old.pose.position);
            float angle = Quaternion.Angle(newNode.pose.rotation, old.pose.rotation);

            if (dist <= loopDistanceThreshold && angle <= loopAngleThreshold)
            {
                // Use the *original* (raw) poses for the measurement
                Pose relative = KeyframeManager.GetRelativePose(old.originalPose, newNode.originalPose);
                AddLoopEdge(old.id, newNode.id, relative);
                Debug.Log($"[PoseGraph] Loop closure accepted: {old.id} ↔ {newNode.id}");
                break;
            }
        }
    }

    /// <summary>
    /// Extremely lightweight drift correction for demonstration purposes.
    /// Distributes the observed loop error linearly along the chain of nodes
    /// that lie between the two loop endpoints.
    /// </summary>
    private void OptimizeSimple(int fromId, int toId)
    {
        PoseNode fromNode = GetNode(fromId);
        PoseNode toNode = GetNode(toId);
        if (fromNode == null || toNode == null) return;

        PoseEdge loopEdge = edges.Find(e => e.isLoopClosure && e.fromId == fromId && e.toId == toId);
        if (loopEdge == null) return;

        Pose currentRel = KeyframeManager.GetRelativePose(fromNode.pose, toNode.pose);

        Vector3 posError = loopEdge.relativePose.position - currentRel.position;
        Quaternion rotError = loopEdge.relativePose.rotation * Quaternion.Inverse(currentRel.rotation);

        int startIdx = nodes.FindIndex(n => n.id == fromId);
        int endIdx = nodes.FindIndex(n => n.id == toId);
        if (startIdx < 0 || endIdx < 0 || startIdx == endIdx) return;

        if (startIdx > endIdx)
        {
            (startIdx, endIdx) = (endIdx, startIdx);
            posError = -posError;
            rotError = Quaternion.Inverse(rotError);
        }

        int steps = endIdx - startIdx;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            PoseNode n = nodes[startIdx + i];

            n.pose.position += posError * t;
            n.pose.rotation = Quaternion.Slerp(Quaternion.identity, rotError, t) * n.pose.rotation;
        }
    }
}