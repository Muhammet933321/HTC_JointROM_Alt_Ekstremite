using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes real-time lower-limb biomechanical metrics from tracker world positions.
/// Supports 1-5 trackers; missing trackers are tolerated gracefully.
/// Can run fully in editor without hardware via useSimulatedInput.
///
/// Placement expectations:
///   pelvisTracker     – posterior superior iliac spine level
///   leftKneeTracker   – lateral/anterior knee (patella area)
///   rightKneeTracker  – lateral/anterior knee (patella area)
///   leftAnkleTracker  – lateral malleolus level
///   rightAnkleTracker – lateral malleolus level
/// </summary>
[DefaultExecutionOrder(200)]
public class LowerLimbBiometrics : MonoBehaviour
{
    // ───────────────────────── Tracker References ─────────────────────────

    [Header("=== Tracker Transforms (assign in Inspector) ===")]
    [Tooltip("Pelvis / hip tracker (1 shared for both sides).")]
    public Transform pelvisTracker;

    [Tooltip("Left knee tracker. If null, left valgus is not computed.")]
    public Transform leftKneeTracker;

    [Tooltip("Right knee tracker. If null, right valgus is not computed.")]
    public Transform rightKneeTracker;

    [Tooltip("Left ankle tracker. If null, left valgus is not computed.")]
    public Transform leftAnkleTracker;

    [Tooltip("Right ankle tracker. If null, right valgus is not computed.")]
    public Transform rightAnkleTracker;

    [Header("=== Avatar Bone Fallback (3 Tracker / IK) ===")]
    [Tooltip("If knee trackers are missing, use the calibrated IK avatar bones as the angle source.")]
    public bool useAvatarBoneFallback = true;

    [Tooltip("Avatar pelvis/hips bone. Used for pelvis sway and body forward fallback.")]
    public Transform avatarPelvisBone;

    [Tooltip("Left upper-leg/hip bone, e.g. mixamorig:LeftUpLeg.")]
    public Transform leftHipBone;
    [Tooltip("Left knee/shin bone, e.g. mixamorig:LeftLeg.")]
    public Transform leftKneeBone;
    [Tooltip("Left ankle/foot bone, e.g. mixamorig:LeftFoot.")]
    public Transform leftAnkleBone;

    [Tooltip("Right upper-leg/hip bone, e.g. mixamorig:RightUpLeg.")]
    public Transform rightHipBone;
    [Tooltip("Right knee/shin bone, e.g. mixamorig:RightLeg.")]
    public Transform rightKneeBone;
    [Tooltip("Right ankle/foot bone, e.g. mixamorig:RightFoot.")]
    public Transform rightAnkleBone;

    // ───────────────────────── Simulated Input (Editor) ─────────────────────────

    [Header("=== Simulated Input (Editor / No Hardware) ===")]
    [Tooltip("When true, all computed angles use the simulated values below.")]
    public bool useSimulatedInput = false;

    [Range(-20f, 20f)]
    public float simLeftValgus = 0f;
    [Range(-20f, 20f)]
    public float simRightValgus = 0f;
    [Range(-10f, 90f)]
    public float simLeftKneeFlexion = 0f;
    [Range(-10f, 90f)]
    public float simRightKneeFlexion = 0f;
    [Range(0f, 40f)]
    public float simSwayRMS = 5f;
    [Range(0f, 20f)]
    public float simSwayVelocity = 2f;
    [Range(0f, 100f)]
    public float simLeftStanceAnteriorReachPct = 65f;
    [Range(0f, 100f)]
    public float simRightStanceAnteriorReachPct = 65f;

    // ───────────────────────── Settings ─────────────────────────

    [Header("=== Settings ===")]
    [Tooltip("Number of samples kept in the sway sliding window (90 Hz → 90 = 1 second).")]
    [SerializeField] private int swayWindowSize = 90;

    [Tooltip("Up axis used for frontal-plane projection. Leave at (0,1,0).")]
    [SerializeField] private Vector3 worldUp = Vector3.up;

    // ───────────────────────── Public Outputs ─────────────────────────

    /// <summary>
    /// Left dynamic knee valgus (+) / varus (−) in degrees.
    /// Positive = medial knee collapse (valgus). Computed from frontal plane projection.
    /// </summary>
    public float LeftValgusAngle { get; private set; }

    /// <summary>Right dynamic knee valgus (+) / varus (−) in degrees.</summary>
    public float RightValgusAngle { get; private set; }

    /// <summary>Left knee flexion in degrees (positive = flexion).</summary>
    public float LeftKneeFlexion { get; private set; }

    /// <summary>Right knee flexion in degrees (positive = flexion).</summary>
    public float RightKneeFlexion { get; private set; }

    /// <summary>Root-mean-square of pelvis XZ displacement over the sliding window (metres).</summary>
    public float PelvisSwayRMS { get; private set; }

    /// <summary>Mean pelvis XZ velocity over the last frame (m/s).</summary>
    public float SwayVelocity { get; private set; }

    /// <summary>
    /// Symmetry index between left and right knee flexion.
    /// SI = 100 × |XR − XL| / ((XR + XL) / 2). Returns 0 when denominator is near zero.
    /// </summary>
    public float SymmetryIndex { get; private set; }

    /// <summary>Modified Y-Balance anterior reach while standing on the left leg (% approximate leg length).</summary>
    public float LeftStanceAnteriorReachPct { get; private set; }

    /// <summary>Modified Y-Balance anterior reach while standing on the right leg (% approximate leg length).</summary>
    public float RightStanceAnteriorReachPct { get; private set; }

    /// <summary>True when both sides have hip-knee-ankle points from trackers or avatar fallback.</summary>
    public bool IsBilateralAvailable => IsLeftLegAvailable && IsRightLegAvailable;

    /// <summary>True when the left side has enough points for valgus/flexion calculations.</summary>
    public bool IsLeftLegAvailable { get; private set; }

    /// <summary>True when the right side has enough points for valgus/flexion calculations.</summary>
    public bool IsRightLegAvailable { get; private set; }

    /// <summary>True when the left side used avatar bones instead of direct knee tracker data this frame.</summary>
    public bool IsUsingLeftAvatarFallback { get; private set; }

    /// <summary>True when the right side used avatar bones instead of direct knee tracker data this frame.</summary>
    public bool IsUsingRightAvatarFallback { get; private set; }

    /// <summary>Short runtime source summary for diagnostics and build validation.</summary>
    public string DataSourceSummaryTR { get; private set; } = "Kaynak bekleniyor";

    /// <summary>Rate of change of left valgus angle (degrees/second).</summary>
    public float LeftValgusAngularVelocity  { get; private set; }

    /// <summary>Rate of change of right valgus angle (degrees/second).</summary>
    public float RightValgusAngularVelocity { get; private set; }

    /// <summary>Rate of change of left valgus angular velocity (degrees/second²).</summary>
    public float LeftValgusAngularAcceleration  { get; private set; }

    /// <summary>Rate of change of right valgus angular velocity (degrees/second²).</summary>
    public float RightValgusAngularAcceleration { get; private set; }

    /// <summary>Rate of change of left valgus angular acceleration (degrees/second³).</summary>
    public float LeftValgusJerk  { get; private set; }

    /// <summary>Rate of change of right valgus angular acceleration (degrees/second³).</summary>
    public float RightValgusJerk { get; private set; }

    // ───────────────────────── Private State ─────────────────────────

    private readonly Queue<Vector3> _swayBuffer = new();
    private Vector3 _prevPelvisXZ;
    private bool _prevPelvisValid;
    private float _prevLeftValgus;
    private float _prevRightValgus;
    private bool  _prevValgusValid;
    private float _prevLeftAngVel, _prevRightAngVel;
    private bool  _prevAngVelValid;
    private float _prevLeftAngAccel, _prevRightAngAccel;
    private bool  _prevAngAccelValid;
    private float _lastValidLeftValgus, _lastValidRightValgus;
    private float _lastValidLeftFlex,   _lastValidRightFlex;
    private bool  _lastValgusValid,     _lastFlexValid;

    // ───────────────────────── Unity Lifecycle ─────────────────────────

    private void LateUpdate()
    {
        if (useSimulatedInput)
        {
            ApplySimulatedValues();
            return;
        }

        ComputeValgusAngles();
        ComputeValgusAngularVelocity();
        ComputeValgusAngularAcceleration();
        ComputeValgusJerk();
        ComputeKneeFlexion();
        ComputeSwayMetrics();
        ComputeSymmetryIndex();
        ComputeReachMetrics();
    }

    // ───────────────────────── Simulated Path ─────────────────────────

    private void ApplySimulatedValues()
    {
        IsLeftLegAvailable = true;
        IsRightLegAvailable = true;
        IsUsingLeftAvatarFallback = false;
        IsUsingRightAvatarFallback = false;
        DataSourceSummaryTR = "Simulasyon verisi";

        LeftValgusAngle = simLeftValgus;
        RightValgusAngle = simRightValgus;
        LeftKneeFlexion = simLeftKneeFlexion;
        RightKneeFlexion = simRightKneeFlexion;
        PelvisSwayRMS = simSwayRMS;
        SwayVelocity = simSwayVelocity;
        float mean = (simLeftKneeFlexion + simRightKneeFlexion) * 0.5f;
        SymmetryIndex = mean > 0.01f
            ? 100f * Mathf.Abs(simRightKneeFlexion - simLeftKneeFlexion) / mean
            : 0f;
        LeftStanceAnteriorReachPct = simLeftStanceAnteriorReachPct;
        RightStanceAnteriorReachPct = simRightStanceAnteriorReachPct;
        LeftValgusAngularVelocity  = 0f;
        RightValgusAngularVelocity = 0f;
        LeftValgusAngularAcceleration  = 0f;
        RightValgusAngularAcceleration = 0f;
        LeftValgusJerk  = 0f;
        RightValgusJerk = 0f;
    }

    // ───────────────────────── Valgus / Varus ─────────────────────────

    /// <summary>
    /// Method A — vectorial (geometric) approach (PDF §4.2).
    /// Vfemur = knee − pelvisHipCenter
    /// Vtibia = ankle − knee
    /// Both projected onto the frontal (coronal) plane, then signed angle computed.
    /// Sign: positive = valgus (medial knee), negative = varus (lateral knee).
    /// </summary>
    private void ComputeValgusAngles()
    {
        bool leftAvailable = TryGetLegPoints(true, out Vector3 leftHip, out Vector3 leftKnee, out Vector3 leftAnkle, out bool leftFallback);
        bool rightAvailable = TryGetLegPoints(false, out Vector3 rightHip, out Vector3 rightKnee, out Vector3 rightAnkle, out bool rightFallback);

        if (leftAvailable)
        {
            LeftValgusAngle = ComputeValgusForSide(leftHip, leftKnee, leftAnkle, isLeft: true);
            _lastValidLeftValgus = LeftValgusAngle;
            _lastValgusValid = true;
        }
        else
            LeftValgusAngle = _lastValgusValid ? _lastValidLeftValgus : 0f;

        if (rightAvailable)
        {
            RightValgusAngle = ComputeValgusForSide(rightHip, rightKnee, rightAnkle, isLeft: false);
            _lastValidRightValgus = RightValgusAngle;
            _lastValgusValid = true;
        }
        else
            RightValgusAngle = _lastValgusValid ? _lastValidRightValgus : 0f;

        IsLeftLegAvailable = leftAvailable;
        IsRightLegAvailable = rightAvailable;
        IsUsingLeftAvatarFallback = leftFallback;
        IsUsingRightAvatarFallback = rightFallback;
        UpdateDataSourceSummary();
    }

    private float ComputeValgusForSide(Vector3 hipCenter, Vector3 kneePos, Vector3 anklePos, bool isLeft)
    {
        Vector3 vFemur = kneePos - hipCenter;
        Vector3 vTibia = anklePos - kneePos;

        if (vFemur.sqrMagnitude < 1e-6f || vTibia.sqrMagnitude < 1e-6f) return 0f;

        // Project onto frontal plane (remove forward/back component, keep left/right + up/down)
        // Frontal plane normal ≈ forward axis of the person.
        // We use world forward as approximation; for full accuracy, pelvis tracker's forward would be used.
        Vector3 forward = GetForwardReference();

        Vector3 femurFrontal = Vector3.ProjectOnPlane(vFemur, forward).normalized;
        Vector3 tibiaFrontal = Vector3.ProjectOnPlane(vTibia, forward).normalized;

        if (femurFrontal.sqrMagnitude < 1e-6f || tibiaFrontal.sqrMagnitude < 1e-6f) return 0f;

        // Signed angle: cross-product with worldUp gives valgus/varus direction
        float angle = Vector3.SignedAngle(femurFrontal, tibiaFrontal, forward);

        // Convention: positive = tibia leans inward (valgus) for both sides
        return isLeft ? -angle : angle;
    }

    // ───────────────────────── Valgus Angular Velocity ─────────────────────────

    private void ComputeValgusAngularVelocity()
    {
        if (_prevValgusValid && Time.deltaTime > 0f)
        {
            LeftValgusAngularVelocity  = (LeftValgusAngle  - _prevLeftValgus)  / Time.deltaTime;
            RightValgusAngularVelocity = (RightValgusAngle - _prevRightValgus) / Time.deltaTime;
        }
        _prevLeftValgus  = LeftValgusAngle;
        _prevRightValgus = RightValgusAngle;
        _prevValgusValid = true;
    }

    private void ComputeValgusAngularAcceleration()
    {
        if (_prevAngVelValid && Time.deltaTime > 0f)
        {
            LeftValgusAngularAcceleration  = (LeftValgusAngularVelocity  - _prevLeftAngVel)  / Time.deltaTime;
            RightValgusAngularAcceleration = (RightValgusAngularVelocity - _prevRightAngVel) / Time.deltaTime;
        }
        _prevLeftAngVel  = LeftValgusAngularVelocity;
        _prevRightAngVel = RightValgusAngularVelocity;
        _prevAngVelValid = true;
    }

    private void ComputeValgusJerk()
    {
        if (_prevAngAccelValid && Time.deltaTime > 0f)
        {
            LeftValgusJerk  = (LeftValgusAngularAcceleration  - _prevLeftAngAccel)  / Time.deltaTime;
            RightValgusJerk = (RightValgusAngularAcceleration - _prevRightAngAccel) / Time.deltaTime;
        }
        _prevLeftAngAccel  = LeftValgusAngularAcceleration;
        _prevRightAngAccel = RightValgusAngularAcceleration;
        _prevAngAccelValid = true;
    }

    // ───────────────────────── Knee Flexion ─────────────────────────

    /// <summary>
    /// Sagittal plane flexion from tracker world positions.
    /// θ = arccos((Vfemur · Vtibia) / (|Vfemur| × |Vtibia|))
    /// 0° = fully extended, positive = flexed.
    /// </summary>
    private void ComputeKneeFlexion()
    {
        if (TryGetLegPoints(true, out Vector3 leftHip, out Vector3 leftKnee, out Vector3 leftAnkle, out _))
        {
            LeftKneeFlexion = ComputeFlexionForSide(leftHip, leftKnee, leftAnkle);
            _lastValidLeftFlex = LeftKneeFlexion;
            _lastFlexValid = true;
        }
        else
            LeftKneeFlexion = _lastFlexValid ? _lastValidLeftFlex : 0f;

        if (TryGetLegPoints(false, out Vector3 rightHip, out Vector3 rightKnee, out Vector3 rightAnkle, out _))
        {
            RightKneeFlexion = ComputeFlexionForSide(rightHip, rightKnee, rightAnkle);
            _lastValidRightFlex = RightKneeFlexion;
            _lastFlexValid = true;
        }
        else
            RightKneeFlexion = _lastFlexValid ? _lastValidRightFlex : 0f;
    }

    private static float ComputeFlexionForSide(Vector3 hipPos, Vector3 kneePos, Vector3 anklePos)
    {
        Vector3 vFemur = (kneePos - hipPos).normalized;
        Vector3 vTibia = (anklePos - kneePos).normalized;

        if (vFemur.sqrMagnitude < 1e-6f || vTibia.sqrMagnitude < 1e-6f) return 0f;

        float dot = Mathf.Clamp(Vector3.Dot(vFemur, vTibia), -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

        // When fully extended, femur and tibia point in same direction → angle ≈ 0°.
        // Flexion bends them apart → angle grows. Report as positive flexion.
        return angle;
    }

    // ───────────────────────── Sway Metrics ─────────────────────────

    private void ComputeSwayMetrics()
    {
        if (!TryGetPelvisPoint(out Vector3 pelvisPosition))
        {
            PelvisSwayRMS = 0f;
            SwayVelocity = 0f;
            return;
        }

        Vector3 pos = pelvisPosition;
        Vector3 posXZ = new Vector3(pos.x, 0f, pos.z);

        // Velocity
        if (_prevPelvisValid)
        {
            SwayVelocity = Vector3.Distance(posXZ, _prevPelvisXZ) / Time.deltaTime;
        }
        _prevPelvisXZ = posXZ;
        _prevPelvisValid = true;

        // Sliding window RMS
        _swayBuffer.Enqueue(posXZ);
        while (_swayBuffer.Count > swayWindowSize)
            _swayBuffer.Dequeue();

        if (_swayBuffer.Count < 2)
        {
            PelvisSwayRMS = 0f;
            return;
        }

        // Compute mean position
        Vector3 mean = Vector3.zero;
        foreach (var s in _swayBuffer) mean += s;
        mean /= _swayBuffer.Count;

        // RMS of displacement from mean
        float sumSq = 0f;
        foreach (var s in _swayBuffer)
        {
            float d = Vector3.Distance(s, mean);
            sumSq += d * d;
        }
        PelvisSwayRMS = Mathf.Sqrt(sumSq / _swayBuffer.Count);
    }

    // ───────────────────────── Symmetry Index ─────────────────────────

    private void ComputeSymmetryIndex()
    {
        float xr = RightKneeFlexion;
        float xl = LeftKneeFlexion;
        float mean = (xr + xl) * 0.5f;
        SymmetryIndex = mean > 0.01f
            ? 100f * Mathf.Abs(xr - xl) / mean
            : 0f;
    }

    private void ComputeReachMetrics()
    {
        if (!TryGetLegPoints(true, out Vector3 leftHip, out _, out Vector3 leftAnkle, out _) ||
            !TryGetLegPoints(false, out Vector3 rightHip, out _, out Vector3 rightAnkle, out _))
        {
            LeftStanceAnteriorReachPct = 0f;
            RightStanceAnteriorReachPct = 0f;
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(GetForwardReference(), worldUp);
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();

        LeftStanceAnteriorReachPct = ComputeAnteriorReachPct(leftHip, leftAnkle, rightAnkle, forward);
        RightStanceAnteriorReachPct = ComputeAnteriorReachPct(rightHip, rightAnkle, leftAnkle, forward);
    }

    private float ComputeAnteriorReachPct(Vector3 stanceHip, Vector3 stanceAnkle, Vector3 reachAnkle, Vector3 forward)
    {
        Vector3 deltaXZ = Vector3.ProjectOnPlane(reachAnkle - stanceAnkle, worldUp);
        float anteriorReach = Mathf.Max(0f, Vector3.Dot(deltaXZ, forward));
        float legLength = Vector3.Distance(stanceHip, stanceAnkle);

        return legLength > 0.01f
            ? 100f * anteriorReach / legLength
            : 0f;
    }

    private bool TryGetLegPoints(bool isLeft, out Vector3 hip, out Vector3 knee, out Vector3 ankle, out bool usingAvatarFallback)
    {
        usingAvatarFallback = false;

        Transform kneeTracker = isLeft ? leftKneeTracker : rightKneeTracker;
        Transform ankleTracker = isLeft ? leftAnkleTracker : rightAnkleTracker;
        if (pelvisTracker != null && kneeTracker != null && ankleTracker != null)
        {
            hip = ComputeApproxHipCenter(pelvisTracker.position, GetForwardReference(), isLeft);
            knee = kneeTracker.position;
            ankle = ankleTracker.position;
            return true;
        }

        Transform hipBone = isLeft ? leftHipBone : rightHipBone;
        Transform kneeBone = isLeft ? leftKneeBone : rightKneeBone;
        Transform ankleBone = isLeft ? leftAnkleBone : rightAnkleBone;
        if (useAvatarBoneFallback && hipBone != null && kneeBone != null && ankleBone != null)
        {
            hip = hipBone.position;
            knee = kneeBone.position;
            ankle = ankleBone.position;
            usingAvatarFallback = true;
            return true;
        }

        hip = Vector3.zero;
        knee = Vector3.zero;
        ankle = Vector3.zero;
        return false;
    }

    private bool TryGetPelvisPoint(out Vector3 pelvisPosition)
    {
        if (useAvatarBoneFallback && avatarPelvisBone != null && (leftKneeTracker == null || rightKneeTracker == null))
        {
            pelvisPosition = avatarPelvisBone.position;
            return true;
        }

        if (pelvisTracker != null)
        {
            pelvisPosition = pelvisTracker.position;
            return true;
        }

        if (useAvatarBoneFallback && avatarPelvisBone != null)
        {
            pelvisPosition = avatarPelvisBone.position;
            return true;
        }

        pelvisPosition = Vector3.zero;
        return false;
    }

    private Vector3 GetForwardReference()
    {
        Transform reference = useAvatarBoneFallback && avatarPelvisBone != null && (leftKneeTracker == null || rightKneeTracker == null)
            ? avatarPelvisBone
            : pelvisTracker;

        Vector3 forward = reference != null ? reference.forward : Vector3.forward;
        forward = Vector3.ProjectOnPlane(forward, worldUp);
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        return forward.normalized;
    }

    private Vector3 ComputeApproxHipCenter(Vector3 pelvisPos, Vector3 forward, bool isLeft)
    {
        Vector3 lateral = Vector3.Cross(worldUp, forward).normalized;
        if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.right;

        float lateralOffset = isLeft ? -0.10f : 0.10f;
        return pelvisPos + lateral * lateralOffset;
    }

    private void UpdateDataSourceSummary()
    {
        if (!IsLeftLegAvailable && !IsRightLegAvailable)
        {
            DataSourceSummaryTR = "Gecersiz: hip-knee-ankle kaynagi yok";
            return;
        }

        if (IsUsingLeftAvatarFallback || IsUsingRightAvatarFallback)
        {
            DataSourceSummaryTR = "Avatar IK kemik fallback";
            return;
        }

        DataSourceSummaryTR = "Tracker pelvis+knee+ankle";
    }

    // ───────────────────────── Quaternion / Rotasyon Matrisi Araçları (Q43) ─────────────────────────

    /// <summary>
    /// Quaternion'ı 4×4 rotasyon matrisine dönüştürür (standart quaternion-to-matrix formülü).
    /// Unity'nin sütun-öncelikli (column-major) Matrix4x4 formatına uygundur.
    /// </summary>
    public static Matrix4x4 QuaternionToRotationMatrix(Quaternion q)
    {
        float x = q.x, y = q.y, z = q.z, w = q.w;
        return new Matrix4x4(
            new Vector4(1 - 2*(y*y + z*z),     2*(x*y + z*w),     2*(x*z - y*w), 0f),
            new Vector4(    2*(x*y - z*w), 1 - 2*(x*x + z*z),     2*(y*z + x*w), 0f),
            new Vector4(    2*(x*z + y*w),     2*(y*z - x*w), 1 - 2*(x*x + y*y), 0f),
            new Vector4(0f, 0f, 0f, 1f)
        );
    }

    /// <summary>
    /// İki rijit cisim oryantasyonu arasındaki açıyı (derece) rotasyon matrisi üzerinden hesaplar.
    /// Konum tabanlı hesaplamaya alternatif; Q46 karşılaştırma testleri için kullanılabilir.
    /// </summary>
    public static float AngleBetweenOrientations(Quaternion segA, Quaternion segB)
    {
        Matrix4x4 rel = QuaternionToRotationMatrix(Quaternion.Inverse(segA) * segB);
        float trace = rel.m00 + rel.m11 + rel.m22; // 3×3 kısmın izi
        return Mathf.Acos(Mathf.Clamp((trace - 1f) / 2f, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // ───────────────────────── Gizmos ─────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (pelvisTracker == null) return;

        Vector3 pelvisPos = pelvisTracker.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pelvisPos, 0.04f);

        DrawLegGizmo(pelvisPos, leftKneeTracker, leftAnkleTracker, Color.green);
        DrawLegGizmo(pelvisPos, rightKneeTracker, rightAnkleTracker, Color.blue);
    }

    private static void DrawLegGizmo(Vector3 pelvis, Transform knee, Transform ankle, Color col)
    {
        Gizmos.color = col;
        if (knee != null)
        {
            Gizmos.DrawLine(pelvis, knee.position);
            Gizmos.DrawWireSphere(knee.position, 0.03f);
        }
        if (knee != null && ankle != null)
        {
            Gizmos.DrawLine(knee.position, ankle.position);
            Gizmos.DrawWireSphere(ankle.position, 0.025f);
        }
    }
#endif
}
