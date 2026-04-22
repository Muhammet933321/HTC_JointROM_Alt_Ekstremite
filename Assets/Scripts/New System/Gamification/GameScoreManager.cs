using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Accumulates per-task scores into a running session total.
/// Listens to TaskEvaluator.OnResultReady.
///
/// Expose OnScoreUpdated UnityEvent to bind UI elements in Inspector.
/// </summary>
public class GameScoreManager : MonoBehaviour
{
    // ───────────────────────── Inspector ─────────────────────────

    [Header("=== Dependencies ===")]
    public TaskEvaluator evaluator;

    [Header("=== Events ===")]
    [Tooltip("Fired whenever the session score changes. Parameter: new cumulative average score [0-100].")]
    public UnityEvent<float> OnScoreUpdated;

    [Tooltip("Fired when each task result arrives. Parameter: task game score [0-100].")]
    public UnityEvent<float> OnTaskScoreReceived;

    // ───────────────────────── Public State ─────────────────────────

    /// <summary>Cumulative average game score for this session [0-100].</summary>
    public float SessionScore { get; private set; }

    /// <summary>Most recent single-task game score [0-100].</summary>
    public float LastTaskScore { get; private set; }

    /// <summary>Number of tasks completed in this session.</summary>
    public int CompletedTaskCount { get; private set; }

    /// <summary>All results received this session (in order).</summary>
    public IReadOnlyList<TaskResult> AllResults => _results;

    // ───────────────────────── Private ─────────────────────────

    private readonly List<TaskResult> _results = new();

    // ───────────────────────── Unity ─────────────────────────

    private void OnEnable()
    {
        if (evaluator != null)
            evaluator.OnResultReady += HandleResultReady;
    }

    private void OnDisable()
    {
        if (evaluator != null)
            evaluator.OnResultReady -= HandleResultReady;
    }

    // ───────────────────────── API ─────────────────────────

    /// <summary>Resets all session data. Call before starting a new session.</summary>
    public void ResetSession()
    {
        _results.Clear();
        SessionScore = 0f;
        LastTaskScore = 0f;
        CompletedTaskCount = 0;
        OnScoreUpdated?.Invoke(SessionScore);
    }

    // ───────────────────────── Handler ─────────────────────────

    private void HandleResultReady(TaskResult result)
    {
        _results.Add(result);
        CompletedTaskCount++;

        LastTaskScore = result.GameScore;

        // Running average
        float sum = 0f;
        foreach (var r in _results) sum += r.GameScore;
        SessionScore = sum / _results.Count;

        OnTaskScoreReceived?.Invoke(LastTaskScore);
        OnScoreUpdated?.Invoke(SessionScore);

        Debug.Log($"[GameScoreManager] Task \"{result.TaskNameTR}\" score: {LastTaskScore:F1} | Session avg: {SessionScore:F1}");
    }
}
