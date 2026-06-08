using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public class ReplayRecorder : MonoBehaviour
{
    [Header("=== Dependencies ===")]
    public GameFlowController gameFlow;
    public TaskSequencer sequencer;
    public TaskEvaluator evaluator;
    public SessionReportWriter reportWriter;
    public FullBodyCalibrator calibrator;
    public FullBodyTrackingManager trackingManager;
    public FullBodyIKSolver ikSolver;
    public LowerLimbBiometrics biometrics;

    [Header("=== Recording ===")]
    [SerializeField] private bool autoRecordSession = true;
    [SerializeField] private string outputFolder = "Replays";
    [SerializeField] private float sampleRateHz = 30f;
    [SerializeField] private bool recordSourceDevices = true;
    [SerializeField] private bool recordIkTargets = true;
    [SerializeField] private bool recordAvatarBones = true;
    [SerializeField] private bool recordAdditionalRecordables = true;
    [SerializeField] private bool recordMetrics = true;
    [SerializeField] private bool recordCountdownAndRestFrames = true;
    [SerializeField] private int flushInterval = 60;

    [Header("=== Additional Scene Objects ===")]
    [SerializeField] private bool autoFindRecordables = true;
    [SerializeField] private ReplayRecordable[] additionalRecordables;

    [Header("=== Manual Marker ===")]
    [SerializeField] private Key markerKey = Key.F12;

    private readonly ReplayFileWriter _writer = new();
    private readonly List<Transform> _avatarBones = new();
    private readonly List<string> _avatarBoneIds = new();
    private ReplayManifest _manifest;
    private bool _recording;
    private bool _finalizing;
    private float _recordStartRealtime;
    private float _sampleTimer;
    private int _frameIndex;
    private int _eventIndex;
    private int _lastRestSecond = int.MinValue;

    public bool IsRecording => _recording;
    public string CurrentReplayFolder => _writer.FolderPath;

    public bool IsReadyForSession(out string reason)
    {
        AutoFindDependencies();

        if (!isActiveAndEnabled)
        {
            reason = "ReplayRecorder aktif değil; oyun tekrarı kaydı alınamaz.";
            return false;
        }

        if (!autoRecordSession)
        {
            reason = "ReplayRecorder autoRecordSession kapalı; session başlarken kayıt başlamaz.";
            return false;
        }

        if (sequencer == null)
        {
            reason = "ReplayRecorder TaskSequencer referansı bulamadı.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            reason = "ReplayRecorder outputFolder boş; replay kayıt klasörü oluşturulamaz.";
            return false;
        }

        reason = "ReplayRecorder hazır.";
        return true;
    }

    private void Awake()
    {
        AutoFindDependencies();
        RefreshRecordables();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        FinalizeRecording("component_disabled");
    }

    private void OnApplicationQuit()
    {
        FinalizeRecording("application_quit");
    }

    private void Update()
    {
        if (Keyboard.current != null && markerKey != Key.None && Keyboard.current[markerKey].wasPressedThisFrame)
            AddManualMarker("keyboard_marker");

        if (autoRecordSession && !_recording && sequencer != null && sequencer.IsRunning)
            BeginRecording("session_detected");
    }

    private void LateUpdate()
    {
        if (!_recording || _finalizing) return;
        if (!ShouldRecordFrame()) return;

        float interval = sampleRateHz > 0f ? 1f / sampleRateHz : 1f / 30f;
        _sampleTimer += Time.deltaTime;
        if (_sampleTimer < interval) return;

        _sampleTimer -= interval;
        WriteFrame();
    }

    public void BeginRecording(string reason = "manual")
    {
        if (_recording) return;

        AutoFindDependencies();
        RefreshRecordables();

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string recordingId = $"replay_{timestamp}";
        string folder = Path.Combine(Application.persistentDataPath, outputFolder, recordingId);

        _writer.FlushInterval = flushInterval;
        _writer.Open(folder);
        _recordStartRealtime = Time.realtimeSinceStartup;
        _sampleTimer = 999f;
        _frameIndex = 0;
        _eventIndex = 0;
        _lastRestSecond = int.MinValue;
        _finalizing = false;
        _recording = true;

        _manifest = BuildManifest(recordingId, timestamp);
        _writer.WriteManifest(_manifest);
        WriteEvent("recording_started", reason, null, null);
        Debug.Log($"[ReplayRecorder] Recording started: {folder}");
        Debug.Log($"[ReplayRecorder] Expected files: {Path.Combine(folder, "manifest.json")}, {Path.Combine(folder, "events.jsonl")}, {Path.Combine(folder, "frames.jsonl")}");
    }

    public void AddManualMarker(string message)
    {
        if (!_recording) return;
        WriteEvent("manual_marker", message, sequencer != null ? sequencer.CurrentTask : null, null);
    }

    public void FinalizeRecording(string reason = "manual")
    {
        if (!_recording || _finalizing) return;

        _finalizing = true;
        WriteEvent("recording_stopped", reason, sequencer != null ? sequencer.CurrentTask : null, null);
        UpdateManifestFinalFields();
        _writer.WriteManifest(_manifest);
        _writer.Close();
        _recording = false;
        _finalizing = false;
        Debug.Log($"[ReplayRecorder] Recording finalized: {CurrentReplayFolder}");
    }

    private void Subscribe()
    {
        if (sequencer != null)
        {
            sequencer.OnSessionStarted += HandleSessionStarted;
            sequencer.OnSessionStopped += HandleSessionStopped;
            sequencer.OnCountdownTick += HandleCountdownTick;
            sequencer.OnTaskStarted += HandleTaskStarted;
            sequencer.OnTaskEnded += HandleTaskEnded;
            sequencer.OnRestTick += HandleRestTick;
            sequencer.OnSessionCompleted += HandleSessionCompleted;
        }

        if (evaluator != null)
            evaluator.OnResultReady += HandleResultReady;
    }

    private void Unsubscribe()
    {
        if (sequencer != null)
        {
            sequencer.OnSessionStarted -= HandleSessionStarted;
            sequencer.OnSessionStopped -= HandleSessionStopped;
            sequencer.OnCountdownTick -= HandleCountdownTick;
            sequencer.OnTaskStarted -= HandleTaskStarted;
            sequencer.OnTaskEnded -= HandleTaskEnded;
            sequencer.OnRestTick -= HandleRestTick;
            sequencer.OnSessionCompleted -= HandleSessionCompleted;
        }

        if (evaluator != null)
            evaluator.OnResultReady -= HandleResultReady;
    }

    private void HandleSessionStarted()
    {
        if (autoRecordSession && !_recording)
            BeginRecording("session_started");

        WriteEvent("session_started", "", sequencer != null ? sequencer.CurrentTask : null, null);
    }

    private void HandleSessionStopped()
    {
        if (!_recording) return;
        WriteEvent("session_stopped", "", null, null);
        FinalizeRecording("session_stopped");
    }

    private void HandleCountdownTick(int remaining)
    {
        WriteEvent("countdown_tick", remaining.ToString(CultureInfo.InvariantCulture), sequencer != null ? sequencer.CurrentTask : null, null);
    }

    private void HandleTaskStarted(TaskDefinition task)
    {
        WriteEvent("task_started", "", task, null);
    }

    private void HandleTaskEnded(TaskDefinition task)
    {
        WriteEvent("task_ended", "", task, null);
    }

    private void HandleRestTick(float remainingSeconds)
    {
        int remainingWholeSeconds = Mathf.CeilToInt(remainingSeconds);
        if (remainingWholeSeconds == _lastRestSecond) return;

        _lastRestSecond = remainingWholeSeconds;
        WriteEvent("rest_tick", remainingWholeSeconds.ToString(CultureInfo.InvariantCulture), sequencer != null ? sequencer.CurrentTask : null, null);
    }

    private void HandleResultReady(TaskResult result)
    {
        WriteEvent("result_ready", "", sequencer != null ? sequencer.CurrentTask : null, result);
    }

    private void HandleSessionCompleted(List<TaskResult> results)
    {
        if (!_recording) return;
        WriteEvent("session_completed", results != null ? results.Count.ToString(CultureInfo.InvariantCulture) : "0", null, null);
        StartCoroutine(FinalizeAfterReportWriter());
    }

    private IEnumerator FinalizeAfterReportWriter()
    {
        yield return null;
        FinalizeRecording("session_completed");
    }

    private bool ShouldRecordFrame()
    {
        if (sequencer == null) return true;
        if (!sequencer.IsRunning && gameFlow != null && gameFlow.CurrentState != GameFlowController.GameState.SessionRunning)
            return false;

        return recordCountdownAndRestFrames || sequencer.IsMeasuring;
    }

    private void WriteFrame()
    {
        var frame = new ReplayFrame
        {
            frameIndex = _frameIndex++,
            elapsedSeconds = ElapsedSeconds,
            phase = CurrentPhase,
            taskIndex = sequencer != null ? sequencer.CurrentTaskIndex : -1,
            taskType = sequencer != null && sequencer.CurrentTask != null ? sequencer.CurrentTask.taskType.ToString() : "",
            taskNameTR = sequencer != null && sequencer.CurrentTask != null ? sequencer.CurrentTask.taskNameTR : "",
            taskElapsedSeconds = sequencer != null ? sequencer.CurrentTaskElapsed : 0f,
            status = CreateCalibrationSnapshot(),
            metrics = recordMetrics ? ReplayMetricSnapshot.FromBiometrics(biometrics) : null
        };
        frame.isDataGap = biometrics != null && !biometrics.IsLeftLegAvailable && !biometrics.IsRightLegAvailable;

        if (recordSourceDevices) CollectSourcePoses(frame.sourcePoses);
        if (recordIkTargets) CollectIkTargetPoses(frame.ikTargetPoses);
        if (recordAvatarBones) CollectAvatarBonePoses(frame.avatarBonePoses);
        if (recordAdditionalRecordables) CollectRecordablePoses(frame.recordablePoses);

        _manifest.frameCount = _frameIndex;
        _manifest.durationSeconds = frame.elapsedSeconds;
        _writer.WriteFrame(frame);
    }

    private void WriteEvent(string eventType, string message, TaskDefinition task, TaskResult result)
    {
        if (!_recording || _writer == null || !_writer.IsOpen) return;

        var replayEvent = new ReplayEvent
        {
            eventIndex = _eventIndex++,
            elapsedSeconds = ElapsedSeconds,
            eventType = eventType,
            phase = CurrentPhase,
            taskIndex = sequencer != null ? sequencer.CurrentTaskIndex : -1,
            taskType = task != null ? task.taskType.ToString() : "",
            taskNameTR = task != null ? task.taskNameTR : "",
            message = message,
            taskContext = task != null ? ReplayTaskContext.FromTask(task, sequencer != null ? sequencer.CurrentTaskIndex : -1) : null,
            result = ReplayTaskResultSnapshot.FromResult(result)
        };

        _manifest.eventCount = _eventIndex;
        _writer.WriteEvent(replayEvent);
    }

    private ReplayManifest BuildManifest(string recordingId, string timestamp)
    {
        var manifest = new ReplayManifest
        {
            recordingId = recordingId,
            sessionTimestamp = timestamp,
            sceneName = SceneManager.GetActiveScene().name,
            applicationVersion = Application.version,
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString(),
            persistentDataPath = Application.persistentDataPath,
            interpretationScopeTR = TaskResult.InterpretationScopeTR,
            recordingSettings = BuildSettingsSnapshot(),
            calibration = CreateCalibrationSnapshot(),
            trackerAssignment = CreateTrackerAssignment(),
            notes = "Data-based replay package. Visual playback uses recorded avatar bones; source device poses are kept for review."
        };

        if (sequencer != null && sequencer.sessionConfig != null)
        {
            manifest.sessionName = sequencer.sessionConfig.sessionName;
            manifest.sessionDescriptionTR = sequencer.sessionConfig.descriptionTR;
        }

        if (sequencer != null && sequencer.taskSequence != null)
        {
            for (int taskIndex = 0; taskIndex < sequencer.taskSequence.Count; taskIndex++)
                manifest.tasks.Add(ReplayTaskContext.FromTask(sequencer.taskSequence[taskIndex], taskIndex));
        }

        manifest.taskCount = manifest.tasks.Count;
        return manifest;
    }

    private ReplayRecordingSettings BuildSettingsSnapshot()
    {
        return new ReplayRecordingSettings
        {
            sampleRateHz = sampleRateHz,
            recordSourceDevices = recordSourceDevices,
            recordIkTargets = recordIkTargets,
            recordAvatarBones = recordAvatarBones,
            recordAdditionalRecordables = recordAdditionalRecordables,
            recordMetrics = recordMetrics,
            recordCountdownAndRestFrames = recordCountdownAndRestFrames
        };
    }

    private ReplayCalibrationSnapshot CreateCalibrationSnapshot()
    {
        return new ReplayCalibrationSnapshot
        {
            calibratorCalibrated = calibrator != null && calibrator.IsCalibrated,
            ikCalibrated = ikSolver != null && ikSolver.IsCalibrated,
            trackingAssigned = trackingManager != null && trackingManager.IsAssigned,
            mappingCalibrated = trackingManager != null && trackingManager.IsMappingCalibrated,
            ikCalibrationVersion = ikSolver != null ? ikSolver.CalibrationVersion : 0,
            simulatedInput = biometrics != null && biometrics.useSimulatedInput,
            dataSourceTR = biometrics != null ? biometrics.DataSourceSummaryTR : ""
        };
    }

    private ReplayTrackerAssignment CreateTrackerAssignment()
    {
        var assignment = new ReplayTrackerAssignment();
        if (trackingManager == null) return assignment;

        trackingManager.GetAssignment(
            out assignment.pelvisTrackerIndex,
            out assignment.leftFootTrackerIndex,
            out assignment.rightFootTrackerIndex,
            out assignment.leftKneeTrackerIndex,
            out assignment.rightKneeTrackerIndex);
        return assignment;
    }

    private void UpdateManifestFinalFields()
    {
        if (_manifest == null) return;
        _manifest.calibration = CreateCalibrationSnapshot();
        _manifest.trackerAssignment = CreateTrackerAssignment();
        _manifest.durationSeconds = ElapsedSeconds;
        _manifest.frameCount = _frameIndex;
        _manifest.eventCount = _eventIndex;

        if (reportWriter != null && !string.IsNullOrEmpty(reportWriter.LastJsonPath))
            _manifest.linkedSessionReportJson = reportWriter.LastJsonPath;
    }

    private void CollectSourcePoses(List<ReplayPose> poses)
    {
        if (trackingManager == null) return;

        AddPose(poses, "source/hmd", trackingManager.HmdTransform, _manifest.sourcePoseIds);
        AddPose(poses, "source/controller/left", trackingManager.LeftControllerTransform, _manifest.sourcePoseIds);
        AddPose(poses, "source/controller/right", trackingManager.RightControllerTransform, _manifest.sourcePoseIds);

        if (!trackingManager.GetAssignment(out int pelvisIndex, out int leftFootIndex, out int rightFootIndex, out int leftKneeIndex, out int rightKneeIndex))
            return;

        AddPose(poses, $"source/tracker/pelvis/T{pelvisIndex}", trackingManager.GetTrackerTransform(pelvisIndex), _manifest.sourcePoseIds);
        AddPose(poses, $"source/tracker/leftFoot/T{leftFootIndex}", trackingManager.GetTrackerTransform(leftFootIndex), _manifest.sourcePoseIds);
        AddPose(poses, $"source/tracker/rightFoot/T{rightFootIndex}", trackingManager.GetTrackerTransform(rightFootIndex), _manifest.sourcePoseIds);
        AddPose(poses, $"source/tracker/leftKnee/T{leftKneeIndex}", trackingManager.GetTrackerTransform(leftKneeIndex), _manifest.sourcePoseIds);
        AddPose(poses, $"source/tracker/rightKnee/T{rightKneeIndex}", trackingManager.GetTrackerTransform(rightKneeIndex), _manifest.sourcePoseIds);
    }

    private void CollectIkTargetPoses(List<ReplayPose> poses)
    {
        if (trackingManager == null) return;

        AddPose(poses, "ik/head", trackingManager.HeadIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/leftHand", trackingManager.LeftHandIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/rightHand", trackingManager.RightHandIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/pelvis", trackingManager.PelvisIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/leftFoot", trackingManager.LeftFootIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/rightFoot", trackingManager.RightFootIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/leftKnee", trackingManager.LeftKneeIKTarget, _manifest.ikTargetPoseIds);
        AddPose(poses, "ik/rightKnee", trackingManager.RightKneeIKTarget, _manifest.ikTargetPoseIds);
    }

    private void CollectAvatarBonePoses(List<ReplayPose> poses)
    {
        if (ikSolver == null) return;

        ikSolver.GetReplayAvatarBones(_avatarBones, _avatarBoneIds);
        for (int index = 0; index < _avatarBones.Count; index++)
            AddPose(poses, _avatarBoneIds[index], _avatarBones[index], _manifest.avatarBonePoseIds);
    }

    private void CollectRecordablePoses(List<ReplayPose> poses)
    {
        if (additionalRecordables == null) return;

        for (int index = 0; index < additionalRecordables.Length; index++)
        {
            ReplayRecordable recordable = additionalRecordables[index];
            if (recordable == null) continue;

            int startCount = poses.Count;
            recordable.AppendPoses(poses);
            for (int poseIndex = startCount; poseIndex < poses.Count; poseIndex++)
                RegisterPoseId(poses[poseIndex].id, _manifest.recordablePoseIds);
        }
    }

    private static void AddPose(List<ReplayPose> poses, string id, Transform transform, List<string> manifestIds)
    {
        if (poses == null || transform == null) return;
        poses.Add(ReplayPose.FromTransform(id, transform));
        RegisterPoseId(id, manifestIds);
    }

    private static void RegisterPoseId(string id, List<string> ids)
    {
        if (string.IsNullOrEmpty(id) || ids == null || ids.Contains(id)) return;
        ids.Add(id);
    }

    private void AutoFindDependencies()
    {
        if (gameFlow == null) gameFlow = FindObjectOfType<GameFlowController>();
        if (sequencer == null) sequencer = FindObjectOfType<TaskSequencer>();
        if (evaluator == null) evaluator = FindObjectOfType<TaskEvaluator>();
        if (reportWriter == null) reportWriter = FindObjectOfType<SessionReportWriter>();
        if (calibrator == null) calibrator = FindObjectOfType<FullBodyCalibrator>();
        if (trackingManager == null) trackingManager = FindObjectOfType<FullBodyTrackingManager>();
        if (ikSolver == null) ikSolver = FindObjectOfType<FullBodyIKSolver>();
        if (biometrics == null) biometrics = FindObjectOfType<LowerLimbBiometrics>();
    }

    private void RefreshRecordables()
    {
        if (!autoFindRecordables || additionalRecordables != null && additionalRecordables.Length > 0) return;
        additionalRecordables = FindObjectsOfType<ReplayRecordable>();
    }

    private float ElapsedSeconds => _recording ? Time.realtimeSinceStartup - _recordStartRealtime : 0f;

    private string CurrentPhase
    {
        get
        {
            if (sequencer != null)
            {
                if (sequencer.IsInCountdown) return "Countdown";
                if (sequencer.IsMeasuring) return "Measurement";
                if (sequencer.IsInRest) return "Rest";
                if (sequencer.IsRunning) return "SessionRunning";
            }

            return gameFlow != null ? gameFlow.CurrentState.ToString() : "Idle";
        }
    }
}