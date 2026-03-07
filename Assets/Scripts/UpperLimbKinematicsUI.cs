using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR;
using VIVE.OpenXR.Hand;

/// <summary>
/// Computes upper-limb joint angles by combining OpenXR hand-tracking data with tracker poses
/// and writes the values to TextMeshPro labels.
/// </summary>
[DisallowMultipleComponent]
public class UpperLimbKinematicsUI : MonoBehaviour, IUpperLimbKinematicsSource
{
    [Header("Tracked sources")]
    [SerializeField] private XRNode handNode = XRNode.RightHand;
    [SerializeField] private Transform forearmTracker;
    [SerializeField] private Transform upperArmTracker;
    [Tooltip("Optional torso tracker. When assigned and tracked it defines the shoulder reference frame.")]
    [SerializeField] private Transform torsoTracker;
    [Tooltip("Secondary reference (for example HMD). Used when torso tracker is missing or untracked.")]
    [SerializeField] private Transform shoulderReference;

    [Header("Segment geometry (metres)")]
    [Tooltip("Distance from the wrist joint to the elbow joint along the forearm.")]
    [SerializeField, Min(0.05f)] private float forearmLength = 0.26f;
    [Tooltip("Distance from the elbow joint to the shoulder joint along the upper arm.")]
    [SerializeField, Min(0.05f)] private float upperArmLength = 0.30f;

    [Header("Tracker anatomical axes")]
    [Tooltip("Forearm tracker local axis that points from the wrist towards the elbow in the tracker-neutral pose.")]
    [SerializeField] private Vector3 forearmTrackerAxis = Vector3.forward;
    [Tooltip("Upper-arm tracker local axis that points from the elbow towards the shoulder in the tracker-neutral pose.")]
    [SerializeField] private Vector3 upperArmTrackerAxis = Vector3.forward;

    [Header("UI labels")]
    [SerializeField] private TMP_Text wristLabel;
    [SerializeField] private TMP_Text elbowLabel;
    [SerializeField] private TMP_Text shoulderLabel;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Runtime options")]
    [Tooltip("When disabled the component keeps tracking but skips text updates.")]
    [SerializeField] private bool updateUI = true;

    private Quaternion _wristNeutral = Quaternion.identity;
    private Quaternion _elbowNeutral = Quaternion.identity;
    private Quaternion _shoulderNeutral = Quaternion.identity;
    private bool _hasCalibration;

    private Pose _wristPose;
    private Pose _elbowPose;
    private Pose _shoulderPose;
    public Pose WristPose => _wristPose;
    public Pose ElbowPose => _elbowPose;
    public Pose ShoulderPose => _shoulderPose;
    private Quaternion _wristRelativeRotation = Quaternion.identity;
    private Quaternion _elbowRelativeRotation = Quaternion.identity;
    private Quaternion _shoulderRelativeRotation = Quaternion.identity;
    public Quaternion WristRelativeRotation => _wristRelativeRotation;
    public Quaternion ElbowRelativeRotation => _elbowRelativeRotation;
    public Quaternion ShoulderRelativeRotation => _shoulderRelativeRotation;
    private ViveHandTracking _handTracking;
    private readonly StringBuilder _statusBuilder = new();

    private Pose _shoulderReferencePose;
    private bool _hasShoulderReferencePose;

    private enum ShoulderFrameSource
    {
        World,
        Headset,
        TorsoTracker
    }

    private void Awake()
    {
        ResetSolvedData();
        ValidateAssignments();
    }

    private void Update()
    {
    bool torsoTracked = IsTrackerTracked(torsoTracker);
    bool headsetTracked = IsTrackerTracked(shoulderReference);
    bool handTrackingAvailable = ResolveHandTracking() != null;

        if (!forearmTracker || !upperArmTracker)
        {
            ShoulderFrameSource fallbackSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out _, out _);
            ResetSolvedData();
            if (updateUI)
            {
                WriteLabels(default, default, default, 0f, 0f, false);
            }
            WriteStatus(false, false, torsoTracked, headsetTracked, handTrackingAvailable, fallbackSource);
            return;
        }

        bool forearmTracked = IsTrackerTracked(forearmTracker);
        bool upperArmTracked = IsTrackerTracked(upperArmTracker);

        if (!forearmTracked || !upperArmTracked)
        {
            ShoulderFrameSource fallbackSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out _, out _);
            ResetSolvedData();
            if (updateUI)
            {
                WriteLabels(default, default, default, 0f, 0f, false);
            }
            WriteStatus(forearmTracked, upperArmTracked, torsoTracked, headsetTracked, handTrackingAvailable, fallbackSource);
            return;
        }

        if (!TryGetJointLocations(out XrHandJointLocationEXT[] joints))
        {
            ShoulderFrameSource fallbackSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out _, out _);
            ResetSolvedData();
            if (updateUI)
            {
                WriteLabels(default, default, default, 0f, 0f, false);
            }
            WriteStatus(forearmTracked, upperArmTracked, torsoTracked, headsetTracked, handTrackingAvailable, fallbackSource);
            return;
        }

        ref XrHandJointLocationEXT wristJoint = ref joints[(int)XrHandJointEXT.XR_HAND_JOINT_WRIST_EXT];
        if (!IsJointTracked(wristJoint))
        {
            ShoulderFrameSource fallbackSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out _, out _);
            ResetSolvedData();
            if (updateUI)
            {
                WriteLabels(default, default, default, 0f, 0f, false);
            }
            WriteStatus(forearmTracked, upperArmTracked, torsoTracked, headsetTracked, handTrackingAvailable, fallbackSource);
            return;
        }

        Pose wristPose = BuildPose(wristJoint);

        Quaternion forearmRot = forearmTracker.rotation;
        Quaternion upperArmRot = upperArmTracker.rotation;
        ShoulderFrameSource frameSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out Quaternion shoulderRefRot, out Vector3 referencePosition);

        Quaternion wristRelative = Quaternion.Inverse(forearmRot) * wristPose.rotation;
        Quaternion elbowRelative = Quaternion.Inverse(upperArmRot) * forearmRot;
        Quaternion shoulderRelative = frameSource == ShoulderFrameSource.World ? upperArmRot : Quaternion.Inverse(shoulderRefRot) * upperArmRot;

        if (_hasCalibration)
        {
            wristRelative = Quaternion.Inverse(_wristNeutral) * wristRelative;
            elbowRelative = Quaternion.Inverse(_elbowNeutral) * elbowRelative;
            shoulderRelative = Quaternion.Inverse(_shoulderNeutral) * shoulderRelative;
        }

    _wristRelativeRotation = wristRelative;
    _elbowRelativeRotation = elbowRelative;
    _shoulderRelativeRotation = shoulderRelative;

    Vector3 wristAngles = NormalizeEuler(wristRelative.eulerAngles);
    Vector3 elbowAngles = NormalizeEuler(elbowRelative.eulerAngles);
    Vector3 shoulderAngles = NormalizeEuler(shoulderRelative.eulerAngles);

        Vector3 forearmDir = forearmTracker.TransformDirection(forearmTrackerAxis).normalized;
        Vector3 upperArmDir = upperArmTracker.TransformDirection(upperArmTrackerAxis).normalized;

        float elbowFlexion = 180f - Vector3.Angle(forearmDir, upperArmDir);
        Vector3 planeNormal = frameSource == ShoulderFrameSource.World ? Vector3.up : shoulderRefRot * Vector3.up;
        Vector3 referenceForward = frameSource == ShoulderFrameSource.World ? Vector3.forward : shoulderRefRot * Vector3.forward;
        float shoulderAbduction = SignedAngleOnPlane(upperArmDir, planeNormal, referenceForward);

        _wristPose = wristPose;
        _elbowPose = new Pose(wristPose.position + forearmDir * forearmLength, Quaternion.LookRotation(forearmDir));
        _shoulderPose = new Pose(_elbowPose.position + upperArmDir * upperArmLength, Quaternion.LookRotation(upperArmDir));
        _shoulderReferencePose = new Pose(referencePosition, shoulderRefRot);
        _hasShoulderReferencePose = frameSource != ShoulderFrameSource.World;

        if (updateUI)
        {
            WriteLabels(wristAngles, elbowAngles, shoulderAngles, elbowFlexion, shoulderAbduction, true);
        }

    WriteStatus(forearmTracked, upperArmTracked, torsoTracked, headsetTracked, handTrackingAvailable, frameSource);
    }

    /// <summary>
    /// Call from UI to capture the neutral pose (e.g., anatomical reference position).
    /// </summary>
    [ContextMenu("Calibrate From Current Pose")]
    public void CalibrateFromCurrentPose()
    {
        if (!forearmTracker || !upperArmTracker)
        {
            Debug.LogWarning("UpperLimbKinematicsUI calibration failed: tracker references are missing.");
            return;
        }

        if (!TryGetJointLocations(out XrHandJointLocationEXT[] joints))
        {
            Debug.LogWarning("UpperLimbKinematicsUI calibration failed: hand joints not available.");
            return;
        }

        ref XrHandJointLocationEXT wristJoint = ref joints[(int)XrHandJointEXT.XR_HAND_JOINT_WRIST_EXT];
        if (!IsJointTracked(wristJoint))
        {
            Debug.LogWarning("UpperLimbKinematicsUI calibration failed: wrist pose not available.");
            return;
        }

        bool torsoTracked = IsTrackerTracked(torsoTracker);
        bool headsetTracked = IsTrackerTracked(shoulderReference);

        Pose wristPose = BuildPose(wristJoint);
        Quaternion forearmRot = forearmTracker.rotation;
        Quaternion upperArmRot = upperArmTracker.rotation;
        ShoulderFrameSource frameSource = ResolveShoulderFrame(torsoTracked, headsetTracked, out Quaternion shoulderRefRot, out Vector3 referencePosition);

        _wristNeutral = Quaternion.Inverse(forearmRot) * wristPose.rotation;
        _elbowNeutral = Quaternion.Inverse(upperArmRot) * forearmRot;
        _shoulderNeutral = frameSource == ShoulderFrameSource.World ? upperArmRot : Quaternion.Inverse(shoulderRefRot) * upperArmRot;
        _shoulderReferencePose = new Pose(referencePosition, shoulderRefRot);
        _hasShoulderReferencePose = frameSource != ShoulderFrameSource.World;

        _hasCalibration = true;
    }

    public void ClearCalibration()
    {
    _hasCalibration = false;
    _wristNeutral = Quaternion.identity;
    _elbowNeutral = Quaternion.identity;
    _shoulderNeutral = Quaternion.identity;
    _wristRelativeRotation = Quaternion.identity;
    _elbowRelativeRotation = Quaternion.identity;
    _shoulderRelativeRotation = Quaternion.identity;
    }

    private void WriteLabels(Vector3 wristAngles, Vector3 elbowAngles, Vector3 shoulderAngles, float elbowFlexion, float shoulderAbduction, bool hasData)
    {
        if (wristLabel)
        {
            wristLabel.text = hasData
                ? string.Format(
                    "Bilek Δ (deg)\nPitch {0:+0.0;-0.0;0.0}  Yaw {1:+0.0;-0.0;0.0}  Roll {2:+0.0;-0.0;0.0}",
                    wristAngles.x, wristAngles.y, wristAngles.z)
                : "Bilek Δ (deg)\nVeri yok";
        }

        if (elbowLabel)
        {
            elbowLabel.text = hasData
                ? string.Format(
                    "Dirsek Δ (deg)\nPitch {0:+0.0;-0.0;0.0}  Yaw {1:+0.0;-0.0;0.0}  Roll {2:+0.0;-0.0;0.0}\nFleksiyon {3:0.0}°",
                    elbowAngles.x, elbowAngles.y, elbowAngles.z, elbowFlexion)
                : "Dirsek Δ (deg)\nVeri yok";
        }

        if (shoulderLabel)
        {
            shoulderLabel.text = hasData
                ? string.Format(
                    "Omuz Δ (deg)\nPitch {0:+0.0;-0.0;0.0}  Yaw {1:+0.0;-0.0;0.0}  Roll {2:+0.0;-0.0;0.0}\nAbdüksiyon {3:+0.0;-0.0;0.0}°",
                    shoulderAngles.x, shoulderAngles.y, shoulderAngles.z, shoulderAbduction)
                : "Omuz Δ (deg)\nVeri yok";
        }
    }

    private bool TryGetJointLocations(out XrHandJointLocationEXT[] joints)
    {
        joints = null;
        ViveHandTracking feature = ResolveHandTracking();
        if (feature == null)
        {
            return false;
        }

        bool isLeft = handNode == XRNode.LeftHand;
        if (!feature.GetJointLocations(isLeft, out joints))
        {
            return false;
        }

        return joints != null;
    }

    private ShoulderFrameSource ResolveShoulderFrame(bool torsoTracked, bool headsetTracked, out Quaternion rotation, out Vector3 position)
    {
        if (torsoTracked && torsoTracker)
        {
            rotation = torsoTracker.rotation;
            position = torsoTracker.position;
            return ShoulderFrameSource.TorsoTracker;
        }

        if (headsetTracked && shoulderReference)
        {
            rotation = shoulderReference.rotation;
            position = shoulderReference.position;
            return ShoulderFrameSource.Headset;
        }

        rotation = Quaternion.identity;
        position = Vector3.zero;
        return ShoulderFrameSource.World;
    }

    private bool IsTrackerTracked(Transform tracker)
    {
        if (!tracker)
        {
            return false;
        }

        TrackedPoseDriver driver = tracker.GetComponent<TrackedPoseDriver>();
        if (driver == null)
        {
            return tracker.gameObject.activeInHierarchy;
        }

        if (driver.ignoreTrackingState)
        {
            return true;
        }

        InputAction action = driver.trackingStateInput.action;
        if (action == null)
        {
            return true;
        }

        if (!action.enabled)
        {
            return action.controls.Count == 0;
        }

        try
        {
            int trackingState = action.ReadValue<int>();
            return (trackingState & 0x3) != 0;
        }
        catch
        {
            return true;
        }
    }

    public bool TryGetShoulderReference(out Pose pose)
    {
        if (_hasShoulderReferencePose)
        {
            pose = _shoulderReferencePose;
            return true;
        }

        pose = new Pose(Vector3.zero, Quaternion.identity);
        return false;
    }

    private void WriteStatus(bool forearmTracked, bool upperArmTracked, bool torsoTracked, bool headsetTracked, bool handTrackingAvailable, ShoulderFrameSource frameSource)
    {
        if (!statusLabel)
        {
            return;
        }

        _statusBuilder.Clear();
        _statusBuilder.Append("Önkol tracker: ").Append(forearmTracked ? "Takipte" : "- - -").Append('\n');
        _statusBuilder.Append("Üst kol tracker: ").Append(upperArmTracked ? "Takipte" : "- - -").Append('\n');
        _statusBuilder.Append("Gövde tracker: ").Append(torsoTracked ? "Takipte" : "Yok").Append('\n');
        _statusBuilder.Append("HMD referansı: ");
        if (shoulderReference)
        {
            _statusBuilder.Append(headsetTracked ? "Takipte" : "Takipsiz");
        }
        else
        {
            _statusBuilder.Append("Tanımlı değil");
        }
        _statusBuilder.Append('\n');
        _statusBuilder.Append("Omuz referansı: ");
        switch (frameSource)
        {
            case ShoulderFrameSource.TorsoTracker:
                _statusBuilder.Append("Torso tracker");
                break;
            case ShoulderFrameSource.Headset:
                _statusBuilder.Append(headsetTracked ? "HMD" : "HMD (takipsiz)");
                break;
            default:
                _statusBuilder.Append("Dünya ekseni");
                break;
        }
        _statusBuilder.Append('\n');
    _statusBuilder.Append("El takibi: ");
    _statusBuilder.Append(handTrackingAvailable ? "Var" : "Kapalı");
    _statusBuilder.Append('\n');
        _statusBuilder.Append("Kalibrasyon: ").Append(_hasCalibration ? "Aktif" : "Yok");

        statusLabel.text = _statusBuilder.ToString();
    }

    private ViveHandTracking ResolveHandTracking()
    {
        if (_handTracking != null && _handTracking.enabled)
        {
            return _handTracking;
        }

        OpenXRSettings settings = OpenXRSettings.Instance;
        _handTracking = settings != null ? settings.GetFeature<ViveHandTracking>() : null;
        return (_handTracking != null && _handTracking.enabled) ? _handTracking : null;
    }

    private void ValidateAssignments()
    {
        if (!forearmTracker)
        {
            Debug.LogWarning("UpperLimbKinematicsUI: Forearm tracker is not assigned.");
        }

        if (!upperArmTracker)
        {
            Debug.LogWarning("UpperLimbKinematicsUI: Upper-arm tracker is not assigned.");
        }

        if (!wristLabel || !elbowLabel || !shoulderLabel)
        {
            Debug.LogWarning("UpperLimbKinematicsUI: Assign TextMeshPro labels to see angle outputs.");
        }

        if (!statusLabel)
        {
            Debug.LogWarning("UpperLimbKinematicsUI: Assign a status TextMeshPro label to see tracker diagnostics.");
        }
    }

    private void ResetSolvedData()
    {
        _wristPose = default;
        _elbowPose = default;
        _shoulderPose = default;
        _wristRelativeRotation = Quaternion.identity;
        _elbowRelativeRotation = Quaternion.identity;
        _shoulderRelativeRotation = Quaternion.identity;
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        euler.x = Mathf.DeltaAngle(0f, euler.x);
        euler.y = Mathf.DeltaAngle(0f, euler.y);
        euler.z = Mathf.DeltaAngle(0f, euler.z);
        return euler;
    }

    private static float SignedAngleOnPlane(Vector3 direction, Vector3 planeNormal, Vector3 reference)
    {
        Vector3 projected = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        Vector3 projectedReference = Vector3.ProjectOnPlane(reference, planeNormal).normalized;
        if (projected.sqrMagnitude < Mathf.Epsilon || projectedReference.sqrMagnitude < Mathf.Epsilon)
        {
            return 0f;
        }

        float unsigned = Vector3.Angle(projectedReference, projected);
        float sign = Mathf.Sign(Vector3.Dot(planeNormal, Vector3.Cross(projectedReference, projected)));
        return unsigned * sign;
    }

    private static bool IsJointTracked(in XrHandJointLocationEXT joint)
    {
        const XrSpaceLocationFlags required = XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_TRACKED_BIT | XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT;
        return (joint.locationFlags & required) == required;
    }

    private static Pose BuildPose(in XrHandJointLocationEXT joint)
    {
        return new Pose(ToUnityVector(joint.pose.position), ToUnityQuaternion(joint.pose.orientation));
    }

    private static Vector3 ToUnityVector(XrVector3f value)
    {
        return new Vector3(value.x, value.y, -value.z);
    }

    private static Quaternion ToUnityQuaternion(XrQuaternionf value)
    {
        return new Quaternion(value.x, value.y, -value.z, -value.w);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(_wristPose.position, 0.01f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(_elbowPose.position, 0.012f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_shoulderPose.position, 0.014f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(_wristPose.position, _elbowPose.position);
        Gizmos.DrawLine(_elbowPose.position, _shoulderPose.position);
    }
#endif
}
