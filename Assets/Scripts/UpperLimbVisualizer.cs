using UnityEngine;

/// <summary>
/// Simple procedural upper-limb avatar that mirrors tracker-driven segment poses.
/// Should be fed by <see cref="UpperLimbKinematicsUI"/>.
/// </summary>
[DisallowMultipleComponent]
public class UpperLimbVisualizer : MonoBehaviour
{
    [Header("Segment references")]
    [Tooltip("Transform anchored at the wrist joint (drives the forearm segment).")]
    [SerializeField] private Transform wristJoint;
    [Tooltip("Transform anchored at the elbow joint.")]
    [SerializeField] private Transform elbowJoint;
    [Tooltip("Transform anchored at the shoulder joint.")]
    [SerializeField] private Transform shoulderJoint;

    [Header("Segment meshes")]
    [Tooltip("Visual mesh to stretch between wrist and elbow.")]
    [SerializeField] private Transform forearmMesh;
    [SerializeField] private Axis forearmMeshLengthAxis = Axis.PositiveZ;
    [SerializeField] private Vector3 forearmMeshRotationOffsetEuler = Vector3.zero;
    [Tooltip("Visual mesh to stretch between elbow and shoulder.")]
    [SerializeField] private Transform upperArmMesh;
    [SerializeField] private Axis upperArmMeshLengthAxis = Axis.PositiveZ;
    [SerializeField] private Vector3 upperArmMeshRotationOffsetEuler = Vector3.zero;
    [Tooltip("Optional torso mesh, aligned with shoulder reference frame.")]
    [SerializeField] private Transform torsoMesh;
    [SerializeField] private Vector3 torsoRotationOffsetEuler = Vector3.zero;
    [Tooltip("Optional hand mesh that should follow the wrist pose.")]
    [SerializeField] private Transform handMesh;
    [SerializeField] private Vector3 handRotationOffsetEuler = Vector3.zero;

    [Header("Source component")]
    [SerializeField] private MonoBehaviour kinematicsSource;

    private IUpperLimbKinematicsSource _kinematics;
    private Quaternion _forearmMeshRotationOffset = Quaternion.identity;
    private Quaternion _upperArmMeshRotationOffset = Quaternion.identity;
    private Quaternion _torsoRotationOffset = Quaternion.identity;
    private Quaternion _handRotationOffset = Quaternion.identity;
    private Vector3 _forearmInitialScale = Vector3.one;
    private Vector3 _upperArmInitialScale = Vector3.one;
    private Vector3 _torsoInitialScale = Vector3.one;
    private Vector3 _handInitialScale = Vector3.one;

    [Header("Appearance")]
    [Tooltip("Radius of joint spheres (for optional gizmos).")]
    [SerializeField] private float jointGizmoRadius = 0.015f;
    [Tooltip("Scale factor applied to meshes along their length axis.")]
    [SerializeField] private float meshThickness = 0.04f;

    private void LateUpdate()
    {
        if (_kinematics == null)
        {
            if (kinematicsSource)
            {
                ResolveSource();
            }

            if (_kinematics == null)
            {
                return;
            }
        }

        Pose wristPose = _kinematics.WristPose;
        Pose elbowPose = _kinematics.ElbowPose;
        Pose shoulderPose = _kinematics.ShoulderPose;

        // Update joint markers if provided.
        if (wristJoint)
        {
            wristJoint.SetPositionAndRotation(wristPose.position, wristPose.rotation);
        }

        if (elbowJoint)
        {
            elbowJoint.SetPositionAndRotation(elbowPose.position, elbowPose.rotation);
        }

        if (shoulderJoint)
        {
            shoulderJoint.SetPositionAndRotation(shoulderPose.position, shoulderPose.rotation);
        }

        // Update segment meshes.
        UpdateSegmentMesh(forearmMesh, wristPose, elbowPose, forearmMeshLengthAxis, _forearmMeshRotationOffset, _forearmInitialScale);
        UpdateSegmentMesh(upperArmMesh, elbowPose, shoulderPose, upperArmMeshLengthAxis, _upperArmMeshRotationOffset, _upperArmInitialScale);

        if (torsoMesh)
        {
            if (_kinematics.TryGetShoulderReference(out Pose referencePose))
            {
                torsoMesh.SetPositionAndRotation(referencePose.position, referencePose.rotation * _torsoRotationOffset);
            }
            else
            {
                torsoMesh.SetPositionAndRotation(shoulderPose.position, shoulderPose.rotation * _torsoRotationOffset);
            }
            torsoMesh.localScale = _torsoInitialScale;
        }

        if (handMesh)
        {
            handMesh.SetPositionAndRotation(wristPose.position, wristPose.rotation * _handRotationOffset);
            handMesh.localScale = _handInitialScale;
        }
    }

    private void Awake()
    {
        ResolveSource();
        CacheInitialStates();
    }

    private void OnValidate()
    {
        ResolveSource();
        CacheInitialStates();
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
            Debug.LogWarning("UpperLimbVisualizer source must implement IUpperLimbKinematicsSource.", this);
        }
    }

    private void CacheInitialStates()
    {
        _forearmMeshRotationOffset = Quaternion.Euler(forearmMeshRotationOffsetEuler);
        _upperArmMeshRotationOffset = Quaternion.Euler(upperArmMeshRotationOffsetEuler);
        _torsoRotationOffset = Quaternion.Euler(torsoRotationOffsetEuler);
        _handRotationOffset = Quaternion.Euler(handRotationOffsetEuler);

        if (forearmMesh)
        {
            _forearmInitialScale = forearmMesh.localScale;
        }

        if (upperArmMesh)
        {
            _upperArmInitialScale = upperArmMesh.localScale;
        }

        if (torsoMesh)
        {
            _torsoInitialScale = torsoMesh.localScale;
        }

        if (handMesh)
        {
            _handInitialScale = handMesh.localScale;
        }
    }

    private void UpdateSegmentMesh(Transform meshRoot, Pose distal, Pose proximal, Axis lengthAxis, Quaternion rotationOffset, Vector3 baseScale)
    {
        if (!meshRoot)
        {
            return;
        }

        Vector3 fromTo = proximal.position - distal.position;
        float length = fromTo.magnitude;
        if (length <= Mathf.Epsilon)
        {
            return;
        }

            Vector3 direction = fromTo.normalized;
            meshRoot.position = distal.position + direction * (length * 0.5f);
        Vector3 axisVector = AxisVector(lengthAxis);
            Quaternion lookRotation = Quaternion.FromToRotation(axisVector, direction);
        meshRoot.rotation = lookRotation * rotationOffset;
        meshRoot.localScale = BuildScale(baseScale, lengthAxis, length, meshThickness);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_kinematics == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(_kinematics.WristPose.position, jointGizmoRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(_kinematics.ElbowPose.position, jointGizmoRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_kinematics.ShoulderPose.position, jointGizmoRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(_kinematics.WristPose.position, _kinematics.ElbowPose.position);
        Gizmos.DrawLine(_kinematics.ElbowPose.position, _kinematics.ShoulderPose.position);
    }
#endif

    private enum Axis
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ
    }

    private static Vector3 BuildScale(Vector3 baseScale, Axis axis, float length, float thickness)
    {
        Vector3 scale = baseScale;
        int primaryIndex = AxisIndex(axis);
        scale[primaryIndex] = length;

        for (int i = 0; i < 3; i++)
        {
            if (i == primaryIndex)
            {
                continue;
            }

            float sign = Mathf.Sign(scale[i]);
            if (Mathf.Approximately(sign, 0f))
            {
                sign = 1f;
            }

            scale[i] = thickness * sign;
        }

        return scale;
    }

    private static int AxisIndex(Axis axis)
    {
        return axis switch
        {
            Axis.PositiveX => 0,
            Axis.NegativeX => 0,
            Axis.PositiveY => 1,
            Axis.NegativeY => 1,
            Axis.PositiveZ => 2,
            Axis.NegativeZ => 2,
            _ => 2
        };
    }

    private static Vector3 AxisVector(Axis axis)
    {
        return axis switch
        {
            Axis.PositiveX => Vector3.right,
            Axis.NegativeX => Vector3.left,
            Axis.PositiveY => Vector3.up,
            Axis.NegativeY => Vector3.down,
            Axis.PositiveZ => Vector3.forward,
            Axis.NegativeZ => Vector3.back,
            _ => Vector3.forward
        };
    }
}
