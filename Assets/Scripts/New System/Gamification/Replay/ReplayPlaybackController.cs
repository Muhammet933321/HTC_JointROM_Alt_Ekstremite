using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
public class ReplayPlaybackController : MonoBehaviour
{
    [Header("=== Source ===")]
    [SerializeField] private string replayFolderPath;
    [SerializeField] private bool loadNewestFromPersistentDataPath = true;
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool playOnLoad = true;

    [Header("=== Playback ===")]
    [SerializeField] private ReplayAvatarDriver avatarDriver;
    [SerializeField] private ReplayRecordable[] recordableTargets;
    [SerializeField] private float playbackSpeed = 1f;
    [SerializeField] private bool loop;
    [SerializeField] private bool enableKeyboardShortcuts = true;
    [SerializeField] private bool applyAvatarInLateUpdate = true;
    [SerializeField] private bool disableLiveTrackingDriversOnLoad = true;

    [Header("=== UI ===")]
    [SerializeField] private bool createFallbackUi = true;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text metricsText;

    private readonly List<ReplayFrame> _frames = new();
    private readonly List<ReplayEvent> _events = new();
    private readonly Dictionary<string, Transform> _recordableTargetsById = new();
    private ReplayManifest _manifest;
    private float _playheadSeconds;
    private int _frameCursor;
    private bool _playing;
    private bool _warnedMissingAvatarDriver;
    private ReplayFrame _pendingLateFrame;
    private bool _hasPendingLateFrame;

    public bool IsLoaded => _frames.Count > 0;
    public bool IsPlaying => _playing;
    public float DurationSeconds => _frames.Count > 0 ? _frames[_frames.Count - 1].elapsedSeconds : 0f;
    public string ReplayFolderPath => replayFolderPath;

    public void ConfigureReplay(string folderPath, ReplayAvatarDriver driver, bool shouldLoadOnStart, bool shouldPlayOnLoad, bool shouldLoadNewest)
    {
        replayFolderPath = folderPath;
        avatarDriver = driver;
        loadOnStart = shouldLoadOnStart;
        playOnLoad = shouldPlayOnLoad;
        loadNewestFromPersistentDataPath = shouldLoadNewest;
    }

    private void Start()
    {
        if (avatarDriver == null)
            avatarDriver = FindObjectOfType<ReplayAvatarDriver>();

        if (createFallbackUi)
            EnsureFallbackUi();

        if (!loadOnStart) return;

        if (loadNewestFromPersistentDataPath)
            replayFolderPath = FindNewestReplayFolder();

        if (!string.IsNullOrEmpty(replayFolderPath))
            LoadReplay(replayFolderPath, playOnLoad);
        else
            ReportStatus("Replay klasoru bulunamadi. replayFolderPath alanina klasor yolu girin veya Replays klasorune paket kopyalayin.", true);
    }

    private void Update()
    {
        HandleKeyboardShortcuts();

        if (!_playing || _frames.Count == 0) return;

        _playheadSeconds += Time.deltaTime * Mathf.Max(0f, playbackSpeed);
        if (_playheadSeconds > DurationSeconds)
        {
            if (loop) _playheadSeconds = 0f;
            else
            {
                _playheadSeconds = DurationSeconds;
                _playing = false;
            }
        }

        ApplyAtTime(_playheadSeconds);
    }

    private void LateUpdate()
    {
        if (!applyAvatarInLateUpdate || !_hasPendingLateFrame) return;

        ReplayFrame frame = _pendingLateFrame;
        _pendingLateFrame = null;
        _hasPendingLateFrame = false;
        ApplyFrameNow(frame);
    }

    public void LoadReplay(string folderPath, bool startPlaying)
    {
        _frames.Clear();
        _events.Clear();
        _frameCursor = 0;
        _playheadSeconds = 0f;
        _playing = false;
        _warnedMissingAvatarDriver = false;
        replayFolderPath = folderPath;

        string manifestPath = Path.Combine(folderPath, "manifest.json");
        string framesPath = Path.Combine(folderPath, "frames.jsonl");
        string eventsPath = Path.Combine(folderPath, "events.jsonl");

        if (!File.Exists(manifestPath) || !File.Exists(framesPath))
        {
            ReportStatus($"Replay bulunamadi: {folderPath}", true);
            return;
        }

        _manifest = JsonUtility.FromJson<ReplayManifest>(File.ReadAllText(manifestPath));
        foreach (string line in File.ReadLines(framesPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ReplayFrame frame = JsonUtility.FromJson<ReplayFrame>(line);
            if (frame != null) _frames.Add(frame);
        }

        if (File.Exists(eventsPath))
        {
            foreach (string line in File.ReadLines(eventsPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ReplayEvent replayEvent = JsonUtility.FromJson<ReplayEvent>(line);
                if (replayEvent != null) _events.Add(replayEvent);
            }
        }

        BuildRecordableTargetMap();
        DisableLiveDriversForReplay();
        ReportStatus($"Replay yuklendi: {_frames.Count} frame, {_events.Count} olay", _frames.Count == 0);

        if (_frames.Count > 0)
            PresentFrame(_frames[0]);

        if (_frames.Count == 0)
            return;

        _playing = startPlaying && _frames.Count > 0;
    }

    public void Play() => _playing = _frames.Count > 0;
    public void Pause() => _playing = false;
    public void TogglePlay() => _playing = !_playing && _frames.Count > 0;

    public void SetNormalizedTime(float normalizedTime)
    {
        if (_frames.Count == 0) return;
        _playheadSeconds = Mathf.Clamp01(normalizedTime) * DurationSeconds;
        ApplyAtTime(_playheadSeconds);
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Max(0f, speed);
    }

    public void SeekRelative(float seconds)
    {
        if (_frames.Count == 0) return;
        _playheadSeconds = Mathf.Clamp(_playheadSeconds + seconds, 0f, DurationSeconds);
        ApplyAtTime(_playheadSeconds);
    }

    private void ApplyAtTime(float timeSeconds)
    {
        if (_frames.Count == 0) return;

        while (_frameCursor < _frames.Count - 1 && _frames[_frameCursor + 1].elapsedSeconds <= timeSeconds)
            _frameCursor++;

        while (_frameCursor > 0 && _frames[_frameCursor].elapsedSeconds > timeSeconds)
            _frameCursor--;

        PresentFrame(_frames[_frameCursor]);
    }

    private void PresentFrame(ReplayFrame frame)
    {
        if (applyAvatarInLateUpdate && Application.isPlaying)
        {
            _pendingLateFrame = frame;
            _hasPendingLateFrame = true;
            return;
        }

        ApplyFrameNow(frame);
    }

    private void ApplyFrameNow(ReplayFrame frame)
    {
        if (frame == null) return;

        if (avatarDriver != null)
        {
            avatarDriver.ApplyFrame(frame);
        }
        else if (!_warnedMissingAvatarDriver)
        {
            _warnedMissingAvatarDriver = true;
            ReportStatus("Replay yuklendi ama ReplayAvatarDriver bulunamadi; sadece UI/metrikler gosteriliyor.", true);
        }

        ApplyRecordablePoses(frame);
        UpdateUi(frame);
    }

    private void DisableLiveDriversForReplay()
    {
        if (!disableLiveTrackingDriversOnLoad) return;

        FullBodyIKSolver[] solvers = FindObjectsOfType<FullBodyIKSolver>();
        for (int index = 0; index < solvers.Length; index++)
            solvers[index].enabled = false;

        FullBodyTrackingManager[] managers = FindObjectsOfType<FullBodyTrackingManager>();
        for (int index = 0; index < managers.Length; index++)
            managers[index].enabled = false;

        FullBodyCalibrator[] calibrators = FindObjectsOfType<FullBodyCalibrator>();
        for (int index = 0; index < calibrators.Length; index++)
            calibrators[index].enabled = false;

        GameFlowController[] flows = FindObjectsOfType<GameFlowController>();
        for (int index = 0; index < flows.Length; index++)
            flows[index].enabled = false;

        TaskSequencer[] sequencers = FindObjectsOfType<TaskSequencer>();
        for (int index = 0; index < sequencers.Length; index++)
            sequencers[index].enabled = false;
    }

    private void ApplyRecordablePoses(ReplayFrame frame)
    {
        if (frame.recordablePoses == null) return;

        for (int index = 0; index < frame.recordablePoses.Count; index++)
        {
            ReplayPose pose = frame.recordablePoses[index];
            if (pose == null || !pose.valid || string.IsNullOrEmpty(pose.id)) continue;
            if (!_recordableTargetsById.TryGetValue(pose.id, out Transform target) || target == null) continue;

            target.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }

    private void UpdateUi(ReplayFrame frame)
    {
        string taskName = !string.IsNullOrEmpty(frame.taskNameTR) ? frame.taskNameTR : GetTaskName(frame.taskIndex);
        string instruction = GetInstruction(frame.taskIndex);

        SetText(taskText, string.IsNullOrEmpty(taskName) ? frame.phase : taskName);
        SetText(instructionText, instruction);
        SetText(timeText, $"{frame.elapsedSeconds:F1}s / {DurationSeconds:F1}s  |  {frame.phase}");

        if (metricsText != null && frame.metrics != null)
        {
            metricsText.text =
                $"Valgus L/R: {frame.metrics.leftValgusDeg:F1} / {frame.metrics.rightValgusDeg:F1} deg\n" +
                $"Flex L/R: {frame.metrics.leftFlexionDeg:F1} / {frame.metrics.rightFlexionDeg:F1} deg\n" +
                $"Sway: {frame.metrics.pelvisSwayRmsMeters * 1000f:F1} mm  SI: {frame.metrics.symmetryPct:F1}%" +
                (avatarDriver != null ? $"\nBones: {avatarDriver.LastAppliedPoseCount} applied, {avatarDriver.LastMissingPoseCount} missing" : "");
        }
    }

    private void HandleKeyboardShortcuts()
    {
        if (!enableKeyboardShortcuts || Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TogglePlay();
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            SeekRelative(-5f);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            SeekRelative(5f);
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SetPlaybackSpeed(0.5f);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SetPlaybackSpeed(1f);
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SetPlaybackSpeed(2f);
    }

    private void EnsureFallbackUi()
    {
        if (statusText != null && taskText != null && instructionText != null && timeText != null && metricsText != null)
            return;

        GameObject canvasGo = new("ReplayPlaybackFallbackCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(18f, -18f);
        panelRect.sizeDelta = new Vector2(680f, 260f);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.68f);

        if (statusText == null) statusText = CreateFallbackLabel(panelRect, "Status", new Vector2(18f, -16f), 18f, 26f);
        if (taskText == null) taskText = CreateFallbackLabel(panelRect, "Task", new Vector2(18f, -48f), 24f, 32f);
        if (instructionText == null) instructionText = CreateFallbackLabel(panelRect, "Instruction", new Vector2(18f, -86f), 18f, 50f);
        if (timeText == null) timeText = CreateFallbackLabel(panelRect, "Time", new Vector2(18f, -142f), 18f, 28f);
        if (metricsText == null) metricsText = CreateFallbackLabel(panelRect, "Metrics", new Vector2(18f, -176f), 17f, 68f);

        SetText(statusText, "Replay yuklenmeyi bekliyor. Space: oynat/duraklat, Sol/Sag: 5s, 1/2/3: hiz.");
    }

    private static TMP_Text CreateFallbackLabel(RectTransform parent, string name, Vector2 anchoredPosition, float fontSize, float height)
    {
        GameObject labelGo = new(name);
        labelGo.transform.SetParent(parent, false);
        RectTransform rect = labelGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(-36f, height);

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = true;
        label.text = "";
        return label;
    }

    private string GetTaskName(int taskIndex)
    {
        if (_manifest == null || _manifest.tasks == null || taskIndex < 0 || taskIndex >= _manifest.tasks.Count) return "";
        return _manifest.tasks[taskIndex].taskNameTR;
    }

    private string GetInstruction(int taskIndex)
    {
        if (_manifest == null || _manifest.tasks == null || taskIndex < 0 || taskIndex >= _manifest.tasks.Count) return "";
        return _manifest.tasks[taskIndex].instructionsTR;
    }

    private void BuildRecordableTargetMap()
    {
        _recordableTargetsById.Clear();
        if (recordableTargets == null) return;

        for (int index = 0; index < recordableTargets.Length; index++)
        {
            ReplayRecordable recordable = recordableTargets[index];
            if (recordable == null) continue;
            recordable.AppendTargets(_recordableTargetsById);
        }
    }

    private static string FindNewestReplayFolder()
    {
        string root = Path.Combine(Application.persistentDataPath, "Replays");
        if (!Directory.Exists(root)) return "";

        string newest = "";
        System.DateTime newestTime = System.DateTime.MinValue;
        foreach (string folder in Directory.GetDirectories(root))
        {
            System.DateTime writeTime = Directory.GetLastWriteTimeUtc(folder);
            if (writeTime <= newestTime) continue;
            newest = folder;
            newestTime = writeTime;
        }

        return newest;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }

    private void ReportStatus(string message, bool warning)
    {
        SetText(statusText, message);
        if (warning) Debug.LogWarning($"[ReplayPlaybackController] {message}");
        else Debug.Log($"[ReplayPlaybackController] {message}");
    }
}