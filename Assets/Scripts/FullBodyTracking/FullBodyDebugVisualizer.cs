using UnityEngine;
using TMPro;

/// <summary>
/// Runtime debug visualizer for the full-body tracking system.
/// Shows joint angles, IK status, and draws skeleton gizmos.
/// </summary>
[DisallowMultipleComponent]
public class FullBodyDebugVisualizer : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private FullBodyIKSolver ikSolver;
    [SerializeField] private FullBodyTrackingManager trackingManager;

    [Header("=== Kemik Referansları (Opsiyonel — Açı Ölçümü İçin) ===")]
    [SerializeField] private Transform hipsBone;
    [SerializeField] private Transform spineBone;
    [SerializeField] private Transform headBone;

    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform leftForeArm;
    [SerializeField] private Transform leftHand;

    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform rightForeArm;
    [SerializeField] private Transform rightHand;

    [SerializeField] private Transform leftUpLeg;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform leftFoot;

    [SerializeField] private Transform rightUpLeg;
    [SerializeField] private Transform rightLeg;
    [SerializeField] private Transform rightFoot;

    [Header("UI")]
    [SerializeField] private TMP_Text jointAnglesText;
    [SerializeField] private TMP_Text systemStatusText;

    [Header("Ayarlar")]
    [SerializeField] private bool showAngles = true;
    [SerializeField] private bool updateEveryFrame = false;
    [SerializeField] private float updateInterval = 0.1f; // 10 Hz

    private float _updateTimer;
    private readonly System.Text.StringBuilder _sb = new(256);

    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if (updateEveryFrame || _updateTimer >= updateInterval)
        {
            _updateTimer = 0f;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (showAngles && jointAnglesText)
        {
            UpdateJointAngles();
        }

        if (systemStatusText)
        {
            UpdateSystemStatus();
        }
    }

    private void UpdateJointAngles()
    {
        _sb.Clear();
        _sb.AppendLine("<b>Eklem Açıları</b>");
        _sb.AppendLine("─────────────────");

        // Left elbow flexion
        if (leftUpperArm && leftForeArm && leftHand)
        {
            float leftElbow = CalculateFlexionAngle(leftUpperArm, leftForeArm, leftHand);
            _sb.Append("Sol Dirsek: ").Append(leftElbow.ToString("F1")).AppendLine("°");
        }

        // Right elbow flexion
        if (rightUpperArm && rightForeArm && rightHand)
        {
            float rightElbow = CalculateFlexionAngle(rightUpperArm, rightForeArm, rightHand);
            _sb.Append("Sağ Dirsek: ").Append(rightElbow.ToString("F1")).AppendLine("°");
        }

        // Left knee flexion
        if (leftUpLeg && leftLeg && leftFoot)
        {
            float leftKnee = CalculateFlexionAngle(leftUpLeg, leftLeg, leftFoot);
            _sb.Append("Sol Diz: ").Append(leftKnee.ToString("F1")).AppendLine("°");
        }

        // Right knee flexion
        if (rightUpLeg && rightLeg && rightFoot)
        {
            float rightKnee = CalculateFlexionAngle(rightUpLeg, rightLeg, rightFoot);
            _sb.Append("Sağ Diz: ").Append(rightKnee.ToString("F1")).AppendLine("°");
        }

        // Hip flexion (spine angle)
        if (hipsBone && spineBone)
        {
            Vector3 hipsUp = hipsBone.up;
            Vector3 spineDir = (spineBone.position - hipsBone.position).normalized;
            float hipFlexion = Vector3.Angle(hipsUp, spineDir);
            _sb.Append("Gövde Eğimi: ").Append(hipFlexion.ToString("F1")).AppendLine("°");
        }

        jointAnglesText.text = _sb.ToString();
    }

    private void UpdateSystemStatus()
    {
        _sb.Clear();
        _sb.AppendLine("<b>Sistem Durumu</b>");
        _sb.AppendLine("─────────────────");

        bool isAssigned = trackingManager && trackingManager.IsAssigned;
        bool isCalibrated = ikSolver && ikSolver.IsCalibrated;

        _sb.Append("Tracker Atama: ").AppendLine(isAssigned ? "<color=green>Tamam</color>" : "<color=red>Bekleniyor</color>");
        _sb.Append("Kalibrasyon: ").AppendLine(isCalibrated ? "<color=green>Aktif</color>" : "<color=yellow>Gerekli</color>");

        if (isCalibrated)
        {
            _sb.Append("Kalibrasyon v").AppendLine(ikSolver.CalibrationVersion.ToString());
        }

        _sb.Append("FPS: ").AppendLine(Mathf.RoundToInt(1f / Time.smoothDeltaTime).ToString());

        systemStatusText.text = _sb.ToString();
    }

    /// <summary>
    /// Calculates the flexion angle at the mid joint of a 3-bone chain.
    /// Returns 0° for fully extended, increasing for more flexion.
    /// </summary>
    private float CalculateFlexionAngle(Transform root, Transform mid, Transform tip)
    {
        Vector3 upperDir = (mid.position - root.position).normalized;
        Vector3 lowerDir = (tip.position - mid.position).normalized;
        return 180f - Vector3.Angle(upperDir, lowerDir);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawBoneGizmo(hipsBone, spineBone, Color.cyan);
        DrawBoneGizmo(spineBone, headBone, Color.cyan);

        // Left arm
        DrawBoneGizmo(leftUpperArm, leftForeArm, Color.green);
        DrawBoneGizmo(leftForeArm, leftHand, Color.green);

        // Right arm
        DrawBoneGizmo(rightUpperArm, rightForeArm, Color.blue);
        DrawBoneGizmo(rightForeArm, rightHand, Color.blue);

        // Left leg
        DrawBoneGizmo(leftUpLeg, leftLeg, Color.yellow);
        DrawBoneGizmo(leftLeg, leftFoot, Color.yellow);

        // Right leg
        DrawBoneGizmo(rightUpLeg, rightLeg, Color.red);
        DrawBoneGizmo(rightLeg, rightFoot, Color.red);

        // Joint spheres
        float r = 0.015f;
        DrawJointSphere(hipsBone, Color.white, r * 2);
        DrawJointSphere(headBone, Color.white, r * 2);
        DrawJointSphere(leftForeArm, Color.green, r);
        DrawJointSphere(rightForeArm, Color.blue, r);
        DrawJointSphere(leftLeg, Color.yellow, r);
        DrawJointSphere(rightLeg, Color.red, r);
    }

    private void DrawBoneGizmo(Transform from, Transform to, Color color)
    {
        if (!from || !to) return;
        Gizmos.color = color;
        Gizmos.DrawLine(from.position, to.position);
    }

    private void DrawJointSphere(Transform joint, Color color, float radius)
    {
        if (!joint) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(joint.position, radius);
    }
#endif
}
