using UnityEngine;

/// <summary>
/// Lightweight simulator that generates synthetic shoulder, elbow, and wrist poses
/// so the visualizer can be tested without live tracker data.
/// </summary>
[DisallowMultipleComponent]
public class UpperLimbKinematicsTestSource : MonoBehaviour, IUpperLimbKinematicsSource
{
    [Header("Reference frame")]
    [Tooltip("Optional transform representing the torso reference (if omitted, uses this GameObject).")]
    [SerializeField] private Transform referenceRoot;
    [Tooltip("Local offset from the reference root to the shoulder pivot (metres).")]
    [SerializeField] private Vector3 shoulderLocalOffset = new Vector3(0.18f, 0.18f, 0f);
    [Tooltip("Local direction (in reference root space) that points from shoulder to elbow in the neutral pose.")]
    [SerializeField] private Vector3 shoulderLocalForward = Vector3.right;
    [Tooltip("Local up direction defining the shoulder reference frame.")]
    [SerializeField] private Vector3 shoulderLocalUp = Vector3.up;

    [Header("Segment lengths (metres)")]
    [SerializeField, Min(0.05f)] private float upperArmLength = 0.30f;
    [SerializeField, Min(0.05f)] private float forearmLength = 0.26f;

    [Header("Motion profile")]
    [Tooltip("Base elbow flexion angle in degrees.")]
    [SerializeField] private float elbowBaseAngle = 60f;
    [Tooltip("Peak-to-peak variation applied to elbow flexion.")]
    [SerializeField] private float elbowSwingAmplitude = 40f;
    [Tooltip("Peak-to-peak shoulder yaw (horizontal) swing in degrees.")]
    [SerializeField] private float shoulderYawAmplitude = 45f;
    [Tooltip("Peak-to-peak shoulder pitch (vertical) swing in degrees.")]
    [SerializeField] private float shoulderPitchAmplitude = 20f;
    [Tooltip("Oscillation speed in cycles per second.")]
    [SerializeField] private float frequency = 0.33f;

    private Pose _wristPose;
    private Pose _elbowPose;
    private Pose _shoulderPose;
    private Pose _referencePose;
    private Vector3 _shoulderBasePosition;

    private Vector3 _baseForward;
    private Vector3 _baseUp;
    private float _time;
    private Vector3 _cachedShoulderUp = Vector3.up;
    private Vector3 _cachedForearmUp = Vector3.up;

    public Pose WristPose => _wristPose;
    public Pose ElbowPose => _elbowPose;
    public Pose ShoulderPose => _shoulderPose;
    public Quaternion WristRelativeRotation => _wristRelativeRotation;
    public Quaternion ElbowRelativeRotation => _elbowRelativeRotation;
    public Quaternion ShoulderRelativeRotation => _shoulderRelativeRotation;
    private Quaternion _shoulderRelativeRotation = Quaternion.identity;
    private Quaternion _elbowRelativeRotation = Quaternion.identity;
    private Quaternion _wristRelativeRotation = Quaternion.identity;
    private Quaternion _shoulderNeutralRotation = Quaternion.identity;
    private Quaternion _elbowNeutralRotation = Quaternion.identity;
    private Quaternion _wristNeutralRotation = Quaternion.identity;

    private void Awake()
    {
        RecalculateReferenceSnapshot();
    }

    private void OnValidate()
    {
        RecalculateReferenceSnapshot();
        if (!Application.isPlaying)
        {
            _time = 0f;
        }
    }

    private void Update()
    {
        Transform origin = GetOrigin();
        if (origin.hasChanged)
        {
            RecalculateReferenceSnapshot();
            origin.hasChanged = false;
        }

        _time += Time.deltaTime * Mathf.PI * 2f * Mathf.Max(frequency, 0.01f);

        // Shoulder swing relative to stored neutral axes.
        float shoulderYaw = Mathf.Sin(_time) * (shoulderYawAmplitude * 0.5f);
        float shoulderPitch = Mathf.Sin(_time * 0.5f) * (shoulderPitchAmplitude * 0.5f);

        Quaternion yawRotation = Quaternion.AngleAxis(shoulderYaw, _baseUp);
        Vector3 pitchAxis = Vector3.Cross(_baseUp, _baseForward);
        if (pitchAxis.sqrMagnitude < 1e-4f)
        {
            pitchAxis = Vector3.right;
        }

        Quaternion pitchRotation = Quaternion.AngleAxis(shoulderPitch, pitchAxis.normalized);
        Quaternion shoulderSwing = yawRotation * pitchRotation;

        Vector3 upperDirection = (shoulderSwing * _baseForward).normalized;
        Vector3 shoulderUp = StabilizeUpVector(upperDirection, _baseUp, _baseForward, ref _cachedShoulderUp);
    Vector3 shoulderPosition = _shoulderBasePosition;
    Vector3 elbowPosition = shoulderPosition + upperDirection * upperArmLength;

        float elbowFlex = elbowBaseAngle + Mathf.Sin(_time * 1.3f) * (elbowSwingAmplitude * 0.5f);
        Vector3 flexAxis = Vector3.Cross(upperDirection, shoulderUp).normalized;
        if (flexAxis.sqrMagnitude < 1e-4f)
        {
            flexAxis = _baseForward;
        }

        Quaternion elbowRotationOffset = Quaternion.AngleAxis(elbowFlex, flexAxis);
        Vector3 forearmDirection = (elbowRotationOffset * (-upperDirection)).normalized;
        Vector3 forearmUp = StabilizeUpVector(forearmDirection, shoulderUp, Vector3.Cross(shoulderUp, forearmDirection), ref _cachedForearmUp);
        Vector3 wristPosition = elbowPosition + forearmDirection * forearmLength;

        Quaternion shoulderRotation = Quaternion.LookRotation(upperDirection, shoulderUp);
        Quaternion elbowRotation = Quaternion.LookRotation(forearmDirection, forearmUp);
        Quaternion wristRotation = elbowRotation;

    _shoulderPose = new Pose(shoulderPosition, shoulderRotation);
    _elbowPose = new Pose(elbowPosition, elbowRotation);
    _wristPose = new Pose(wristPosition, wristRotation);
        _shoulderRelativeRotation = Quaternion.Inverse(_shoulderNeutralRotation) * shoulderRotation;
        _elbowRelativeRotation = Quaternion.Inverse(_elbowNeutralRotation) * elbowRotation;
        _wristRelativeRotation = Quaternion.Inverse(_wristNeutralRotation) * wristRotation;
    }

    public bool TryGetShoulderReference(out Pose pose)
    {
        pose = _referencePose;
        return true;
    }

    [ContextMenu("Recalculate Reference Snapshot")]
    private void RecalculateReferenceSnapshot()
    {
    Transform origin = GetOrigin();

    Quaternion originRotation = origin.rotation;
    Vector3 originPosition = origin.position;
    Vector3 offset = origin.TransformVector(shoulderLocalOffset);
    Vector3 shoulderPosition = originPosition + offset;

    Vector3 forward = origin.TransformDirection(shoulderLocalForward);
    Vector3 up = origin.TransformDirection(shoulderLocalUp);

        if (forward.sqrMagnitude < 1e-4f)
        {
            forward = Vector3.right;
        }

        if (up.sqrMagnitude < 1e-4f)
        {
            up = Vector3.up;
        }

        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude < 1e-4f)
        {
            forward = Vector3.Cross(up, Vector3.forward);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.right;
            }
        }

        _baseForward = forward.normalized;
        _baseUp = up.normalized;
        _shoulderBasePosition = shoulderPosition;
        _referencePose = new Pose(originPosition, originRotation);
        _cachedShoulderUp = _baseUp;
        _cachedForearmUp = _baseUp;
        _shoulderNeutralRotation = Quaternion.LookRotation(_baseForward, _baseUp);
        Vector3 neutralFlexAxis = Vector3.Cross(_baseUp, _baseForward);
        if (neutralFlexAxis.sqrMagnitude < 1e-4f)
        {
            neutralFlexAxis = Vector3.right;
        }

        Vector3 neutralForearmDir = Quaternion.AngleAxis(elbowBaseAngle, neutralFlexAxis.normalized) * (-_baseForward);
        _elbowNeutralRotation = Quaternion.LookRotation(neutralForearmDir, _baseUp);
        _wristNeutralRotation = _elbowNeutralRotation;
        _shoulderRelativeRotation = Quaternion.identity;
        _elbowRelativeRotation = Quaternion.identity;
        _wristRelativeRotation = Quaternion.identity;
    }

    private static Vector3 StabilizeUpVector(Vector3 forward, Vector3 primaryUp, Vector3 backupAxis, ref Vector3 cachedUp)
    {
        Vector3 up = Vector3.ProjectOnPlane(primaryUp, forward).normalized;
        if (up.sqrMagnitude < 1e-4f)
        {
            up = Vector3.ProjectOnPlane(backupAxis, forward).normalized;
        }

        if (up.sqrMagnitude < 1e-4f)
        {
            up = cachedUp;
        }

        float alignment = Vector3.Dot(up, cachedUp);
        if (alignment < 0f)
        {
            up = -up;
        }

        cachedUp = Vector3.Slerp(cachedUp, up, 0.5f).normalized;
        if (cachedUp.sqrMagnitude < 1e-4f)
        {
            cachedUp = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.up;
        }

        return cachedUp;
    }

    private Transform GetOrigin()
    {
        if (referenceRoot == null)
        {
            return transform;
        }

        // If origin is a child of this component, we want the animation relative
        // to the parent's stable frame instead of stacking motion.
        if (referenceRoot.IsChildOf(transform) && transform.parent != null)
        {
            return transform.parent;
        }

        return referenceRoot;
    }
}
