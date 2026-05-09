using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Master state machine for the Denge Kahramanı application.
///
/// Full flow:
///   WaitingForTrackers
///     → trackers detected → CalibrationInstructions
///   CalibrationInstructions  (show T-pose instructions)
///     → user starts holding A/B → Calibrating
///   Calibrating              (hold-progress bar fills)
///     → FullBodyCalibrator.OnCalibrationComplete → CalibrationComplete
///   CalibrationComplete      ("Başlamak için A'ya basın" or auto-start)
///     → single A press → SessionRunning
///   SessionRunning           (TaskSequencer manages tasks)
///     → session complete → SessionComplete
///   SessionComplete          (summary shown)
///     → single A press → CalibrationInstructions (restart)
///
/// Editor / No-tracker mode:
///   If simulatedMode = true, skips WaitingForTrackers and Calibrating entirely.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    // ───────────────────────── State ─────────────────────────

    public enum GameState
    {
        WaitingForTrackers,
        CalibrationInstructions,
        Calibrating,
        CalibrationComplete,
        SessionRunning,
        SessionComplete
    }

    public GameState CurrentState { get; private set; } = GameState.WaitingForTrackers;

    // ───────────────────────── Dependencies ─────────────────────────

    [Header("=== Core Dependencies ===")]
    public FullBodyCalibrator calibrator;
    public FullBodyTrackingManager trackingManager;
    public TaskSequencer sequencer;
    public LowerLimbBiometrics biometrics;
    public GameUIController gameUI;

    // ───────────────────────── Calibration Panel UI ─────────────────────────

    [Header("=== Calibration Panel ===")]
    [Tooltip("Root GameObject for the calibration screen.")]
    public GameObject calibrationPanel;

    [Tooltip("Shown in WaitingForTrackers state.")]
    public TMPro.TMP_Text trackerStatusText;

    [Tooltip("Instruction text ('T-pozuna geçin...').")]
    public TMPro.TMP_Text calibrationInstructText;

    [Tooltip("Hold progress / status text from calibrator.")]
    public TMPro.TMP_Text calibrationStatusText;

    [Tooltip("Hold progress bar fill image.")]
    public UnityEngine.UI.Image holdProgressBar;

    [Tooltip("Shown after calibration succeeds ('Başlamak için A basın').")]
    public TMPro.TMP_Text startPromptText;

    // ───────────────────────── Simulated / Editor Mode ─────────────────────────

    [Header("=== Editor / Simulated Mode ===")]
    [Tooltip("Skip tracker wait and calibration — go directly to CalibrationComplete.\n" +
             "LowerLimbBiometrics.useSimulatedInput will be set automatically.")]
    public bool simulatedMode = false;

    [Tooltip("Keyboard key for single-press 'start session' in simulated mode (default: Space).")]
    [SerializeField] private Key startKey = Key.Space;

    [Tooltip("Keyboard key for recalibrating (default: R).")]
    [SerializeField] private Key recalibKey = Key.R;

    // ───────────────────────── Settings ─────────────────────────

    [Header("=== Settings ===")]
    [Tooltip("Seconds between tracker scan polls in WaitingForTrackers state.")]
    [SerializeField] private float trackerScanInterval = 1.5f;

    [Tooltip("If true, session starts automatically after calibration completes (no button press needed).")]
    [SerializeField] private bool autoStartAfterCalibration = false;

    // ───────────────────────── Private ─────────────────────────

    private float _trackerScanTimer;
    private bool _startButtonWasPressed;
    private readonly List<UnityEngine.XR.InputDevice> _rightHandDevices = new();

    // ───────────────────────── Unity ─────────────────────────

    private void Awake()
    {
        // Disable the calibrator's own Update loop text — we drive the UI ourselves
        if (calibrator != null)
            calibrator.OnCalibrationComplete += HandleCalibrationComplete;

        if (sequencer != null)
            sequencer.OnSessionCompleted += HandleSessionCompleted;
    }

    private void OnDestroy()
    {
        if (calibrator != null)
            calibrator.OnCalibrationComplete -= HandleCalibrationComplete;
        if (sequencer != null)
            sequencer.OnSessionCompleted -= HandleSessionCompleted;
    }

    private void Start()
    {
        if (simulatedMode)
        {
            if (biometrics != null) biometrics.useSimulatedInput = true;
            TransitionTo(GameState.CalibrationComplete);
        }
        else
        {
            if (biometrics != null) biometrics.useSimulatedInput = false;
            TransitionTo(GameState.WaitingForTrackers);
        }
    }

    private void Update()
    {
        _trackerScanTimer += Time.deltaTime;

        switch (CurrentState)
        {
            case GameState.WaitingForTrackers:
                UpdateWaitingForTrackers();
                break;

            case GameState.CalibrationInstructions:
                UpdateCalibrationInstructions();
                break;

            case GameState.Calibrating:
                UpdateCalibrating();
                break;

            case GameState.CalibrationComplete:
                UpdateCalibrationComplete();
                break;

            case GameState.SessionRunning:
                // TaskSequencer drives everything; nothing needed here.
                break;

            case GameState.SessionComplete:
                UpdateSessionComplete();
                break;
        }
    }

    // ───────────────────────── State Updates ─────────────────────────

    private void UpdateWaitingForTrackers()
    {
        if (_trackerScanTimer < trackerScanInterval) return;
        _trackerScanTimer = 0f;

        bool trackersReady = trackingManager != null && trackingManager.IsAssigned;

        if (trackersReady)
        {
            TransitionTo(GameState.CalibrationInstructions);
        }
        else
        {
            SetText(trackerStatusText,
                trackingManager != null
                    ? "Tracker'lar aranıyor... VR cihazlarını bağlayın."
                    : "FullBodyTrackingManager atanmamış!");
        }
    }

    private void UpdateCalibrationInstructions()
    {
        // If user starts holding A/B → transition to Calibrating state
        if (calibrator != null && calibrator.HoldProgress > 0.01f)
            TransitionTo(GameState.Calibrating);

        // Keyboard: C key = start calibrating immediately via TriggerCalibration (editor)
        if (Keyboard.current != null && Keyboard.current[Key.C].wasPressedThisFrame)
        {
            if (calibrator != null) calibrator.TriggerCalibration();
        }
    }

    private void UpdateCalibrating()
    {
        if (calibrator == null) return;

        float progress = calibrator.HoldProgress;

        if (holdProgressBar != null)
            holdProgressBar.fillAmount = progress;

        SetText(calibrationStatusText,
            $"T-pozunda bekleyin... {progress * 100f:F0}%");

        // If user released button before completing, go back to instructions
        if (progress <= 0.001f && CurrentState == GameState.Calibrating)
            TransitionTo(GameState.CalibrationInstructions);
    }

    private void UpdateCalibrationComplete()
    {
        if (autoStartAfterCalibration)
        {
            StartSession();
            return;
        }

        // Single A/B press OR Space key
        bool singlePress = GetStartButtonSinglePress();

        if (singlePress)
        {
            StartSession();
            return;
        }

        // Allow re-calibration with R key before starting the session.
        if (Keyboard.current != null && Keyboard.current[recalibKey].wasPressedThisFrame)
        {
            ReturnToCalibration();
        }
    }

    private void UpdateSessionComplete()
    {
        // Press A/B or Space to restart
        bool singlePress = GetStartButtonSinglePress();
        if (singlePress)
        {
            if (simulatedMode)
                TransitionTo(GameState.CalibrationComplete);
            else
                ReturnToCalibration();
        }
    }

    // ───────────────────────── Transitions ─────────────────────────

    private void TransitionTo(GameState newState)
    {
        GameState prev = CurrentState;
        CurrentState = newState;

        _startButtonWasPressed = false;

        HideAllCalibrationPanels();

        switch (newState)
        {
            case GameState.WaitingForTrackers:
                ShowCalibrationPanel(true);
                SetText(trackerStatusText, "VR tracker'ları bekleniyor...");
                SetText(calibrationInstructText, "");
                SetText(calibrationStatusText, "");
                SetText(startPromptText, "");
                gameUI?.SetGamePanelsVisible(false);
                break;

            case GameState.CalibrationInstructions:
                ShowCalibrationPanel(true);
                SetText(trackerStatusText, "✓ Tracker'lar bağlı");
                SetText(calibrationInstructText,
                    "T-POZU KALIBRASYONU\n\n" +
                    "Ayaklarınızı omuz genişliğinde açın.\n" +
                    "Kollarınızı yanlara doğru yatay tutun.\n" +
                    "Dik durun ve hareketsiz kalın.\n\n" +
                    "Hazır olduğunuzda sağ kontrolörde\n" +
                    "A veya B butonunu 2 saniye basılı tutun.\n" +
                    "(Editör: C tuşu)");
                SetText(calibrationStatusText, "Butona basılı tutmayı bekliyor...");
                SetText(startPromptText, "");
                SetProgress(0f);
                gameUI?.SetGamePanelsVisible(false);
                break;

            case GameState.Calibrating:
                ShowCalibrationPanel(true);
                SetText(calibrationInstructText, "T-pozunda bekleyin — butonu bırakmayın!");
                SetText(startPromptText, "");
                gameUI?.SetGamePanelsVisible(false);
                break;

            case GameState.CalibrationComplete:
                ShowCalibrationPanel(true);
                SetText(trackerStatusText, "✓ Kalibrasyon tamamlandı");
                SetText(calibrationInstructText,
                    simulatedMode
                        ? "SİMÜLASYON MODU — Tracker gerekmez.\nBiometrics > sliderları ile test edebilirsiniz."
                        : "Kalibrasyon başarılı!\n\nTrackerlar doğru konumlandırıldı.\nHareketleriniz izleniyor.");
                SetText(calibrationStatusText, "");
                SetText(startPromptText,
                    autoStartAfterCalibration
                        ? "Oturum başlatılıyor..."
                        : "OTURUMU BAŞLATMAK İÇİN\nA BUTONUNA BASIN\n(Editör: Space)");
                SetProgress(1f);
                gameUI?.SetGamePanelsVisible(false);

                // Wire real tracker transforms to biometrics
                if (!simulatedMode) WireBiometricsTrackers();
                break;

            case GameState.SessionRunning:
                ShowCalibrationPanel(false);
                gameUI?.SetGamePanelsVisible(true);
                // Disable calibrator to prevent accidental re-calibration during session
                if (calibrator != null) calibrator.enabled = false;
                break;

            case GameState.SessionComplete:
                ShowCalibrationPanel(false);
                SetText(startPromptText, "");
                gameUI?.SetGamePanelsVisible(true); // shows summary panel
                // Re-enable calibrator for next session
                if (calibrator != null) calibrator.enabled = true;
                break;
        }

        Debug.Log($"[GameFlowController] {prev} → {newState}");
    }

    // ───────────────────────── Session Control ─────────────────────────

    private void StartSession()
    {
        if (sequencer == null)
        {
            Debug.LogWarning("[GameFlowController] TaskSequencer atanmamış!");
            return;
        }

        if (!HasCompletedCalibration())
        {
            Debug.LogWarning("[GameFlowController] Kalibrasyon tamamlanmadan oturum başlatılamaz.");
            ReturnToCalibration();
            SetText(calibrationStatusText, "Kalibrasyon tamamlanmadan oturum başlatılamaz.");
            return;
        }

        TransitionTo(GameState.SessionRunning);
        sequencer.StartSession();
    }

    private bool HasCompletedCalibration()
    {
        return simulatedMode || (calibrator != null && calibrator.IsCalibrated);
    }

    private void ReturnToCalibration()
    {
        if (calibrator != null)
        {
            calibrator.enabled = true;
            calibrator.ResetCalibration();
        }

        bool trackersReady = trackingManager != null && trackingManager.IsAssigned;
        TransitionTo(trackersReady ? GameState.CalibrationInstructions : GameState.WaitingForTrackers);
    }

    // ───────────────────────── Event Handlers ─────────────────────────

    private void HandleCalibrationComplete()
    {
        if (CurrentState == GameState.Calibrating || CurrentState == GameState.CalibrationInstructions)
            TransitionTo(GameState.CalibrationComplete);
    }

    private void HandleSessionCompleted(List<TaskResult> results)
    {
        TransitionTo(GameState.SessionComplete);
    }

    // ───────────────────────── Biometrics Wiring ─────────────────────────

    /// <summary>
    /// After calibration, maps knee/ankle tracker transforms from TrackingManager to LowerLimbBiometrics.
    /// </summary>
    private void WireBiometricsTrackers()
    {
        if (biometrics == null || trackingManager == null) return;

        trackingManager.GetAssignment(
            out int pelvisIdx, out int leftFootIdx, out int rightFootIdx,
            out int leftKneeIdx, out int rightKneeIdx);

        biometrics.pelvisTracker     = trackingManager.GetTrackerTransform(pelvisIdx);
        biometrics.leftAnkleTracker  = trackingManager.GetTrackerTransform(leftFootIdx);
        biometrics.rightAnkleTracker = trackingManager.GetTrackerTransform(rightFootIdx);
        biometrics.leftKneeTracker   = trackingManager.GetTrackerTransform(leftKneeIdx);
        biometrics.rightKneeTracker  = trackingManager.GetTrackerTransform(rightKneeIdx);

        biometrics.useSimulatedInput = false;

        Debug.Log("[GameFlowController] Biometrics tracker'ları bağlandı. " +
                  $"Pelvis=T{pelvisIdx} LeftKnee=T{leftKneeIdx} RightKnee=T{rightKneeIdx}");
    }

    // ───────────────────────── Input Helpers ─────────────────────────

    /// <summary>Detects a single (non-hold) press of A/B or Space this frame.</summary>
    private bool GetStartButtonSinglePress()
    {
        // Keyboard
        if (Keyboard.current != null && Keyboard.current[startKey].wasPressedThisFrame)
            return true;

        // Right controller A button — single press (not held)
        _rightHandDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, _rightHandDevices);
        foreach (var dev in _rightHandDevices)
        {
            if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed)
            {
                if (!_startButtonWasPressed)
                {
                    _startButtonWasPressed = true;
                    return true;
                }
            }
            else
            {
                _startButtonWasPressed = false;
            }
        }
        return false;
    }

    // ───────────────────────── UI Helpers ─────────────────────────

    private void ShowCalibrationPanel(bool visible)
    {
        if (calibrationPanel != null)
            calibrationPanel.SetActive(visible);
    }

    private void HideAllCalibrationPanels() { /* panels controlled per-state */ }

    private static void SetText(TMPro.TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }

    private void SetProgress(float v)
    {
        if (holdProgressBar != null)
            holdProgressBar.fillAmount = Mathf.Clamp01(v);
    }

    // ───────────────────────── Gizmos / Editor ─────────────────────────

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(GameFlowController))]
    public class Inspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var ctrl = (GameFlowController)target;
            if (!Application.isPlaying) return;

            UnityEditor.EditorGUILayout.Space(8);
            UnityEditor.EditorGUILayout.LabelField("── Runtime ──", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.LabelField("Durum:", ctrl.CurrentState.ToString());

            UnityEditor.EditorGUILayout.Space(4);
            if (GUILayout.Button("→ CalibrationComplete (test)"))
                ctrl.TransitionTo(GameState.CalibrationComplete);
            if (GUILayout.Button("→ SessionRunning (test)"))
                ctrl.StartSession();
        }
    }
#endif
}
