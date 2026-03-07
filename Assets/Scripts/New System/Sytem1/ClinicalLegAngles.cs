using UnityEngine;
using TMPro;

public class ClinicalLegAngles : MonoBehaviour
{
    public LegTrackerSolver solver;

    [Header("Debug Texts")]
    public TextMeshProUGUI hipFlexText;
    public TextMeshProUGUI kneeFlexText;

    // Signed angles relative to calibration pose
    private float hipFlexDeg;
    private float kneeFlexDeg;

    private int _lastCalibVersion = -1;

    // Baseline references captured at calibration
    private Vector3 _thighRefPelvisLocal = Vector3.down;
    private Vector3 _shinRefThighLocal = Vector3.down;
    private float _hipFlexZeroDeg = 0f;
    private float _kneeFlexZeroDeg = 0f;

    void Update()
    {
        if (solver == null || !solver.calibrated)
        {
            ResetAngles();
            WriteTexts();
            return;
        }

        if (_lastCalibVersion != solver.CalibrationVersion)
        {
            SampleBaseline();
            _lastCalibVersion = solver.CalibrationVersion;
        }

        if (!solver.hipJoint || !solver.kneeJoint || !solver.ankleJoint)
        {
            ResetAngles();
            WriteTexts();
            return;
        }

        ComputeAngles();
        WriteTexts();
    }

    private void ResetAngles()
    {
        hipFlexDeg = 0f;
        kneeFlexDeg = 0f;
    }

    private void SampleBaseline()
    {
        Transform pelvis = solver.pelvisBone ? solver.pelvisBone : transform;
        Transform hip = solver.hipJoint;
        Transform knee = solver.kneeJoint;
        Transform ankle = solver.ankleJoint;

        if (!hip || !knee || !ankle)
            return;

        Vector3 h = hip.position;
        Vector3 k = knee.position;
        Vector3 a = ankle.position;

        Vector3 thighDirWorld = k - h;
        Vector3 shinDirWorld = a - k;

        if (thighDirWorld.sqrMagnitude < 1e-8f)
            thighDirWorld = pelvis.TransformDirection(Vector3.down);
        if (shinDirWorld.sqrMagnitude < 1e-8f)
            shinDirWorld = hip.TransformDirection(Vector3.down);

        thighDirWorld.Normalize();
        shinDirWorld.Normalize();

        _thighRefPelvisLocal = pelvis.InverseTransformDirection(thighDirWorld).normalized;
        _shinRefThighLocal = hip.InverseTransformDirection(shinDirWorld).normalized;

        _hipFlexZeroDeg = ComputeHipFlexionFromPelvisLocal(_thighRefPelvisLocal);
        _kneeFlexZeroDeg = Mathf.Atan2(_shinRefThighLocal.z, -_shinRefThighLocal.y) * Mathf.Rad2Deg;
    }

    private void ComputeAngles()
    {
        Transform pelvis = solver.pelvisBone ? solver.pelvisBone : transform;
        Transform hip = solver.hipJoint;
        Transform knee = solver.kneeJoint;
        Transform ankle = solver.ankleJoint;

        Vector3 h = hip.position;
        Vector3 k = knee.position;
        Vector3 a = ankle.position;

        // --- Kalça Fleksiyonu ---
        Vector3 thighDirWorld = k - h;
        if (thighDirWorld.sqrMagnitude < 1e-8f)
            thighDirWorld = pelvis.TransformDirection(_thighRefPelvisLocal);
        thighDirWorld.Normalize();

        Vector3 thighPelvisLocal = pelvis.InverseTransformDirection(thighDirWorld).normalized;
        float hipFlexCurrent = ComputeHipFlexionFromPelvisLocal(thighPelvisLocal);
        hipFlexDeg = Mathf.DeltaAngle(_hipFlexZeroDeg, hipFlexCurrent);

        // --- Diz Fleksiyonu ---
        Vector3 shinDirWorld = a - k;
        if (shinDirWorld.sqrMagnitude < 1e-8f)
            shinDirWorld = hip.TransformDirection(_shinRefThighLocal);
        shinDirWorld.Normalize();

        Vector3 shinThighLocal = hip.InverseTransformDirection(shinDirWorld).normalized;
        float kneeFlexCurrent = Mathf.Atan2(shinThighLocal.z, -shinThighLocal.y) * Mathf.Rad2Deg;
        kneeFlexDeg = -Mathf.DeltaAngle(_kneeFlexZeroDeg, kneeFlexCurrent);
    }

    private void WriteTexts()
    {
        if (hipFlexText)
            hipFlexText.text = $"Kalça Fleksiyon: {hipFlexDeg:0.0}°";
        if (kneeFlexText)
            kneeFlexText.text = $"Diz Fleksiyon: {kneeFlexDeg:0.0}°";
    }

    /// <summary>
    /// Üst bacak yönünü pelvisin sagittal düzlemine (Y-Z) projekte ederek
    /// standart gonyometri fleksiyon/ekstansiyon açısını hesaplar.
    /// Pozitif = fleksiyon (ileri), negatif = ekstansiyon (geri).
    /// </summary>
    private static float ComputeHipFlexionFromPelvisLocal(Vector3 thighPelvisLocal)
    {
        Vector3 projected;
        if (!TryProjectOnPlane(thighPelvisLocal, Vector3.right, out projected))
            return 0f;
        return -Vector3.SignedAngle(Vector3.down, projected, Vector3.right);
    }

    private static bool TryProjectOnPlane(Vector3 vector, Vector3 planeNormal, out Vector3 projected)
    {
        projected = Vector3.ProjectOnPlane(vector, planeNormal);
        if (projected.sqrMagnitude < 1e-8f)
        {
            projected = Vector3.zero;
            return false;
        }
        projected.Normalize();
        return true;
    }

    public float GetHipFlexion() => hipFlexDeg;
    public float GetKneeFlexion() => kneeFlexDeg;
}
