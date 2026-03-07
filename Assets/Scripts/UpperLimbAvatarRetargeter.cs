using UnityEngine;

/// <summary>
/// Retargets joint rotations provided by an <see cref="IUpperLimbKinematicsSource"/> to an arbitrary avatar arm.
/// Use this on the remote avatar so that bone rotations, not segment lengths, mirror the tracked user.
/// </summary>
[DisallowMultipleComponent]
public class UpperLimbAvatarRetargeter : MonoBehaviour
{
    [Header("Kinematics source")]
    [Tooltip("Component that exposes live joint rotations (e.g., UpperLimbKinematicsUI or a network proxy).")]
    [SerializeField] private MonoBehaviour kinematicsSource;

    [Header("Avatar bones")]
    [Tooltip("Optional transform that should receive the tracked shoulder root pose (usually the clavicle/root parent).")]
    [SerializeField] private Transform shoulderRoot;
    [Tooltip("Bone driven by the shoulder delta rotation (often upper-arm).")]
    [SerializeField] private Transform shoulderBone;
    [Tooltip("Bone driven by the elbow delta rotation (often forearm).")]
    [SerializeField] private Transform elbowBone;
    [Tooltip("Bone driven by the wrist delta rotation (often hand).")]
    [SerializeField] private Transform wristBone;

    [Header("Options")]
    [Tooltip("Apply the tracked shoulder reference pose to the shoulderRoot transform (position + rotation).")]
    [SerializeField] private bool driveShoulderRootPose = true;

    private IUpperLimbKinematicsSource _kinematics;
    private Quaternion _shoulderBoneBase = Quaternion.identity;
    private Quaternion _elbowBoneBase = Quaternion.identity;
    private Quaternion _wristBoneBase = Quaternion.identity;

    private void Awake()
    {
        ResolveSource();
        CacheNeutralRotations();
    }

    private void OnValidate()
    {
        ResolveSource();
        CacheNeutralRotations();
    }

    private void LateUpdate()
    {
        if (_kinematics == null)
        {
            ResolveSource();
            if (_kinematics == null)
            {
                return;
            }
        }

        if (driveShoulderRootPose && shoulderRoot && _kinematics.TryGetShoulderReference(out Pose referencePose))
        {
            shoulderRoot.SetPositionAndRotation(referencePose.position, referencePose.rotation);
        }

        if (shoulderBone)
        {
            shoulderBone.localRotation = _shoulderBoneBase * _kinematics.ShoulderRelativeRotation;
        }

        if (elbowBone)
        {
            elbowBone.localRotation = _elbowBoneBase * _kinematics.ElbowRelativeRotation;
        }

        if (wristBone)
        {
            wristBone.localRotation = _wristBoneBase * _kinematics.WristRelativeRotation;
        }
    }

    private void ResolveSource()
    {
        _kinematics = null;
        if (!kinematicsSource)
        {
            return;
        }

        _kinematics = kinematicsSource as IUpperLimbKinematicsSource;
        if (_kinematics == null)
        {
            Debug.LogWarning("UpperLimbAvatarRetargeter requires a component implementing IUpperLimbKinematicsSource.", this);
        }
    }

    private void CacheNeutralRotations()
    {
        if (shoulderBone)
        {
            _shoulderBoneBase = shoulderBone.localRotation;
        }

        if (elbowBone)
        {
            _elbowBoneBase = elbowBone.localRotation;
        }

        if (wristBone)
        {
            _wristBoneBase = wristBone.localRotation;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Neutral Rotations")]
    private void CaptureNeutralRotationsContext()
    {
        CacheNeutralRotations();
    }
#endif
}
