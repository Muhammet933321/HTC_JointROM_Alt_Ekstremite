using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR world-space Canvas UI controller for the Denge Kahramanı gamification system.
///
/// Panels:
///   - Task Panel:   task name, instructions, countdown
///   - Live Panel:   real-time valgus bars, sway bar, live score
///   - Rest Panel:   "rest" message + timer
///   - Summary Panel: per-task results + session score after session ends
///
/// All TMP / Image references are optional — assign only the ones you create in the scene.
/// </summary>
public class GameUIController : MonoBehaviour
{
    // ───────────────────────── Dependencies ─────────────────────────

    [Header("=== Dependencies ===")]
    public TaskSequencer sequencer;
    public TaskEvaluator evaluator;
    public GameScoreManager scoreManager;
    public LowerLimbBiometrics biometrics;

    // ───────────────────────── Task Panel ─────────────────────────

    [Header("=== Task Panel ===")]
    public GameObject taskPanel;

    [Tooltip("Large task name text, e.g. 'Mini Squat'.")]
    public TMP_Text taskNameText;

    [Tooltip("Instruction text shown to user.")]
    public TMP_Text instructionText;

    [Tooltip("Countdown number (3, 2, 1...).")]
    public TMP_Text countdownText;

    [Tooltip("Task index progress, e.g. 'Görev 2 / 5'.")]
    public TMP_Text taskProgressText;

    // ───────────────────────── Live Panel ─────────────────────────

    [Header("=== Live Feedback Panel ===")]
    public GameObject livePanel;

    [Tooltip("Left valgus angle label.")]
    public TMP_Text leftValgusText;

    [Tooltip("Right valgus angle label.")]
    public TMP_Text rightValgusText;

    [Tooltip("Sway RMS label (mm).")]
    public TMP_Text swayText;

    [Tooltip("Live symmetry index label.")]
    public TMP_Text symmetryText;

    [Tooltip("Live score label (0-100).")]
    public TMP_Text liveScoreText;

    [Tooltip("Left valgus bar fill (Image with Fill method).")]
    public Image leftValgusBar;

    [Tooltip("Right valgus bar fill.")]
    public Image rightValgusBar;

    [Tooltip("Sway RMS bar fill.")]
    public Image swayBar;

    [Tooltip("Valgus bar colour when safe (< 4°).")]
    [SerializeField] private Color barColorGood    = new Color(0f, 0.78f, 0.33f);

    [Tooltip("Valgus bar colour when warning (4–8°).")]
    [SerializeField] private Color barColorWarning = new Color(1f, 0.84f, 0f);

    [Tooltip("Valgus bar colour when risky (≥ 8°).")]
    [SerializeField] private Color barColorRisk    = new Color(0.84f, 0f, 0f);

    // ───────────────────────── Rest Panel ─────────────────────────

    [Header("=== Rest Panel ===")]
    public GameObject restPanel;
    public TMP_Text restTimerText;

    // ───────────────────────── Summary Panel ─────────────────────────

    [Header("=== Summary Panel ===")]
    public GameObject summaryPanel;

    [Tooltip("Session average score label.")]
    public TMP_Text sessionScoreText;

    [Tooltip("Per-task result list text (multi-line).")]
    public TMP_Text taskResultsListText;

    // ───────────────────────── Private ─────────────────────────

    private const float ValgusBarMax  = 20f; // degrees for full bar
    private const float SwayBarMax    = 0.04f; // metres for full bar

    // ───────────────────────── Unity ─────────────────────────

    private void OnEnable()
    {
        if (sequencer != null)
        {
            sequencer.OnTaskStarted     += HandleTaskStarted;
            sequencer.OnTaskEnded       += HandleTaskEnded;
            sequencer.OnSessionCompleted += HandleSessionCompleted;
            sequencer.OnCountdownTick   += HandleCountdownTick;
            sequencer.OnRestTick        += HandleRestTick;
        }

        if (evaluator != null)
            evaluator.OnResultReady += HandleResultReady;
    }

    private void OnDisable()
    {
        if (sequencer != null)
        {
            sequencer.OnTaskStarted     -= HandleTaskStarted;
            sequencer.OnTaskEnded       -= HandleTaskEnded;
            sequencer.OnSessionCompleted -= HandleSessionCompleted;
            sequencer.OnCountdownTick   -= HandleCountdownTick;
            sequencer.OnRestTick        -= HandleRestTick;
        }

        if (evaluator != null)
            evaluator.OnResultReady -= HandleResultReady;
    }

    private void Start()
    {
        ShowPanel(taskPanel,    false);
        ShowPanel(livePanel,    false);
        ShowPanel(restPanel,    false);
        ShowPanel(summaryPanel, false);
    }

    private void Update()
    {
        if (biometrics == null || !sequencer.IsMeasuring) return;
        UpdateLiveBars();
    }

    // ───────────────────────── Event Handlers ─────────────────────────

    private void HandleTaskStarted(TaskDefinition task)
    {
        ShowPanel(taskPanel,    true);
        ShowPanel(livePanel,    true);
        ShowPanel(restPanel,    false);
        ShowPanel(summaryPanel, false);

        SetText(taskNameText,     task.taskNameTR);
        SetText(instructionText,  task.instructionsTR);
        SetText(countdownText,    "");
        SetText(taskProgressText, $"Görev {sequencer.CurrentTaskIndex + 1} / {sequencer.taskSequence.Count}");
        SetText(liveScoreText,    "");
    }

    private void HandleTaskEnded(TaskDefinition task)
    {
        SetText(countdownText, "");
    }

    private void HandleCountdownTick(int remaining)
    {
        ShowPanel(taskPanel, true);
        SetText(countdownText, remaining > 0 ? remaining.ToString() : "Başla!");

        if (sequencer.CurrentTask != null)
        {
            SetText(taskNameText,    sequencer.CurrentTask.taskNameTR);
            SetText(instructionText, sequencer.CurrentTask.instructionsTR);
        }
    }

    private void HandleRestTick(float remainingSeconds)
    {
        ShowPanel(restPanel, true);
        ShowPanel(livePanel, false);
        if (restTimerText != null)
            restTimerText.text = $"Dinlenme: {remainingSeconds:F1}s";
    }

    private void HandleResultReady(TaskResult result)
    {
        ShowPanel(restPanel, false);

        if (liveScoreText != null)
            liveScoreText.text = $"Skor: {result.GameScore:F0}";
    }

    private void HandleSessionCompleted(List<TaskResult> results)
    {
        ShowPanel(taskPanel,    false);
        ShowPanel(livePanel,    false);
        ShowPanel(restPanel,    false);
        ShowPanel(summaryPanel, true);

        BuildSummary(results);
    }

    // ───────────────────────── Live Bars ─────────────────────────

    private void UpdateLiveBars()
    {
        float lv = Mathf.Abs(biometrics.LeftValgusAngle);
        float rv = Mathf.Abs(biometrics.RightValgusAngle);
        float sw = biometrics.PelvisSwayRMS;
        bool showReach = sequencer != null && sequencer.CurrentTask != null
            && TaskResult.HasReachMetric(sequencer.CurrentTask.taskType);

        SetText(leftValgusText,  $"Sol Valgus:  {lv:F1}°");
        SetText(rightValgusText, $"Sağ Valgus: {rv:F1}°");
        SetText(swayText,        $"Sway: {sw * 1000f:F1} mm");
        if (showReach)
        {
            float reach = sequencer.CurrentTask.taskType == TaskType.ModifiedYBalanceAnterior_R
                ? biometrics.RightStanceAnteriorReachPct
                : biometrics.LeftStanceAnteriorReachPct;
            SetText(symmetryText, $"Anterior Reach: {reach:F1}%");
        }
        else
        {
            SetText(symmetryText, $"Simetri: {biometrics.SymmetryIndex:F1}%");
        }

        UpdateBar(leftValgusBar,  lv / ValgusBarMax,  ValgusBarColor(lv));
        UpdateBar(rightValgusBar, rv / ValgusBarMax,  ValgusBarColor(rv));
        UpdateBar(swayBar,        sw / SwayBarMax,    barColorWarning);

        if (scoreManager != null)
            SetText(liveScoreText, $"Oturum: {scoreManager.SessionScore:F0}");
    }

    private Color ValgusBarColor(float deg)
    {
        if (deg < 4f) return barColorGood;
        if (deg < 8f) return barColorWarning;
        return barColorRisk;
    }

    private static void UpdateBar(Image bar, float fill, Color color)
    {
        if (bar == null) return;
        bar.fillAmount = Mathf.Clamp01(fill);
        bar.color = color;
    }

    // ───────────────────────── Summary ─────────────────────────

    private void BuildSummary(List<TaskResult> results)
    {
        if (scoreManager != null && sessionScoreText != null)
            sessionScoreText.text = $"Oturum Skoru: {scoreManager.SessionScore:F0} / 100";

        if (taskResultsListText == null) return;

        var sb = new System.Text.StringBuilder();
        foreach (var r in results)
        {
            string reachText = TaskResult.HasReachMetric(r.TaskType)
                ? $"  Erişim Uyarısı: {TaskResult.RiskLabel(r.ReachRiskScore)}"
                : "";

            sb.AppendLine($"<b>{r.TaskNameTR}</b>  →  {r.GameScore:F0} puan");
            sb.AppendLine($"  Valgus Uyarısı: {TaskResult.RiskLabel(r.ValgusRiskScore)}  " +
                          $"Denge Uyarısı: {TaskResult.RiskLabel(r.BalanceRiskScore)}  " +
                          $"Asimetri Uyarısı: {TaskResult.RiskLabel(r.AsymmetryRiskScore)}  " +
                          $"Fleksiyon Uyarısı: {TaskResult.RiskLabel(r.FlexionRiskScore)}{reachText}");
            if (!string.IsNullOrEmpty(r.TaskSummaryTR))
                sb.AppendLine($"  Not: {r.TaskSummaryTR}");
            if (!string.IsNullOrEmpty(r.PostureRecommendationTR))
                sb.AppendLine($"  Öneri: {r.PostureRecommendationTR}");
            sb.AppendLine();
        }
        taskResultsListText.text = sb.ToString();
    }

    // ───────────────────────── Public API (for GameFlowController) ─────────────────────────

    /// <summary>
    /// Shows or hides all game-phase panels (task, live, rest, summary).
    /// Called by GameFlowController during state transitions.
    /// </summary>
    public void SetGamePanelsVisible(bool visible)
    {
        // When hiding, hide everything. When showing, let event handlers control individual panels.
        if (!visible)
        {
            ShowPanel(taskPanel,    false);
            ShowPanel(livePanel,    false);
            ShowPanel(restPanel,    false);
            ShowPanel(summaryPanel, false);
        }
        // Showing is handled by individual event handlers (HandleTaskStarted, HandleSessionCompleted, etc.)
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static void ShowPanel(GameObject panel, bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }
}
