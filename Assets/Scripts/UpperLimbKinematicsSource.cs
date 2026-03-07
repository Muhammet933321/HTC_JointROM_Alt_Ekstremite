using UnityEngine;

/// <summary>
/// Shared interface for components that expose upper-limb joint poses to the visualizer.
/// </summary>
public interface IUpperLimbKinematicsSource
{
    Pose WristPose { get; }
    Pose ElbowPose { get; }
    Pose ShoulderPose { get; }
    Quaternion WristRelativeRotation { get; }
    Quaternion ElbowRelativeRotation { get; }
    Quaternion ShoulderRelativeRotation { get; }
    bool TryGetShoulderReference(out Pose pose);
}
