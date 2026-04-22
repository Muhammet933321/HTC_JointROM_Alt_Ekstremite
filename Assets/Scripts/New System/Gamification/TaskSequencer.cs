using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the sequential execution of gamification tasks.
/// Drives countdown → measure → rest cycle for each TaskDefinition.
///
/// Usage:
///   1. Populate taskSequence in Inspector.
///   2. Call StartSession() to begin.
///   3. Subscribe to OnTaskStarted / OnTaskEnded / OnSessionCompleted events.
/// </summary>
public class TaskSequencer : MonoBehaviour
{
    // ───────────────────────── Inspector ─────────────────────────

    [Header("=== Task Sequence ===")]
    [Tooltip("Ordered list of tasks to execute each session.")]
    public List<TaskDefinition> taskSequence = new();

    [Header("=== Auto-start ===")]
    [Tooltip("Begin the session automatically when the scene starts.")]
    public bool autoStartOnAwake = false;

    // ───────────────────────── Events ─────────────────────────

    /// <summary>Fired when a task measurement starts (after countdown).</summary>
    public event Action<TaskDefinition> OnTaskStarted;

    /// <summary>Fired when a task measurement ends. Subscribers (TaskEvaluator) should call back via OnResultReady.</summary>
    public event Action<TaskDefinition> OnTaskEnded;

    /// <summary>Fired when the complete session finishes.</summary>
    public event Action<List<TaskResult>> OnSessionCompleted;

    /// <summary>Countdown tick: fires each second with remaining count (3, 2, 1).</summary>
    public event Action<int> OnCountdownTick;

    /// <summary>Fired during rest period with remaining seconds.</summary>
    public event Action<float> OnRestTick;

    // ───────────────────────── Public State ─────────────────────────

    public bool IsRunning { get; private set; }
    public bool IsInCountdown { get; private set; }
    public bool IsInRest { get; private set; }
    public bool IsMeasuring { get; private set; }

    /// <summary>Currently active task definition (null when idle).</summary>
    public TaskDefinition CurrentTask { get; private set; }

    /// <summary>0-based index of the current task in taskSequence.</summary>
    public int CurrentTaskIndex { get; private set; } = -1;

    /// <summary>Elapsed time in the current measurement phase (seconds).</summary>
    public float CurrentTaskElapsed { get; private set; }

    // ───────────────────────── Internal ─────────────────────────

    private readonly List<TaskResult> _sessionResults = new();
    private Coroutine _sessionCoroutine;

    // ───────────────────────── Unity ─────────────────────────

    private void Start()
    {
        if (autoStartOnAwake)
            StartSession();
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Starts a new session from the beginning of taskSequence.</summary>
    public void StartSession()
    {
        if (IsRunning)
        {
            Debug.LogWarning("[TaskSequencer] Session already running. Stop it first.");
            return;
        }
        if (taskSequence == null || taskSequence.Count == 0)
        {
            Debug.LogWarning("[TaskSequencer] No tasks in taskSequence.");
            return;
        }

        _sessionResults.Clear();
        _sessionCoroutine = StartCoroutine(RunSession());
    }

    /// <summary>Stops the current session immediately.</summary>
    public void StopSession()
    {
        if (_sessionCoroutine != null)
        {
            StopCoroutine(_sessionCoroutine);
            _sessionCoroutine = null;
        }

        ResetState();
        Debug.Log("[TaskSequencer] Session stopped by user.");
    }

    /// <summary>
    /// Called by TaskEvaluator after it has computed the result for the last ended task.
    /// Adds the result to the session log.
    /// </summary>
    public void SubmitTaskResult(TaskResult result)
    {
        if (result == null) return;
        _sessionResults.Add(result);
        Debug.Log($"[TaskSequencer] Result received: {result}");
    }

    // ───────────────────────── Session Coroutine ─────────────────────────

    private IEnumerator RunSession()
    {
        IsRunning = true;
        CurrentTaskIndex = -1;

        for (int i = 0; i < taskSequence.Count; i++)
        {
            TaskDefinition task = taskSequence[i];
            if (task == null) continue;

            CurrentTaskIndex = i;
            CurrentTask = task;
            CurrentTaskElapsed = 0f;

            // ── Countdown ──
            yield return StartCoroutine(RunCountdown(task));

            // ── Measurement ──
            yield return StartCoroutine(RunMeasurement(task));

            // ── Rest ──
            if (task.restAfterSeconds > 0f)
                yield return StartCoroutine(RunRest(task));
        }

        // Session complete
        IsRunning = false;
        CurrentTask = null;
        CurrentTaskIndex = -1;
        OnSessionCompleted?.Invoke(new List<TaskResult>(_sessionResults));
        Debug.Log($"[TaskSequencer] Session complete. {_sessionResults.Count} task(s) recorded.");
    }

    private IEnumerator RunCountdown(TaskDefinition task)
    {
        IsInCountdown = true;
        int totalSeconds = Mathf.Max(1, Mathf.RoundToInt(task.countdownSeconds));

        for (int s = totalSeconds; s > 0; s--)
        {
            OnCountdownTick?.Invoke(s);
            yield return new WaitForSeconds(1f);
        }

        IsInCountdown = false;
    }

    private IEnumerator RunMeasurement(TaskDefinition task)
    {
        IsMeasuring = true;
        CurrentTaskElapsed = 0f;

        OnTaskStarted?.Invoke(task);

        if (task.durationSeconds <= 0f)
        {
            // Unlimited — caller must call StopSession() to end
            while (IsMeasuring)
            {
                CurrentTaskElapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float remaining = task.durationSeconds;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                CurrentTaskElapsed += Time.deltaTime;
                yield return null;
            }
        }

        IsMeasuring = false;
        OnTaskEnded?.Invoke(task);
    }

    private IEnumerator RunRest(TaskDefinition task)
    {
        IsInRest = true;
        float elapsed = 0f;

        while (elapsed < task.restAfterSeconds)
        {
            elapsed += Time.deltaTime;
            OnRestTick?.Invoke(task.restAfterSeconds - elapsed);
            yield return null;
        }

        IsInRest = false;
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void ResetState()
    {
        IsRunning = false;
        IsInCountdown = false;
        IsMeasuring = false;
        IsInRest = false;
        CurrentTask = null;
        CurrentTaskIndex = -1;
        CurrentTaskElapsed = 0f;
    }
}
