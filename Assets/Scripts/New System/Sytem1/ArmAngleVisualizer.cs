using UnityEngine;
using UnityEngine.UI;

public class ArmAngleVisualizer : MonoBehaviour
{
    public ClinicalArmAngles clinicalAngles;
    public Slider shoulderSlider;
    public Slider elbowSlider;

    void Update()
    {
        if (clinicalAngles == null) return;

        // Read raw signed angles
        float rawShoulder = clinicalAngles.GetShoulderFlexion();
        float rawElbow = clinicalAngles.GetElbowFlexion();

        if (shoulderSlider)
            shoulderSlider.value = Mathf.Clamp(rawShoulder, shoulderSlider.minValue, shoulderSlider.maxValue);

        if (elbowSlider)
            elbowSlider.value = Mathf.Clamp(rawElbow, elbowSlider.minValue, elbowSlider.maxValue);
    }
}
