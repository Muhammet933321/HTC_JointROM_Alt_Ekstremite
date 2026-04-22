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

    /// <summary>True when at least pelvis + one knee + one ankle on each side are available.</summary>
    public bool IsBilateralAvailable =>
        pelvisTracker != null && leftKneeTracker != null && rightKneeTracker != null
        && leftAnkleTracker != null && rightAnkleTracker != null;

    // ───────────────────────── Private State ─────────────────────────

    private readonly Queue<Vector3> _swayBuffer = new();
    private Vector3 _prevPelvisXZ;
    private bool _prevPelvisValid;

    // ───────────────────────── Unity Lifecycle ─────────────────────────

    private void Update()
    {
        if (useSimulatedInput)
        {
            ApplySimulatedValues();
            return;
        }

        ComputeValgusAngles();
        ComputeKneeFlexion();
        ComputeSwayMetrics();
        ComputeSymmetryIndex();
    }

    // ───────────────────────── Simulated Path ─────────────────────────

    private void ApplySimulatedValues()
    {
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
        if (pelvisTracker == null) return;

        LeftValgusAngle = pelvisTracker != null && leftKneeTracker != null && leftAnkleTracker != null
            ? ComputeValgusForSide(pelvisTracker.position, leftKneeTracker.position, leftAnkleTracker.position, isLeft: true)
            : 0f;

        RightValgusAngle = pelvisTracker != null && rightKneeTracker != null && rightAnkleTracker != null
            ? ComputeValgusForSide(pelvisTracker.position, rightKneeTracker.position, rightAnkleTracker.position, isLeft: false)
            : 0f;
    }

    private float ComputeValgusForSide(Vector3 pelvisPos, Vector3 kneePos, Vector3 anklePos, bool isLeft)
    {
        // Approximate hip center: pelvis centre offset laterally by ~10 cm each side
        float lateralOffset = isLeft ? -0.10f : 0.10f;
        Vector3 right = Vector3.Cross(worldUp, Vector3.forward).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        Vector3 hipCenter = pelvisPos + right * lateralOffset;

        Vector3 vFemur = kneePos - hipCenter;
        Vector3 vTibia = anklePos - kneePos;

        if (vFemur.sqrMagnitude < 1e-6f || vTibia.sqrMagnitude < 1e-6f) return 0f;

        // Project onto frontal plane (remove forward/back component, keep left/right + up/down)
        // Frontal plane normal ≈ forward axis of the person.
        // We use world forward as approximation; for full accuracy, pelvis tracker's forward would be used.
        Vector3 forward = pelvisTracker != null
            ? pelvisTracker.forward
            : Vector3.forward;

        Vector3 femurFrontal = Vector3.ProjectOnPlane(vFemur, forward).normalized;
        Vector3 tibiaFrontal = Vector3.ProjectOnPlane(vTibia, forward).normalized;

        if (femurFrontal.sqrMagnitude < 1e-6f || tibiaFrontal.sqrMagnitude < 1e-6f) return 0f;

        // Signed angle: cross-product with worldUp gives valgus/varus direction
        float angle = Vector3.SignedAngle(femurFrontal, tibiaFrontal, forward);

        // Convention: positive = tibia leans inward (valgus) for both sides
        return isLeft ? -angle : angle;
    }

    // ───────────────────────── Knee Flexion ─────────────────────────

    /// <summary>
    /// Sagittal plane flexion from tracker world positions.
    /// θ = arccos((Vfemur · Vtibia) / (|Vfemur| × |Vtibia|))
    /// 0° = fully extended, positive = flexed.
    /// </summary>
    private void ComputeKneeFlexion()
    {
        LeftKneeFlexion = (pelvisTracker != null && leftKneeTracker != null && leftAnkleTracker != null)
            ? ComputeFlexionForSide(pelvisTracker.position, leftKneeTracker.position, leftAnkleTracker.position)
            : 0f;

        RightKneeFlexion = (pelvisTracker != null && rightKneeTracker != null && rightAnkleTracker != null)
            ? ComputeFlexionForSide(pelvisTracker.position, rightKneeTracker.position, rightAnkleTracker.position)
            : 0f;
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
        if (pelvisTracker == null)
        {
            PelvisSwayRMS = 0f;
            SwayVelocity = 0f;
            return;
        }

        Vector3 pos = pelvisTracker.position;
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
