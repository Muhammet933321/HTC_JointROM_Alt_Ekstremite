using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads LowerLimbBiometrics every frame during an active task,
/// accumulates raw data, and produces a TaskResult when the task ends.
///
/// Wiring:
///   - Assign biometrics and sequencer in Inspector.
///   - OnResultReady event → connect GameScoreManager, SessionReportWriter, GameUIController.
/// </summary>
public class TaskEvaluator : MonoBehaviour
{
    // ───────────────────────── Inspector ─────────────────────────

    [Header("=== Dependencies ===")]
    public LowerLimbBiometrics biometrics;
    public TaskSequencer sequencer;

    // ───────────────────────── Events ─────────────────────────

    /// <summary>Fired once a TaskResult has been computed after each task ends.</summary>
    public event Action<TaskResult> OnResultReady;

    // ───────────────────────── Public State ─────────────────────────

    public bool IsCollecting { get; private set; }
    public TaskDefinition ActiveTask { get; private set; }

    // ───────────────────────── Sample Buffers ─────────────────────────

    private readonly List<float> _valgusL = new();
    private readonly List<float> _valgusR = new();
    private readonly List<float> _flexL   = new();
    private readonly List<float> _flexR   = new();
    private readonly List<float> _sway    = new();
    private readonly List<float> _swayVel = new();
    private readonly List<float> _si      = new();
    private readonly List<float> _reachLeftStance  = new();
    private readonly List<float> _reachRightStance = new();
    private readonly List<float> _valgusVelL = new();
    private readonly List<float> _valgusVelR = new();
    private readonly List<float> _jerkL = new();
    private readonly List<float> _jerkR = new();
    private float _collectedDuration;
    private int _missingFrames;

    // Eşik üzerinde kalan frame sayaçları (Q84 valgus süresi, Q89 asimetri yüzdesi)
    private const float ValgusThresholdDeg    = 8f;
    private const float AsymmetryThresholdPct = 10f;
    private int _valgusLAboveCount;
    private int _valgusRAboveCount;
    private int _asymmetryAboveCount;
    private int _totalFrames;
    private const float SampleInterval = 1f / 50f; // 50 Hz sabit örnekleme (Q55)
    private float _sampleAccumulator = 0f;

    // ───────────────────────── Unity ─────────────────────────

    private void OnEnable()
    {
        if (sequencer == null) return;
        sequencer.OnTaskStarted += HandleTaskStarted;
        sequencer.OnTaskEnded   += HandleTaskEnded;
    }

    private void OnDisable()
    {
        if (sequencer == null) return;
        sequencer.OnTaskStarted -= HandleTaskStarted;
        sequencer.OnTaskEnded   -= HandleTaskEnded;
    }

    private void Update()
    {
        if (!IsCollecting || biometrics == null) return;

        _collectedDuration += Time.deltaTime;
        _sampleAccumulator += Time.deltaTime;

        while (_sampleAccumulator >= SampleInterval)
        {
            _sampleAccumulator -= SampleInterval;
            CollectSample();
        }
    }

    private void CollectSample()
    {
        _valgusL.Add(biometrics.LeftValgusAngle);
        _valgusR.Add(biometrics.RightValgusAngle);
        _flexL.Add(biometrics.LeftKneeFlexion);
        _flexR.Add(biometrics.RightKneeFlexion);
        _sway.Add(biometrics.PelvisSwayRMS);
        _swayVel.Add(biometrics.SwayVelocity);
        _si.Add(biometrics.SymmetryIndex);
        _reachLeftStance.Add(biometrics.LeftStanceAnteriorReachPct);
        _reachRightStance.Add(biometrics.RightStanceAnteriorReachPct);
        _valgusVelL.Add(biometrics.LeftValgusAngularVelocity);
        _valgusVelR.Add(biometrics.RightValgusAngularVelocity);
        _jerkL.Add(biometrics.LeftValgusJerk);
        _jerkR.Add(biometrics.RightValgusJerk);

        if (biometrics.LeftValgusAngle  > ValgusThresholdDeg)    _valgusLAboveCount++;
        if (biometrics.RightValgusAngle > ValgusThresholdDeg)    _valgusRAboveCount++;
        if (biometrics.SymmetryIndex    > AsymmetryThresholdPct) _asymmetryAboveCount++;
        if (!biometrics.IsLeftLegAvailable && !biometrics.IsRightLegAvailable) _missingFrames++;
        _totalFrames++;
    }

    // ───────────────────────── Task Lifecycle Handlers ─────────────────────────

    private void HandleTaskStarted(TaskDefinition task)
    {
        ActiveTask = task;
        ClearBuffers();
        IsCollecting = true;
    }

    private void HandleTaskEnded(TaskDefinition task)
    {
        IsCollecting = false;
        TaskResult result = BuildResult(task);
        ActiveTask = null;

        // Report back to sequencer so it can log the result
        sequencer.SubmitTaskResult(result);

        OnResultReady?.Invoke(result);
    }

    // ───────────────────────── Result Builder ─────────────────────────

    private TaskResult BuildResult(TaskDefinition task)
    {
        if (_valgusL.Count == 0)
        {
            // No data collected (e.g. task stopped immediately)
            return TaskResult.Compute(
                taskType: task.taskType,
                taskNameTR: task.taskNameTR,
                measuredDurationSec: _collectedDuration,
                meanValgusLeft: 0f, meanValgusRight: 0f,
                maxValgusLeft: 0f,  maxValgusRight: 0f,
                meanFlexLeft: 0f,   meanFlexRight: 0f,
                maxFlexLeft: 0f,    maxFlexRight: 0f,
                meanSwayRMS: 0f,    meanSwayVelocity: 0f,
                symmetryIndex: 0f,
                maxLeftStanceAnteriorReachPct: 0f,
                maxRightStanceAnteriorReachPct: 0f,
                swayThreshold: task.swayRmsThreshold,
                targetReachPct: task.targetReachPct,
                landingFlexionTargetDeg: task.landingFlexionTargetDeg);
        }

        float meanDt = _totalFrames > 0 ? _collectedDuration / _totalFrames : 0f;

        return TaskResult.Compute(
            taskType:            task.taskType,
            taskNameTR:          task.taskNameTR,
            measuredDurationSec: _collectedDuration,

            meanValgusLeft:  Mean(_valgusL),
            meanValgusRight: Mean(_valgusR),
            maxValgusLeft:   Max(_valgusL),
            maxValgusRight:  Max(_valgusR),

            meanFlexLeft:    Mean(_flexL),
            meanFlexRight:   Mean(_flexR),
            maxFlexLeft:     Max(_flexL),
            maxFlexRight:    Max(_flexR),

            meanSwayRMS:      Mean(_sway),
            meanSwayVelocity: Mean(_swayVel),
            symmetryIndex:    Mean(_si),

            maxLeftStanceAnteriorReachPct:  Max(_reachLeftStance),
            maxRightStanceAnteriorReachPct: Max(_reachRightStance),
            swayThreshold:           task.swayRmsThreshold,
            targetReachPct:          task.targetReachPct,
            landingFlexionTargetDeg: task.landingFlexionTargetDeg,

            // Yeni istatistikler
            minFlexLeft:    Min(_flexL),
            minFlexRight:   Min(_flexR),
            stdFlexLeft:    StdDev(_flexL),
            stdFlexRight:   StdDev(_flexR),
            valgusLAboveThreshSec: _valgusLAboveCount * meanDt,
            valgusRAboveThreshSec: _valgusRAboveCount * meanDt,
            asymmetryAbovePct:     _totalFrames > 0 ? _asymmetryAboveCount * 100f / _totalFrames : 0f,
            meanValgusAngularVelLeft:  Mean(_valgusVelL),
            meanValgusAngularVelRight: Mean(_valgusVelR),
            jerkRmsLeft:  StdDev(_jerkL),
            jerkRmsRight: StdDev(_jerkR),
            dataQualityPct: _totalFrames > 0
                ? (1f - (float)_missingFrames / _totalFrames) * 100f
                : 100f);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void ClearBuffers()
    {
        _valgusL.Clear(); _valgusR.Clear();
        _flexL.Clear();   _flexR.Clear();
        _sway.Clear();    _swayVel.Clear();
        _si.Clear();
        _reachLeftStance.Clear();
        _reachRightStance.Clear();
        _valgusVelL.Clear();
        _valgusVelR.Clear();
        _jerkL.Clear();
        _jerkR.Clear();
        _collectedDuration = 0f;
        _sampleAccumulator = 0f;
        _valgusLAboveCount = 0;
        _valgusRAboveCount = 0;
        _asymmetryAboveCount = 0;
        _missingFrames = 0;
        _totalFrames = 0;
    }

    private static float Mean(List<float> list)
    {
        if (list.Count == 0) return 0f;
        float sum = 0f;
        foreach (var v in list) sum += v;
        return sum / list.Count;
    }

    private static float Max(List<float> list)
    {
        if (list.Count == 0) return 0f;
        float max = float.MinValue;
        foreach (var v in list) if (v > max) max = v;
        return max;
    }

    private static float Min(List<float> list)
    {
        if (list.Count == 0) return 0f;
        float min = float.MaxValue;
        foreach (var v in list) if (v < min) min = v;
        return min;
    }

    private static float StdDev(List<float> list)
    {
        if (list.Count < 2) return 0f;
        float mean = Mean(list);
        float sumSq = 0f;
        foreach (var v in list) sumSq += (v - mean) * (v - mean);
        return Mathf.Sqrt(sumSq / list.Count);
    }
}
