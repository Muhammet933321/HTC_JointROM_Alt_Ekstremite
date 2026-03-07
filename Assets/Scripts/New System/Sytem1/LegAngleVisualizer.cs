using UnityEngine;
using UnityEngine.UI;

public class LegAngleVisualizer : MonoBehaviour
{
    public ClinicalLegAngles clinicalAngles;
    public Slider hipSlider;
    public Slider kneeSlider;

    void Update()
    {
        if (clinicalAngles == null) return;

        float rawHip = clinicalAngles.GetHipFlexion();
        float rawKnee = clinicalAngles.GetKneeFlexion();

        if (hipSlider)
            hipSlider.value = Mathf.Clamp(rawHip, hipSlider.minValue, hipSlider.maxValue);

        if (kneeSlider)
            kneeSlider.value = Mathf.Clamp(rawKnee, kneeSlider.minValue, kneeSlider.maxValue);
    }
}
