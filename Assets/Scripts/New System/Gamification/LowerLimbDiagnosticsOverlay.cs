using System;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Runtime validation panel for build/hardware testing.
/// Shows calibration state, active lower-limb data source, live angles and writes a diagnostic CSV.
/// </summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public class LowerLimbDiagnosticsOverlay : MonoBehaviour
{
    [Header("=== Dependencies ===")]
    public LowerLimbBiometrics biometrics;
    public FullBodyCalibrator calibrator;
    public FullBodyTrackingManager trackingManager;
    public FullBodyIKSolver ikSolver;
    public Transform hmdTransform;

    [Header("=== Panel ===")]
    [SerializeField] private bool showPanelOnStart = true;
    [SerializeField] private float panelDistance = 1.45f;
    [SerializeField] private float panelVerticalOffset = -0.18f;
    [SerializeField] private float panelUpdateInterval = 0.1f;

    [Header("=== Logging ===")]
    [SerializeField] private bool autoRecordAfterCalibration = true;
    [SerializeField] private float autoRecordDurationSeconds = 180f;
    [SerializeField] private float sampleInterval = 0.1f;
    [SerializeField] private string outputFolder = "Diagnostics";

    [Header("=== Keyboard Shortcuts ===")]
    [SerializeField] private Key togglePanelKey = Key.F9;
    [SerializeField] private Key toggleRecordingKey = Key.F10;
    [SerializeField] private Key markKey = Key.F11;

    [Header("=== Controller Shortcuts (Headset) ===")]
    [SerializeField] private bool enableControllerShortcuts = true;
    [SerializeField] private float controllerHoldDuration = 1.5f;

    private readonly StringBuilder _sb = new(1024);
    private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> _leftHandDevices = new();
    private Canvas _canvas;
    private TMP_Text _text;
    private bool _panelVisible;
    private bool _recording;
    private bool _autoRecording;
    private bool _autoRecordConsumed;
    private float _panelTimer;
    private float _sampleTimer;
    private float _recordElapsed;
    private int _sampleIndex;
    private int _markIndex;
    private bool _leftPrimaryWasPressed;
    private bool _leftSecondaryActionFired;
    private float _leftSecondaryHoldTimer;
    private StreamWriter _writer;

    public string CurrentLogPath { get; private set; }

    private void Awake()
    {
        AutoFindDependencies();
        CreatePanel();
        SetPanelVisible(showPanelOnStart);
    }

    private void OnDestroy()
    {
        StopRecording();
    }

    private void OnApplicationQuit()
    {
        StopRecording();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
        HandleControllerShortcuts();
        HandleAutoRecording();

        if (_panelVisible)
        {
            FollowHmd();
            _panelTimer += Time.deltaTime;
            if (_panelTimer >= panelUpdateInterval)
            {
                _panelTimer = 0f;
                UpdatePanelText();
            }
        }

        if (_recording)
        {
            _recordElapsed += Time.deltaTime;
            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= sampleInterval)
            {
                _sampleTimer = 0f;
                WriteSample();
            }

            if (_autoRecording && autoRecordDurationSeconds > 0f && _recordElapsed >= autoRecordDurationSeconds)
            {
                StopRecording();
            }
        }
    }

    private void AutoFindDependencies()
    {
        if (biometrics == null) biometrics = FindObjectOfType<LowerLimbBiometrics>();
        if (calibrator == null) calibrator = FindObjectOfType<FullBodyCalibrator>();
        if (trackingManager == null) trackingManager = FindObjectOfType<FullBodyTrackingManager>();
        if (ikSolver == null) ikSolver = FindObjectOfType<FullBodyIKSolver>();
        if (hmdTransform == null && Camera.main != null) hmdTransform = Camera.main.transform;
    }

    private void CreatePanel()
    {
        GameObject canvasGo = new("LowerLimbDiagnosticsCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Camera.main;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1120f, 720f);
        canvasGo.transform.localScale = Vector3.one * 0.001f;

        GameObject bgGo = new("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject textGo = new("DiagnosticsText");
        textGo.transform.SetParent(canvasGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 18f);
        textRect.offsetMax = new Vector2(-24f, -18f);

        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.fontSize = 22f;
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.enableWordWrapping = false;
        _text.richText = true;
        _text.color = Color.white;
    }

    private void SetPanelVisible(bool visible)
    {
        _panelVisible = visible;
        if (_canvas != null)
            _canvas.gameObject.SetActive(visible);
        if (visible)
            UpdatePanelText();
    }

    private void FollowHmd()
    {
        if (_canvas == null) return;
        if (hmdTransform == null && Camera.main != null) hmdTransform = Camera.main.transform;
        if (hmdTransform == null) return;

        Vector3 position = hmdTransform.position + hmdTransform.forward * panelDistance + hmdTransform.up * panelVerticalOffset;
        _canvas.transform.SetPositionAndRotation(position, hmdTransform.rotation);
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null) return;

        if (WasPressed(togglePanelKey))
            SetPanelVisible(!_panelVisible);

        if (WasPressed(toggleRecordingKey))
        {
            if (_recording) StopRecording();
            else StartRecording(autoStarted: false);
        }

        if (WasPressed(markKey))
            _markIndex++;
    }

    private bool WasPressed(Key key)
    {
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }

    private void HandleControllerShortcuts()
    {
        if (!enableControllerShortcuts) return;

        bool leftPrimary = IsLeftControllerButtonPressed(UnityEngine.XR.CommonUsages.primaryButton);
        if (leftPrimary && !_leftPrimaryWasPressed)
        {
            _markIndex++;
        }
        _leftPrimaryWasPressed = leftPrimary;

        bool leftSecondary = IsLeftControllerButtonPressed(UnityEngine.XR.CommonUsages.secondaryButton);
        if (leftSecondary)
        {
            _leftSecondaryHoldTimer += Time.deltaTime;
            if (!_leftSecondaryActionFired && _leftSecondaryHoldTimer >= controllerHoldDuration)
            {
                if (_recording)
                    StopRecording();
                else if (IsCalibrationReady())
                    StartRecording(autoStarted: false);

                _leftSecondaryActionFired = true;
            }
        }
        else
        {
            _leftSecondaryHoldTimer = 0f;
            _leftSecondaryActionFired = false;
        }
    }

    private bool IsLeftControllerButtonPressed(UnityEngine.XR.InputFeatureUsage<bool> usage)
    {
        _leftHandDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, _leftHandDevices);

        for (int i = 0; i < _leftHandDevices.Count; i++)
        {
            if (_leftHandDevices[i].TryGetFeatureValue(usage, out bool pressed) && pressed)
                return true;
        }

        return false;
    }

    private void HandleAutoRecording()
    {
        if (!autoRecordAfterCalibration || _autoRecordConsumed || _recording) return;

        if (IsCalibrationReady())
            StartRecording(autoStarted: true);
    }

    private bool IsCalibrationReady()
    {
        bool calibratorReady = calibrator != null && calibrator.IsCalibrated;
        bool solverReady = ikSolver != null && ikSolver.IsCalibrated;
        bool trackingReady = trackingManager != null && trackingManager.IsAssigned;
        return calibratorReady && solverReady && trackingReady;
    }

    private void StartRecording(bool autoStarted)
    {
        if (biometrics == null) AutoFindDependencies();

        string folder = Path.Combine(Application.persistentDataPath, outputFolder);
        Directory.CreateDirectory(folder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        CurrentLogPath = Path.Combine(folder, $"lower_limb_diagnostics_{timestamp}.csv");
        _writer = new StreamWriter(CurrentLogPath, false, Encoding.UTF8) { AutoFlush = true };
        _writer.WriteLine("sample,elapsed_s,mark,calibratorCalibrated,ikCalibrated,trackingAssigned,mappingCalibrated,simulatedInput,dataSource,leftAvailable,rightAvailable,leftFallback,rightFallback,leftValgus_deg,rightValgus_deg,leftFlex_deg,rightFlex_deg,swayRMS_mm,swayVelocity_mps,symmetry_pct,leftReach_pct,rightReach_pct,pelvis_x,pelvis_y,pelvis_z,leftKnee_x,leftKnee_y,leftKnee_z,rightKnee_x,rightKnee_y,rightKnee_z,leftAnkle_x,leftAnkle_y,leftAnkle_z,rightAnkle_x,rightAnkle_y,rightAnkle_z");

        _recording = true;
        _autoRecording = autoStarted;
        _autoRecordConsumed = autoStarted;
        _recordElapsed = 0f;
        _sampleTimer = 0f;
        _sampleIndex = 0;

        Debug.Log($"[LowerLimbDiagnosticsOverlay] Diagnostic recording started: {CurrentLogPath}");
    }

    private void StopRecording()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        if (_recording)
            Debug.Log($"[LowerLimbDiagnosticsOverlay] Diagnostic recording stopped: {CurrentLogPath}");

        _recording = false;
        _autoRecording = false;
    }

    private void WriteSample()
    {
        if (_writer == null || biometrics == null) return;

        Transform pelvis = GetActivePelvis();
        Transform leftKnee = biometrics.IsUsingLeftAvatarFallback ? biometrics.leftKneeBone : biometrics.leftKneeTracker;
        Transform rightKnee = biometrics.IsUsingRightAvatarFallback ? biometrics.rightKneeBone : biometrics.rightKneeTracker;
        Transform leftAnkle = biometrics.IsUsingLeftAvatarFallback ? biometrics.leftAnkleBone : biometrics.leftAnkleTracker;
        Transform rightAnkle = biometrics.IsUsingRightAvatarFallback ? biometrics.rightAnkleBone : biometrics.rightAnkleTracker;

        _writer.WriteLine(string.Join(",",
            _sampleIndex++,
            F(_recordElapsed),
            _markIndex,
            Bool(calibrator != null && calibrator.IsCalibrated),
            Bool(ikSolver != null && ikSolver.IsCalibrated),
            Bool(trackingManager != null && trackingManager.IsAssigned),
            Bool(trackingManager != null && trackingManager.IsMappingCalibrated),
            Bool(biometrics.useSimulatedInput),
            Csv(biometrics.DataSourceSummaryTR),
            Bool(biometrics.IsLeftLegAvailable),
            Bool(biometrics.IsRightLegAvailable),
            Bool(biometrics.IsUsingLeftAvatarFallback),
            Bool(biometrics.IsUsingRightAvatarFallback),
            F(biometrics.LeftValgusAngle),
            F(biometrics.RightValgusAngle),
            F(biometrics.LeftKneeFlexion),
            F(biometrics.RightKneeFlexion),
            F(biometrics.PelvisSwayRMS * 1000f),
            F(biometrics.SwayVelocity),
            F(biometrics.SymmetryIndex),
            F(biometrics.LeftStanceAnteriorReachPct),
            F(biometrics.RightStanceAnteriorReachPct),
            Pos(pelvis),
            Pos(leftKnee),
            Pos(rightKnee),
            Pos(leftAnkle),
            Pos(rightAnkle)));
    }

    private void UpdatePanelText()
    {
        if (_text == null) return;
        if (biometrics == null) AutoFindDependencies();

        bool simulated = biometrics != null && biometrics.useSimulatedInput;
        bool leftReady = biometrics != null && biometrics.IsLeftLegAvailable;
        bool rightReady = biometrics != null && biometrics.IsRightLegAvailable;
        bool fallback = biometrics != null && (biometrics.IsUsingLeftAvatarFallback || biometrics.IsUsingRightAvatarFallback);

        _sb.Clear();
        _sb.AppendLine("<b>ALT EKSTREMITE VERI DOGRULAMA</b>");
        _sb.AppendLine("------------------------------------------------------------");
        _sb.Append("Tracker Atama: ").AppendLine(Status(trackingManager != null && trackingManager.IsAssigned));
        _sb.Append("Mapping: ").AppendLine(Status(trackingManager != null && trackingManager.IsMappingCalibrated));
        _sb.Append("Kalibrasyon: ").Append(Status(calibrator != null && calibrator.IsCalibrated));
        _sb.Append("  IK: ").AppendLine(Status(ikSolver != null && ikSolver.IsCalibrated));

        _sb.Append("Kaynak: ").AppendLine(biometrics != null ? biometrics.DataSourceSummaryTR : "Biometrics yok");
        _sb.Append("SimulatedInput: ").AppendLine(simulated ? "<color=red>ACIK - gercek veri degil</color>" : "<color=green>KAPALI</color>");
        _sb.Append("Sol/Sag veri: ").Append(Status(leftReady)).Append(" / ").AppendLine(Status(rightReady));
        if (fallback)
            _sb.AppendLine("<color=yellow>Not: Diz tracker yok; acilar kalibre IK avatar kemiklerinden okunuyor.</color>");

        if (biometrics != null)
        {
            _sb.AppendLine("------------------------------------------------------------");
            _sb.Append("Valgus L/R: ").Append(F(biometrics.LeftValgusAngle)).Append(" / ").Append(F(biometrics.RightValgusAngle)).AppendLine(" deg");
            _sb.Append("Flexion L/R: ").Append(F(biometrics.LeftKneeFlexion)).Append(" / ").Append(F(biometrics.RightKneeFlexion)).AppendLine(" deg");
            _sb.Append("Sway RMS: ").Append(F(biometrics.PelvisSwayRMS * 1000f)).Append(" mm   Vel: ").Append(F(biometrics.SwayVelocity)).AppendLine(" m/s");
            _sb.Append("Symmetry: ").Append(F(biometrics.SymmetryIndex)).Append(" %   Reach L/R: ").Append(F(biometrics.LeftStanceAnteriorReachPct)).Append(" / ").Append(F(biometrics.RightStanceAnteriorReachPct)).AppendLine(" %");
            _sb.Append("Notr durus kontrolu: ").AppendLine(GetNeutralCheckText());
        }

        _sb.AppendLine("------------------------------------------------------------");
        _sb.Append("Kayit: ").AppendLine(_recording ? "<color=green>AKTIF</color>" : "Kapali");
        if (!string.IsNullOrEmpty(CurrentLogPath))
            _sb.Append("Dosya: ").AppendLine(CurrentLogPath);
        _sb.Append("Tuslar: ").Append(togglePanelKey).Append(" panel, ").Append(toggleRecordingKey).Append(" kayit, ").Append(markKey).AppendLine(" marker");
        _sb.Append("Sol controller: X marker, Y ").Append(controllerHoldDuration.ToString("F1", CultureInfo.InvariantCulture)).AppendLine("s kayit durdur/baslat");

        _text.text = _sb.ToString();
    }

    private string GetNeutralCheckText()
    {
        if (biometrics == null) return "Biometrics yok";
        if (biometrics.useSimulatedInput) return "Simulasyon acik";
        if (!biometrics.IsBilateralAvailable) return "Eksik hip-knee-ankle kaynagi";

        bool valgusOk = Mathf.Abs(biometrics.LeftValgusAngle) <= 8f && Mathf.Abs(biometrics.RightValgusAngle) <= 8f;
        bool flexOk = biometrics.LeftKneeFlexion <= 15f && biometrics.RightKneeFlexion <= 15f;
        bool swayOk = biometrics.PelvisSwayRMS <= 0.03f;

        return valgusOk && flexOk && swayOk
            ? "<color=green>OK</color>"
            : "<color=yellow>Notr pozda test et: valgus <=8, flex <=15, sway <=30mm beklenir</color>";
    }

    private Transform GetActivePelvis()
    {
        if (biometrics == null) return null;
        bool fallback = biometrics.IsUsingLeftAvatarFallback || biometrics.IsUsingRightAvatarFallback;
        if (fallback && biometrics.avatarPelvisBone != null) return biometrics.avatarPelvisBone;
        return biometrics.pelvisTracker != null ? biometrics.pelvisTracker : biometrics.avatarPelvisBone;
    }

    private static string Status(bool ok)
    {
        return ok ? "<color=green>OK</color>" : "<color=red>YOK</color>";
    }

    private static string Bool(bool value) => value ? "1" : "0";

    private static string F(float value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string Pos(Transform t)
    {
        if (t == null) return ",,";
        Vector3 p = t.position;
        return string.Join(",", F(p.x), F(p.y), F(p.z));
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}