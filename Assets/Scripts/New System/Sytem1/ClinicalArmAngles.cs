using UnityEngine;
using TMPro;

public class ClinicalArmAngles : MonoBehaviour
{
    public ArmTrackerSolver solver;

    [Header("Debug Texts")]
    public TextMeshProUGUI shoulderFlexText;
    public TextMeshProUGUI elbowFlexText;

    // Signed angles relative to calibration pose
    private float shoulderFlexDeg;
    private float elbowFlexDeg;

    private int _lastCalibVersion = -1;

    // Baseline references captured at calibration
    private Vector3 _upperRefChestLocal = Vector3.down;
    private Vector3 _forearmRefUpperLocal = Vector3.down;
    private float _shoulderFlexZeroDeg = 0f;
    private float _elbowFlexZeroDeg = 0f;

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

        if (!solver.shoulderJoint || !solver.elbowJoint || !solver.wristJoint)
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
        shoulderFlexDeg = 0f;
        elbowFlexDeg = 0f;
    }

    private void SampleBaseline()
    {
        Transform chest = solver.chestBone ? solver.chestBone : transform;
        Transform shoulder = solver.shoulderJoint;
        Transform elbow = solver.elbowJoint;
        Transform wrist = solver.wristJoint;

        if (!shoulder || !elbow || !wrist)
            return;

        Vector3 s = shoulder.position;
        Vector3 e = elbow.position;
        Vector3 w = wrist.position;

        Vector3 upperDirWorld = e - s;
        Vector3 forearmDirWorld = w - e;

        if (upperDirWorld.sqrMagnitude < 1e-8f)
            upperDirWorld = chest.TransformDirection(Vector3.down);
        if (forearmDirWorld.sqrMagnitude < 1e-8f)
            forearmDirWorld = shoulder.TransformDirection(Vector3.down);

        upperDirWorld.Normalize();
        forearmDirWorld.Normalize();

        _upperRefChestLocal = chest.InverseTransformDirection(upperDirWorld).normalized;
        _forearmRefUpperLocal = shoulder.InverseTransformDirection(forearmDirWorld).normalized;

        _shoulderFlexZeroDeg = ComputeShoulderFlexionFromChestLocal(_upperRefChestLocal);
        _elbowFlexZeroDeg = Mathf.Atan2(_forearmRefUpperLocal.z, -_forearmRefUpperLocal.y) * Mathf.Rad2Deg;
    }

    private void ComputeAngles()
    {
        Transform chest = solver.chestBone ? solver.chestBone : transform;
        Transform shoulder = solver.shoulderJoint;
        Transform elbow = solver.elbowJoint;
        Transform wrist = solver.wristJoint;

        Vector3 s = shoulder.position;
        Vector3 e = elbow.position;
        Vector3 w = wrist.position;

        Vector3 upperDirWorld = e - s;
        if (upperDirWorld.sqrMagnitude < 1e-8f)
            upperDirWorld = chest.TransformDirection(_upperRefChestLocal);
        upperDirWorld.Normalize();

        Vector3 upperChestLocal = chest.InverseTransformDirection(upperDirWorld).normalized;
        float flexCurrent = ComputeShoulderFlexionFromChestLocal(upperChestLocal);
        shoulderFlexDeg = Mathf.DeltaAngle(_shoulderFlexZeroDeg, flexCurrent);

        Vector3 forearmDirWorld = w - e;
        if (forearmDirWorld.sqrMagnitude < 1e-8f)
            forearmDirWorld = shoulder.TransformDirection(_forearmRefUpperLocal);
        forearmDirWorld.Normalize();

        Vector3 forearmUpperLocal = shoulder.InverseTransformDirection(forearmDirWorld).normalized;
        float elbowFlexCurrent = Mathf.Atan2(forearmUpperLocal.z, -forearmUpperLocal.y) * Mathf.Rad2Deg;
    elbowFlexDeg = -Mathf.DeltaAngle(_elbowFlexZeroDeg, elbowFlexCurrent);
    }

    private void WriteTexts()
    {
        if (shoulderFlexText)
            shoulderFlexText.text = $"Omuz Fleksiyon: {shoulderFlexDeg:0.0}°";
        if (elbowFlexText)
            elbowFlexText.text = $"Dirsek Fleksiyon: {elbowFlexDeg:0.0}°";
    }

    // Project humerus into the sagittal plane (Y-Z) to follow standard goniometry for flexion/extension.
    private static float ComputeShoulderFlexionFromChestLocal(Vector3 upperChestLocal)
    {
        Vector3 projected;
        if (!TryProjectOnPlane(upperChestLocal, Vector3.right, out projected))
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

    public float GetShoulderFlexion() => shoulderFlexDeg;
    public float GetElbowFlexion() => elbowFlexDeg;
}