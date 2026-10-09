using System;
using UnityEngine;

/// <summary>
/// Shared data structures for the pose-graph SLAM prototype.
/// These are plain serializable classes so they can be inspected in the Inspector
/// and easily extended later (e.g. adding covariance, feature descriptors, etc.).
/// </summary>
[Serializable]
public class PoseNode
{
    public int id;                  // Unique sequential identifier
    public Pose pose;               // Current (possibly optimized) pose
    public Pose originalPose;       // Raw pose as reported by ARCore at keyframe time
    public float timestamp;         // Time.time when the keyframe was created

    public PoseNode(int id, Pose pose, float timestamp)
    {
        this.id = id;
        this.pose = pose;
        this.originalPose = pose;   // Keep a pristine copy for loop measurements
        this.timestamp = timestamp;
    }
}

[Serializable]
public class PoseEdge
{
    public int fromId;
    public int toId;
    public Pose relativePose;       // Relative transform from 'from' → 'to'
    public bool isLoopClosure;      // true = loop-closure edge, false = odometry edge

    public PoseEdge(int from, int to, Pose relative, bool isLoop = false)
    {
        fromId = from;
        toId = to;
        relativePose = relative;
        isLoopClosure = isLoop;
    }
}