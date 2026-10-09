using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Collects keyframes from the AR camera pose according to distance / angle / time thresholds.
/// Also creates odometry edges and forwards new nodes to the PoseGraph.
/// </summary>
public class KeyframeManager : MonoBehaviour
{
    [Header("Keyframe Selection Thresholds")]
    [Tooltip("Minimum translation (metres) before a new keyframe is created.")]
    public float minDistance = 0.35f;

    [Tooltip("Minimum rotation (degrees) before a new keyframe is created.")]
    public float minAngleDegrees = 12f;

    [Tooltip("Minimum time (seconds) that must elapse between keyframes.")]
    public float minTimeInterval = 0.4f;

    [Header("References")]
    [Tooltip("The AR Camera transform (usually XR Origin → Camera Offset → Main Camera).")]
    public Transform arCamera;

    [Tooltip("The PoseGraph component that will receive new nodes and edges.")]
    public PoseGraph poseGraph;

    /// <summary>Read-only access to the collected keyframes.</summary>
    public IReadOnlyList<PoseNode> Keyframes => keyframes;

    // Internal state
    private readonly List<PoseNode> keyframes = new List<PoseNode>();
    private Pose lastKeyframePose;
    private float lastKeyframeTime;
    private int nextId = 0;
    private bool hasFirstKeyframe = false;

    void Start()
    {
        // Auto-wire common references if the user forgot to assign them
        if (arCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                arCamera = mainCam.transform;
            else
                Debug.LogError("[KeyframeManager] No AR Camera assigned and Camera.main is null.");
        }

        if (poseGraph == null)
        {
            poseGraph = FindObjectOfType<PoseGraph>();
            if (poseGraph == null)
                Debug.LogError("[KeyframeManager] No PoseGraph found in the scene.");
        }
    }

    void Update()
    {
        // Early-out if essential references are missing or AR is not tracking
        if (arCamera == null || poseGraph == null) return;

        // Optional: you can also query ARSession.state == ARSessionState.SessionTracking
        // for extra robustness when tracking is lost.

        Pose current = new Pose(arCamera.position, arCamera.rotation);

        if (!hasFirstKeyframe)
        {
            AddKeyframe(current);
            return;
        }

        if (ShouldAddKeyframe(current))
        {
            AddKeyframe(current);
        }
    }

    /// <summary>
    /// Decides whether the current pose is different enough from the last keyframe
    /// to warrant adding a new node.
    /// </summary>
    private bool ShouldAddKeyframe(Pose current)
    {
        float dist = Vector3.Distance(current.position, lastKeyframePose.position);
        float angle = Quaternion.Angle(current.rotation, lastKeyframePose.rotation);
        float timeDelta = Time.time - lastKeyframeTime;

        return (dist >= minDistance || angle >= minAngleDegrees)
               && timeDelta >= minTimeInterval;
    }

    /// <summary>
    /// Creates a new PoseNode, adds an odometry edge (if not the first),
    /// and notifies the PoseGraph.
    /// </summary>
    private void AddKeyframe(Pose pose)
    {
        var node = new PoseNode(nextId, pose, Time.time);
        keyframes.Add(node);

        if (hasFirstKeyframe)
        {
            // Relative pose of the new keyframe expressed in the previous keyframe's frame
            Pose relative = GetRelativePose(lastKeyframePose, pose);
            poseGraph.AddOdometryEdge(keyframes[keyframes.Count - 2].id, node.id, relative);
        }

        poseGraph.AddNode(node);

        // Update bookkeeping
        lastKeyframePose = pose;
        lastKeyframeTime = Time.time;
        nextId++;
        hasFirstKeyframe = true;
    }

    /// <summary>
    /// Computes the relative pose of 'to' with respect to 'from'.
    /// Result = from⁻¹ * to
    /// </summary>
    public static Pose GetRelativePose(Pose from, Pose to)
    {
        Matrix4x4 fromMatrix = Matrix4x4.TRS(from.position, from.rotation, Vector3.one);
        Matrix4x4 toMatrix = Matrix4x4.TRS(to.position, to.rotation, Vector3.one);
        Matrix4x4 relative = fromMatrix.inverse * toMatrix;

        Vector3 position = relative.GetColumn(3);
        Quaternion rotation = relative.rotation;
        return new Pose(position, rotation);
    }

    /// <summary>
    /// Clears all collected keyframes. Call this together with PoseGraph.Clear().
    /// </summary>
    public void Clear()
    {
        keyframes.Clear();
        nextId = 0;
        hasFirstKeyframe = false;
        lastKeyframeTime = 0f;
    }
}