using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Handles calibration for the full-body tracking system.
/// Hold A/B button on right controller (or 'C' key) for 2 seconds in a natural standing pose.
/// </summary>
[DisallowMultipleComponent]
public class FullBodyCalibrator : MonoBehaviour
{
    [Header("Hedef Solver")]
    [SerializeField] private FullBodyIKSolver ikSolver;
    [SerializeField] private FullBodyTrackingManager trackingManager;

    [Header("UI")]
    [SerializeField] private TMP_Text calibrationStatusText;

    [Header("Ayarlar")]
    [Tooltip("Kalibrasyon için butona basılı tutma süresi (saniye)")]
    [SerializeField] private float holdDuration = 2f;
    [Tooltip("Kalibrasyon için klavye tuşu (Editor/Test)")]
    [SerializeField] private Key calibrationKey = Key.C;

    private float _holdTimer;
    private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> _rightHandDevices = new();

    private void Update()
    {
        bool pressed = false;

        // 1) Right controller: A/B button
        pressed |= IsRightControllerButtonPressed();

        // 2) Keyboard fallback
        if (Keyboard.current != null)
        {
            pressed |= Keyboard.current[calibrationKey].isPressed;
        }

        if (pressed)
        {
            _holdTimer += Time.deltaTime;

            if (calibrationStatusText)
            {
                float remaining = holdDuration - _holdTimer;
                if (remaining > 0)
                    calibrationStatusText.text = $"Kalibrasyon: Doğal pozisyonda durun... {remaining:F1}s";
            }

            if (_holdTimer >= holdDuration)
            {
                PerformCalibration();
            }
        }
        else
        {
            if (_holdTimer > 0f && _holdTimer < holdDuration)
            {
                if (calibrationStatusText)
                {
                    if (ikSolver && ikSolver.IsCalibrated)
                        calibrationStatusText.text = "Kalibrasyon aktif. Tekrar kalibre etmek için butona basılı tutun.";
                    else
                        calibrationStatusText.text = "Kalibrasyon gerekli. Doğal pozisyonda durup butona basılı tutun.";
                }
            }
            _holdTimer = 0f;
        }
    }

    private void PerformCalibration()
    {
        _holdTimer = 0f;

        if (!trackingManager || !trackingManager.IsAssigned)
        {
            Debug.LogWarning("[FullBodyCalibrator] Tracker'lar henüz atanmadı. Kalibrasyon yapılamaz.");
            if (calibrationStatusText)
                calibrationStatusText.text = "Hata: Tracker'lar atanmadı! Tracker'ları bağlayın.";
            return;
        }

        if (!ikSolver)
        {
            Debug.LogWarning("[FullBodyCalibrator] IK Solver atanmamış.");
            if (calibrationStatusText)
                calibrationStatusText.text = "Hata: IK Solver bulunamadı!";
            return;
        }

        // 1. Ensure tracker data is fresh in this frame
        trackingManager.RefreshAndDriveTargetsNow();

        // 2. Snap IK targets to avatar's current bone positions (T-pose)
        ikSolver.SnapTargetsToCurrentBones();

        // 3. Capture device↔target pairs for delta-based mapping
        trackingManager.CalibrateMapping();

        // 4. Compute rotation offsets between targets and bones
        ikSolver.Calibrate();

        if (calibrationStatusText)
            calibrationStatusText.text = "✓ Kalibrasyon tamamlandı! Hareket edebilirsiniz.";

        Debug.Log("[FullBodyCalibrator] Full-body kalibrasyon tamamlandı.");
    }

    private bool IsRightControllerButtonPressed()
    {
        _rightHandDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, _rightHandDevices);

        for (int i = 0; i < _rightHandDevices.Count; i++)
        {
            var dev = _rightHandDevices[i];

            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primary) && primary)
                return true;

            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondary) && secondary)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Programmatic calibration trigger (for UI buttons etc.)
    /// </summary>
    public void TriggerCalibration()
    {
        PerformCalibration();
    }

    /// <summary>
    /// Reset calibration.
    /// </summary>
    public void ResetCalibration()
    {
        if (ikSolver)
        {
            ikSolver.ResetCalibration();
            if (calibrationStatusText)
                calibrationStatusText.text = "Kalibrasyon sıfırlandı. Tekrar kalibre edin.";
        }
    }
}
