using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class LegCalibrationManager : MonoBehaviour
{
    public LegTrackerSolver trackerSolver;
    public TextMeshProUGUI statusText;
    public float holdDuration = 2f;

    private float holdTimer = 0f;
    private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> _rightHandDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

    void Update()
    {
        bool pressed = false;

        // 1) Headset controller: Right-hand A/B -> CommonUsages.primaryButton / secondaryButton
        pressed |= IsRightControllerABPressed();

        // 2) Keyboard fallback: 'L' key (editor/testing - bacak kalibrasyonu için)
        if (Keyboard.current != null)
        {
            pressed |= Keyboard.current.lKey.isPressed;
        }

        if (pressed)
        {
            holdTimer += Time.deltaTime;
            if (statusText)
                statusText.text = $"Kalibrasyon yapılıyor... {holdTimer:F1}s";

            if (holdTimer >= holdDuration)
            {
                PerformCalibration();
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    private bool IsRightControllerABPressed()
    {
        _rightHandDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, _rightHandDevices);
        for (int i = 0; i < _rightHandDevices.Count; i++)
        {
            var dev = _rightHandDevices[i];
            bool primaryPressed;
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primaryPressed) && primaryPressed)
                return true;

            bool secondaryPressed;
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out secondaryPressed) && secondaryPressed)
                return true;
        }
        return false;
    }

    void PerformCalibration()
    {
        if (!trackerSolver)
        {
            Debug.LogWarning("LegCalibrationManager: trackerSolver atanmamış!");
            return;
        }

        trackerSolver.Calibrate();
        holdTimer = 0f;

        if (statusText)
            statusText.text = "Kalibrasyon tamamlandı. Açılar sıfırlandı.";

        Debug.Log("[LegCalibrationManager] Kalibrasyon tamamlandı.");
    }
}
