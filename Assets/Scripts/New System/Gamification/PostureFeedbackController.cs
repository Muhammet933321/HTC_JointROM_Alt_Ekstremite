using UnityEngine;

/// <summary>
/// Reads LowerLimbBiometrics every frame and provides real-time posture feedback:
///   - Platform/floor material color changes (Green → Yellow → Red) based on knee valgus.
///   - Audio warning when crossing risk thresholds.
///
/// Attach to any GameObject. Assign platformRenderer and audioSource in Inspector.
/// </summary>
public class PostureFeedbackController : MonoBehaviour
{
    // ───────────────────────── Inspector ─────────────────────────

    [Header("=== Dependencies ===")]
    public LowerLimbBiometrics biometrics;

    [Header("=== Platform Visual Feedback ===")]
    [Tooltip("Renderer whose material emission/color is changed. Typically the floor/platform.")]
    public Renderer platformRenderer;

    [Tooltip("Lerp speed for colour transitions.")]
    [SerializeField] private float colorLerpSpeed = 4f;

    [Header("=== Valgus Thresholds (degrees) ===")]
    [SerializeField] private float goodThreshold   = 4f;   // < this = green
    [SerializeField] private float warningThreshold = 8f;  // < this = yellow; >= this = red

    [Header("=== Colours ===")]
    [SerializeField] private Color colorGood    = new Color(0f,    0.78f, 0.33f); // #00C853
    [SerializeField] private Color colorWarning = new Color(1f,    0.84f, 0f);    // #FFD600
    [SerializeField] private Color colorRisk    = new Color(0.84f, 0f,    0f);    // #D50000

    [Header("=== Audio Feedback ===")]
    public AudioSource audioSource;

    [Tooltip("Sound played when entering the warning zone (yellow).")]
    public AudioClip warningClip;

    [Tooltip("Sound played when entering the risk zone (red).")]
    public AudioClip riskClip;

    [Tooltip("Sound played when returning to the good zone (green).")]
    public AudioClip goodClip;

    [Tooltip("Minimum seconds between successive audio alerts to avoid spamming.")]
    [SerializeField] private float audioMinInterval = 2f;

    // ───────────────────────── Public State ─────────────────────────

    public enum AlignmentZone { Good, Warning, Risk }
    public AlignmentZone CurrentZone { get; private set; } = AlignmentZone.Good;

    // ───────────────────────── Private ─────────────────────────

    private Color _currentColor;
    private AlignmentZone _lastZone = AlignmentZone.Good;
    private float _audioTimer;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _propBlock;

    // ───────────────────────── Unity ─────────────────────────

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        _currentColor = colorGood;
    }

    private void Update()
    {
        if (biometrics == null) return;

        _audioTimer += Time.deltaTime;

        float maxValgus = Mathf.Max(
            Mathf.Abs(biometrics.LeftValgusAngle),
            Mathf.Abs(biometrics.RightValgusAngle));

        AlignmentZone zone = ClassifyValgus(maxValgus);
        CurrentZone = zone;

        // Handle zone transition audio
        if (zone != _lastZone)
        {
            PlayZoneTransitionAudio(zone);
            _lastZone = zone;
        }

        // Target colour
        Color targetColor = zone switch
        {
            AlignmentZone.Good    => colorGood,
            AlignmentZone.Warning => colorWarning,
            AlignmentZone.Risk    => colorRisk,
            _                     => colorGood
        };

        // Smooth colour transition
        _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * colorLerpSpeed);

        // Apply to platform renderer via MaterialPropertyBlock (avoids material instance creation)
        if (platformRenderer != null)
        {
            platformRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, _currentColor);
            _propBlock.SetColor(EmissionColorId, _currentColor * 0.4f);
            platformRenderer.SetPropertyBlock(_propBlock);
        }
    }

    // ───────────────────────── Helpers ─────────────────────────

    private AlignmentZone ClassifyValgus(float maxValgus)
    {
        if (maxValgus < goodThreshold)    return AlignmentZone.Good;
        if (maxValgus < warningThreshold) return AlignmentZone.Warning;
        return AlignmentZone.Risk;
    }

    private void PlayZoneTransitionAudio(AlignmentZone newZone)
    {
        if (audioSource == null || _audioTimer < audioMinInterval) return;

        AudioClip clip = newZone switch
        {
            AlignmentZone.Good    => goodClip,
            AlignmentZone.Warning => warningClip,
            AlignmentZone.Risk    => riskClip,
            _                     => null
        };

        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
            _audioTimer = 0f;
        }
    }

    // ───────────────────────── Gizmos ─────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.3f,
            $"Zone: {CurrentZone}  Colour: {_currentColor}");
    }
#endif
}
