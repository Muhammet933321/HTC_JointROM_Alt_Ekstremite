using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple Play Mode driver to test FullBodyIK targets without live XR trackers.
/// Attach to any GameObject and assign the same IK targets used by FullBodyIKSolver.
/// </summary>
[DisallowMultipleComponent]
public class FullBodyIKEditorTestDriver : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private Transform headTarget;
    [SerializeField] private Transform pelvisTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;
    [SerializeField] private Transform leftKneeHintTarget;
    [SerializeField] private Transform rightKneeHintTarget;
    [SerializeField] private Transform leftElbowHintTarget;
    [SerializeField] private Transform rightElbowHintTarget;

    [Header("Optional Integration")]
    [SerializeField] private FullBodyTrackingManager trackingManager;
    [SerializeField] private FullBodyIKSolver ikSolver;
    [Tooltip("If true, the test driver only runs when tracker assignment is not active.")]
    [SerializeField] private bool runOnlyWhenTrackingIsMissing = true;
    [Tooltip("If true, calls Calibrate on start so editor simulation can run immediately.")]
    [SerializeField] private bool autoCalibrateOnStart = true;
    [Tooltip("If true, snap IK targets to avatar bones before caching base pose.")]
    [SerializeField] private bool snapTargetsToAvatarOnStart = true;
    [Tooltip("Hotkey for runtime recover: snap + cache + calibrate.")]
    [SerializeField] private Key recoverKey = Key.P;
    [Tooltip("Hard reset to startup neutral target pose.")]
    [SerializeField] private Key hardResetKey = Key.O;

    [Header("Simulation")]
    [SerializeField] private bool enableSimulation = true;
    [SerializeField] private float pelvisMoveSpeed = 0.6f;
    [SerializeField] private float pelvisYawSpeed = 90f;
    [SerializeField] private float limbMoveSpeed = 0.4f;
    [SerializeField] private float footVerticalRange = 0.45f;
    [SerializeField] private float handVerticalRange = 0.5f;
    [SerializeField] private float kneeHintForwardOffset = 0.22f;
    [SerializeField] private float elbowHintForwardOffset = -0.15f;
    [SerializeField] private float elbowHintSideOffset = 0.18f;

    private Pose _headBase;
    private Pose _pelvisBase;
    private Pose _leftHandBase;
    private Pose _rightHandBase;
    private Pose _leftFootBase;
    private Pose _rightFootBase;
    private Vector3 _leftKneeBase;
    private Vector3 _rightKneeBase;
    private Vector3 _leftElbowBase;
    private Vector3 _rightElbowBase;

    private float _leftFootVertical;
    private float _rightFootVertical;
    private float _leftHandVertical;
    private float _rightHandVertical;
    private float _pelvisYaw;
    private Vector3 _pelvisOffset;

    private Pose _headNeutral;
    private Pose _pelvisNeutral;
    private Pose _leftHandNeutral;
    private Pose _rightHandNeutral;
    private Pose _leftFootNeutral;
    private Pose _rightFootNeutral;
    private Vector3 _leftKneeNeutral;
    private Vector3 _rightKneeNeutral;
    private Vector3 _leftElbowNeutral;
    private Vector3 _rightElbowNeutral;
    private bool _neutralPoseCached;

    private void Start()
    {
        if (snapTargetsToAvatarOnStart && ikSolver != null)
        {
            ikSolver.SnapTargetsToCurrentBones();
        }

        CacheBasePose();
        CacheNeutralPose();

        if (autoCalibrateOnStart && ikSolver != null)
        {
            ikSolver.Calibrate();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (!enableSimulation) return;
        if (runOnlyWhenTrackingIsMissing && trackingManager != null && trackingManager.IsAssigned) return;

        float dt = Time.deltaTime;

        if (IsPressedThisFrame(keyboard, recoverKey))
        {
            RecoverPoseAndCalibration();
        }

        if (IsPressedThisFrame(keyboard, hardResetKey))
        {
            RestoreNeutralPoseAndRecalibrate();
        }

        if (IsPressedThisFrame(keyboard, Key.T))
        {
            enableSimulation = !enableSimulation;
        }

        if (!enableSimulation) return;

        // Pelvis translation (WASD + RF) and yaw (Q/E)
        Vector3 move = Vector3.zero;
        if (IsPressed(keyboard, Key.W)) move += Vector3.forward;
        if (IsPressed(keyboard, Key.S)) move += Vector3.back;
        if (IsPressed(keyboard, Key.A)) move += Vector3.left;
        if (IsPressed(keyboard, Key.D)) move += Vector3.right;
        if (IsPressed(keyboard, Key.R)) move += Vector3.up;
        if (IsPressed(keyboard, Key.F)) move += Vector3.down;

        if (move.sqrMagnitude > 0f)
        {
            _pelvisOffset += move.normalized * pelvisMoveSpeed * dt;
        }

        float yawInput = 0f;
        if (IsPressed(keyboard, Key.Q)) yawInput -= 1f;
        if (IsPressed(keyboard, Key.E)) yawInput += 1f;
        _pelvisYaw += yawInput * pelvisYawSpeed * dt;

        // Left foot lift: Z/X, Right foot lift: C/V
        _leftFootVertical = Mathf.Clamp(_leftFootVertical + GetAxis(keyboard, Key.Z, Key.X) * limbMoveSpeed * dt, -1f, 1f);
        _rightFootVertical = Mathf.Clamp(_rightFootVertical + GetAxis(keyboard, Key.C, Key.V) * limbMoveSpeed * dt, -1f, 1f);

        // Left hand lift: U/J, Right hand lift: I/K
        _leftHandVertical = Mathf.Clamp(_leftHandVertical + GetAxis(keyboard, Key.J, Key.U) * limbMoveSpeed * dt, -1f, 1f);
        _rightHandVertical = Mathf.Clamp(_rightHandVertical + GetAxis(keyboard, Key.K, Key.I) * limbMoveSpeed * dt, -1f, 1f);

        ApplyPose();
    }

    [ContextMenu("Recover Pose And Calibration")]
    public void RecoverPoseAndCalibration()
    {
        if (ikSolver != null)
        {
            ikSolver.SnapTargetsToCurrentBones();
        }

        CacheBasePose();

        if (ikSolver != null)
        {
            ikSolver.Calibrate();
        }

        CacheNeutralPose();
    }

    [ContextMenu("Restore Neutral Pose And Recalibrate")]
    public void RestoreNeutralPoseAndRecalibrate()
    {
        if (_neutralPoseCached)
        {
            SetPose(headTarget, _headNeutral);
            SetPose(pelvisTarget, _pelvisNeutral);
            SetPose(leftHandTarget, _leftHandNeutral);
            SetPose(rightHandTarget, _rightHandNeutral);
            SetPose(leftFootTarget, _leftFootNeutral);
            SetPose(rightFootTarget, _rightFootNeutral);

            if (leftKneeHintTarget) leftKneeHintTarget.position = _leftKneeNeutral;
            if (rightKneeHintTarget) rightKneeHintTarget.position = _rightKneeNeutral;
            if (leftElbowHintTarget) leftElbowHintTarget.position = _leftElbowNeutral;
            if (rightElbowHintTarget) rightElbowHintTarget.position = _rightElbowNeutral;
        }

        CacheBasePose();

        if (ikSolver != null)
        {
            ikSolver.Calibrate();
        }
    }

    [ContextMenu("Cache Base Pose")]
    public void CacheBasePose()
    {
        _headBase = GetPose(headTarget);
        _pelvisBase = GetPose(pelvisTarget);
        _leftHandBase = GetPose(leftHandTarget);
        _rightHandBase = GetPose(rightHandTarget);
        _leftFootBase = GetPose(leftFootTarget);
        _rightFootBase = GetPose(rightFootTarget);

        _leftKneeBase = leftKneeHintTarget ? leftKneeHintTarget.position : Vector3.zero;
        _rightKneeBase = rightKneeHintTarget ? rightKneeHintTarget.position : Vector3.zero;
        _leftElbowBase = leftElbowHintTarget ? leftElbowHintTarget.position : Vector3.zero;
        _rightElbowBase = rightElbowHintTarget ? rightElbowHintTarget.position : Vector3.zero;

        _leftFootVertical = 0f;
        _rightFootVertical = 0f;
        _leftHandVertical = 0f;
        _rightHandVertical = 0f;
        _pelvisYaw = 0f;
        _pelvisOffset = Vector3.zero;
    }

    [ContextMenu("Cache Neutral Pose")]
    public void CacheNeutralPose()
    {
        _headNeutral = GetPose(headTarget);
        _pelvisNeutral = GetPose(pelvisTarget);
        _leftHandNeutral = GetPose(leftHandTarget);
        _rightHandNeutral = GetPose(rightHandTarget);
        _leftFootNeutral = GetPose(leftFootTarget);
        _rightFootNeutral = GetPose(rightFootTarget);
        _leftKneeNeutral = leftKneeHintTarget ? leftKneeHintTarget.position : Vector3.zero;
        _rightKneeNeutral = rightKneeHintTarget ? rightKneeHintTarget.position : Vector3.zero;
        _leftElbowNeutral = leftElbowHintTarget ? leftElbowHintTarget.position : Vector3.zero;
        _rightElbowNeutral = rightElbowHintTarget ? rightElbowHintTarget.position : Vector3.zero;
        _neutralPoseCached = true;
    }

    private void ApplyPose()
    {
        Quaternion pelvisYawRot = Quaternion.Euler(0f, _pelvisYaw, 0f);

        if (pelvisTarget)
        {
            pelvisTarget.SetPositionAndRotation(
                _pelvisBase.position + _pelvisOffset,
                pelvisYawRot * _pelvisBase.rotation);
        }

        if (headTarget)
        {
            Vector3 headPos = _headBase.position + _pelvisOffset;
            headTarget.SetPositionAndRotation(headPos, pelvisYawRot * _headBase.rotation);
        }

        if (leftFootTarget)
        {
            Vector3 footPos = _leftFootBase.position + _pelvisOffset + Vector3.up * (_leftFootVertical * footVerticalRange);
            leftFootTarget.SetPositionAndRotation(footPos, pelvisYawRot * _leftFootBase.rotation);
        }

        if (rightFootTarget)
        {
            Vector3 footPos = _rightFootBase.position + _pelvisOffset + Vector3.up * (_rightFootVertical * footVerticalRange);
            rightFootTarget.SetPositionAndRotation(footPos, pelvisYawRot * _rightFootBase.rotation);
        }

        if (leftHandTarget)
        {
            Vector3 handPos = _leftHandBase.position + _pelvisOffset + Vector3.up * (_leftHandVertical * handVerticalRange);
            leftHandTarget.SetPositionAndRotation(handPos, pelvisYawRot * _leftHandBase.rotation);
        }

        if (rightHandTarget)
        {
            Vector3 handPos = _rightHandBase.position + _pelvisOffset + Vector3.up * (_rightHandVertical * handVerticalRange);
            rightHandTarget.SetPositionAndRotation(handPos, pelvisYawRot * _rightHandBase.rotation);
        }

        if (leftKneeHintTarget)
        {
            Vector3 hint = _leftKneeBase + _pelvisOffset + pelvisYawRot * (Vector3.forward * kneeHintForwardOffset);
            leftKneeHintTarget.position = hint;
        }

        if (rightKneeHintTarget)
        {
            Vector3 hint = _rightKneeBase + _pelvisOffset + pelvisYawRot * (Vector3.forward * kneeHintForwardOffset);
            rightKneeHintTarget.position = hint;
        }

        if (leftElbowHintTarget)
        {
            Vector3 leftOffset = (Vector3.forward * elbowHintForwardOffset) + (Vector3.left * elbowHintSideOffset);
            leftElbowHintTarget.position = _leftElbowBase + _pelvisOffset + pelvisYawRot * leftOffset;
        }

        if (rightElbowHintTarget)
        {
            Vector3 rightOffset = (Vector3.forward * elbowHintForwardOffset) + (Vector3.right * elbowHintSideOffset);
            rightElbowHintTarget.position = _rightElbowBase + _pelvisOffset + pelvisYawRot * rightOffset;
        }
    }

    private static float GetAxis(Keyboard keyboard, Key negative, Key positive)
    {
        float value = 0f;
        if (IsPressed(keyboard, negative)) value -= 1f;
        if (IsPressed(keyboard, positive)) value += 1f;
        return value;
    }

    private static bool IsPressed(Keyboard keyboard, Key key)
    {
        var control = keyboard[key];
        return control != null && control.isPressed;
    }

    private static bool IsPressedThisFrame(Keyboard keyboard, Key key)
    {
        var control = keyboard[key];
        return control != null && control.wasPressedThisFrame;
    }

    private static Pose GetPose(Transform t)
    {
        return t ? new Pose(t.position, t.rotation) : new Pose(Vector3.zero, Quaternion.identity);
    }

    private static void SetPose(Transform t, Pose pose)
    {
        if (!t) return;
        t.SetPositionAndRotation(pose.position, pose.rotation);
    }
}
