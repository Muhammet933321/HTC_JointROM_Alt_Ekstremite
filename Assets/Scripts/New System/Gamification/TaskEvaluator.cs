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

        _valgusL.Add(biometrics.LeftValgusAngle);
        _valgusR.Add(biometrics.RightValgusAngle);
        _flexL.Add(biometrics.LeftKneeFlexion);
        _flexR.Add(biometrics.RightKneeFlexion);
        _sway.Add(biometrics.PelvisSwayRMS);
        _swayVel.Add(biometrics.SwayVelocity);
        _si.Add(biometrics.SymmetryIndex);
        _reachLeftStance.Add(biometrics.LeftStanceAnteriorReachPct);
        _reachRightStance.Add(biometrics.RightStanceAnteriorReachPct);
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
                measuredDurationSec: 0f,
                meanValgusLeft: 0f,
                meanValgusRight: 0f,
                maxValgusLeft: 0f,
                maxValgusRight: 0f,
                meanFlexLeft: 0f,
                meanFlexRight: 0f,
                maxFlexLeft: 0f,
                maxFlexRight: 0f,
                meanSwayRMS: 0f,
                meanSwayVelocity: 0f,
                symmetryIndex: 0f,
                maxLeftStanceAnteriorReachPct: 0f,
                maxRightStanceAnteriorReachPct: 0f,
                swayThreshold: task.swayRmsThreshold,
                targetReachPct: task.targetReachPct,
                landingFlexionTargetDeg: task.landingFlexionTargetDeg);
        }

        float measuredDuration = _valgusL.Count * Time.deltaTime;

        return TaskResult.Compute(
            taskType:           task.taskType,
            taskNameTR:         task.taskNameTR,
            measuredDurationSec: measuredDuration,

            meanValgusLeft:  Mean(_valgusL),
            meanValgusRight: Mean(_valgusR),
            maxValgusLeft:   Max(_valgusL),
            maxValgusRight:  Max(_valgusR),

            meanFlexLeft:    Mean(_flexL),
            meanFlexRight:   Mean(_flexR),
            maxFlexLeft:     Max(_flexL),
            maxFlexRight:    Max(_flexR),

            meanSwayRMS:     Mean(_sway),
            meanSwayVelocity: Mean(_swayVel),
            symmetryIndex:   Mean(_si),

                maxLeftStanceAnteriorReachPct: Max(_reachLeftStance),
                maxRightStanceAnteriorReachPct: Max(_reachRightStance),
                swayThreshold:   task.swayRmsThreshold,
                targetReachPct:  task.targetReachPct,
                landingFlexionTargetDeg: task.landingFlexionTargetDeg);
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
}
