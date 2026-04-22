using System;

/// <summary>
/// Immutable data container for the outcome of a single gamification task.
/// All risk scores are in [0, 1]; game score is in [0, 100].
///
/// Evidence-based scoring — key literature:
///   [Hewett 2005]  Peak knee abduction moment predicts ACL injury. Valgus >8-10° = risk zone.
///   [Numata 2017]  2-D DKV cut-off ~10° identifies high-risk female athletes.
///   [Tamura 2017]  Mean (habitual) valgus reflects neuromuscular control quality.
///   [Saki 2024]    Bilateral activation asymmetry between limbs is an independent risk factor.
///   [Maki 1990]    COP velocity > 2 cm/s predicts falls better than RMS displacement alone.
///   [Kaptein 2006] Sway velocity fraction reflects reactive (perturbation) balance control.
/// </summary>
[Serializable]
public class TaskResult
{
    // ───────────────────────── Identity ─────────────────────────

    public TaskType TaskType;
    public string TaskNameTR;

    /// <summary>Duration the task was actually measured, in seconds.</summary>
    public float MeasuredDurationSec;

    // ───────────────────────── Valgus / Varus ─────────────────────────

    public float MeanValgusLeft;
    public float MeanValgusRight;
    public float MaxValgusLeft;
    public float MaxValgusRight;

    // ───────────────────────── Flexion ─────────────────────────

    public float MeanFlexLeft;
    public float MeanFlexRight;
    public float MaxFlexLeft;
    public float MaxFlexRight;

    // ───────────────────────── Sway ─────────────────────────

    public float MeanSwayRMS;
    public float MeanSwayVelocity;

    // ───────────────────────── Symmetry ─────────────────────────

    public float SymmetryIndex;

    /// <summary>Absolute degree difference between left and right peak valgus (Saki 2024).</summary>
    public float BilateralValgusAsymmetry;

    // ───────────────────────── Risk Scores [0, 1] ─────────────────────────

    /// <summary>
    /// Dynamic knee valgus risk — combined mean + peak formulation.
    /// Mean valgus component: safe &lt;5°, full risk at 18° (Tamura 2017 — habitual pattern).
    /// Peak valgus component: safe &lt;8°, full risk at 18° (Hewett 2005, Numata 2017).
    /// Bilateral asymmetry sub-component: &gt;8° L/R difference (Saki 2024).
    /// Weights: 35% mean + 65% peak, then 80% combined + 20% bilateral asym.
    /// </summary>
    public float ValgusRiskScore;

    /// <summary>
    /// Bilateral movement asymmetry risk (Symmetry Index).
    /// Safe: SI &lt;10%; full risk: SI &gt;25% (Saki 2024).
    /// </summary>
    public float AsymmetryRiskScore;

    /// <summary>
    /// Knee flexion range risk — task-type-specific target angles (Hewett 2005).
    /// Squat target: 60°; SingleLeg: 20°; LeanForward: 30°; Standing: 10°.
    /// </summary>
    public float FlexionRiskScore;

    /// <summary>
    /// Balance risk — combined COP velocity (60%) + RMS (40%).
    /// Velocity: safe &lt;20 mm/s, full risk at 50 mm/s (Maki 1990).
    /// RMS: normalised to swayRmsThreshold from TaskDefinition (Kaptein 2006).
    /// </summary>
    public float BalanceRiskScore;

    /// <summary>
    /// Weighted total risk. Weights are task-type-specific (evidence-based matrix).
    /// </summary>
    public float TotalRiskScore;

    /// <summary>
    /// Game-facing score 0–100. Includes small consistency bonus when all sub-risks are low.
    /// </summary>
    public float GameScore;

    /// <summary>4-zone clinical grade: Yeşil / Sarı / Turuncu / Kırmızı.</summary>
    public string RiskGrade;

    // ───────────────────────── Factory ─────────────────────────

    /// <summary>
    /// Computes all risk and game scores from raw accumulated metrics.
    /// Uses evidence-based, task-type-specific formulas (see class summary).
    /// <param name="swayThreshold">TaskDefinition.swayRmsThreshold in metres. Default 0.015 m (15 mm).</param>
    /// </summary>
    public static TaskResult Compute(
        TaskType taskType,
        string taskNameTR,
        float measuredDurationSec,
        float meanValgusLeft,  float meanValgusRight,
        float maxValgusLeft,   float maxValgusRight,
        float meanFlexLeft,    float meanFlexRight,
        float maxFlexLeft,     float maxFlexRight,
        float meanSwayRMS,     float meanSwayVelocity,
        float symmetryIndex,
        float swayThreshold = 0.015f)
    {
        // ── VALGUS RISK ──────────────────────────────────────────────────────────
        // Mean valgus = habitual neuromuscular malalignment (Tamura 2017)
        //   Ramp: 0 at 5°  →  1.0 at 18°
        float meanValgusMax  = Math.Max(meanValgusLeft, meanValgusRight);
        float meanValgusRisk = Clamp01((meanValgusMax - 5f) / 13f);

        // Peak valgus = worst-case ACL loading moment (Hewett 2005, Numata 2017)
        //   Ramp: 0 at 8°  →  1.0 at 18°
        float peakValgusMax  = Math.Max(maxValgusLeft, maxValgusRight);
        float peakValgusRisk = Clamp01((peakValgusMax - 8f) / 10f);

        // Combined: peak weighted higher — Hewett's prospective study was peak-based
        float combinedValgusRisk = 0.35f * meanValgusRisk + 0.65f * peakValgusRisk;

        // Bilateral valgus asymmetry: L/R difference > 8° is independent risk factor (Saki 2024)
        float bilateralValgusAsym = Math.Abs(maxValgusLeft - maxValgusRight);
        float bilateralValgusRisk = Clamp01(bilateralValgusAsym / 8f);

        // Final valgus score: 80% combined + 20% bilateral asymmetry component
        float valgusRisk = Clamp01(0.80f * combinedValgusRisk + 0.20f * bilateralValgusRisk);

        // ── ASYMMETRY RISK ───────────────────────────────────────────────────────
        // Symmetry Index: safe < 10%, full risk at 25% (Saki 2024, general consensus)
        float asymmetryRisk = Clamp01((symmetryIndex - 10f) / 15f);

        // ── FLEXION RISK ─────────────────────────────────────────────────────────
        // Task-specific target angles — low flexion increases stiffness and ACL load (Hewett 2005)
        float flexTarget = GetFlexTarget(taskType);
        float maxFlex    = Math.Max(maxFlexLeft, maxFlexRight);
        float flexionRisk = (maxFlex >= flexTarget || flexTarget <= 0f)
            ? 0f
            : Clamp01(1f - maxFlex / flexTarget);

        // ── BALANCE RISK ─────────────────────────────────────────────────────────
        // COP velocity is a better fall predictor than displacement alone (Maki 1990)
        //   Safe: < 0.020 m/s;  full risk: >= 0.050 m/s
        float swayVelRisk = Clamp01((meanSwayVelocity - 0.020f) / 0.030f);

        // RMS displacement normalised to task threshold (Kaptein 2006)
        float swayRmsRisk = swayThreshold > 0f ? Clamp01(meanSwayRMS / swayThreshold) : 0f;

        // 60% velocity + 40% RMS (Maki 1990: velocity fraction weighted higher)
        float balanceRisk = Clamp01(0.60f * swayVelRisk + 0.40f * swayRmsRisk);

        // ── TASK-TYPE WEIGHTS ────────────────────────────────────────────────────
        GetTaskWeights(taskType, out float wV, out float wA, out float wB, out float wF);

        float totalRisk = Clamp01(wV * valgusRisk
                                + wA * asymmetryRisk
                                + wB * balanceRisk
                                + wF * flexionRisk);

        // ── GAME SCORE ───────────────────────────────────────────────────────────
        float gameScore = (1f - totalRisk) * 100f;
        // Consistency bonus (+5 pts) when ALL sub-scores are in the safe zone
        if (valgusRisk < 0.20f && balanceRisk < 0.20f && asymmetryRisk < 0.20f)
            gameScore = Math.Min(100f, gameScore + 5f);

        return new TaskResult
        {
            TaskType            = taskType,
            TaskNameTR          = taskNameTR,
            MeasuredDurationSec = measuredDurationSec,

            MeanValgusLeft  = meanValgusLeft,
            MeanValgusRight = meanValgusRight,
            MaxValgusLeft   = maxValgusLeft,
            MaxValgusRight  = maxValgusRight,

            MeanFlexLeft  = meanFlexLeft,
            MeanFlexRight = meanFlexRight,
            MaxFlexLeft   = maxFlexLeft,
            MaxFlexRight  = maxFlexRight,

            MeanSwayRMS       = meanSwayRMS,
            MeanSwayVelocity  = meanSwayVelocity,
            SymmetryIndex     = symmetryIndex,

            BilateralValgusAsymmetry = bilateralValgusAsym,

            ValgusRiskScore    = valgusRisk,
            AsymmetryRiskScore = asymmetryRisk,
            FlexionRiskScore   = flexionRisk,
            BalanceRiskScore   = balanceRisk,
            TotalRiskScore     = totalRisk,
            GameScore          = gameScore,
            RiskGrade          = GetRiskGrade(totalRisk)
        };
    }

    // ───────────────────────── Evidence-based helpers ─────────────────────────

    /// <summary>
    /// Task-type-specific knee flexion target (degrees).
    /// Below this value the flexion risk score rises to 1.0 (Hewett 2005).
    /// </summary>
    private static float GetFlexTarget(TaskType t)
    {
        switch (t)
        {
            case TaskType.MiniSquat:           return 60f;  // adequate squat depth
            case TaskType.LeanForward:         return 30f;  // anterior lean demands hip/knee flex
            case TaskType.SingleLegBalance_R:
            case TaskType.SingleLegBalance_L:  return 20f;  // slight bend for stability
            default:                           return 10f;  // minimal for static/lean tasks
        }
    }

    /// <summary>
    /// Task-type-specific risk weights (must sum to 1.0).
    /// Distribution reflects biomechanical demands from the literature.
    /// </summary>
    private static void GetTaskWeights(TaskType t,
        out float wValgus, out float wAsymmetry, out float wBalance, out float wFlexion)
    {
        switch (t)
        {
            case TaskType.Standing:
                // Static balance is primary metric; valgus less relevant (Maki 1990, Kaptein 2006)
                wValgus=0.20f; wAsymmetry=0.20f; wBalance=0.50f; wFlexion=0.10f; break;

            case TaskType.LeanRight:
            case TaskType.LeanLeft:
            case TaskType.LeanForward:
                // Lean tests dynamic balance + valgus response under load shift (Tamura 2017)
                wValgus=0.30f; wAsymmetry=0.25f; wBalance=0.35f; wFlexion=0.10f; break;

            case TaskType.SingleLegBalance_R:
            case TaskType.SingleLegBalance_L:
                // Single-leg: balance dominant; valgus secondary (Tamura 2017, Maki 1990)
                wValgus=0.30f; wAsymmetry=0.20f; wBalance=0.40f; wFlexion=0.10f; break;

            case TaskType.MiniSquat:
                // Squat: valgus + flexion clinically most important (Hewett 2005, Saki 2024)
                wValgus=0.40f; wAsymmetry=0.20f; wBalance=0.20f; wFlexion=0.20f; break;

            case TaskType.WalkSimulation:
                // Walk: symmetry + valgus; balance secondary (Saki 2024)
                wValgus=0.35f; wAsymmetry=0.30f; wBalance=0.25f; wFlexion=0.10f; break;

            default:
                wValgus=0.35f; wAsymmetry=0.25f; wBalance=0.25f; wFlexion=0.15f; break;
        }
    }

    private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

    /// <summary>Converts risk score [0,1] to a 4-zone Turkish label (aligned with RiskGrade zones).</summary>
    public static string RiskLabel(float risk)
    {
        if (risk < 0.25f) return "Düşük Risk";     // Yeşil zone
        if (risk < 0.50f) return "Orta Risk";      // Sarı zone
        if (risk < 0.75f) return "Yüksek Risk";    // Turuncu zone
        return "Kritik Risk";                        // Kırmızı zone
    }

    /// <summary>4-zone clinical grade string used in session reports and UI.</summary>
    public static string GetRiskGrade(float totalRisk)
    {
        if (totalRisk < 0.25f) return "Yeşil";     // Low — safe zone
        if (totalRisk < 0.50f) return "Sarı";      // Moderate — attention zone
        if (totalRisk < 0.75f) return "Turuncu";   // High — clinical concern
        return "Kırmızı";                           // Critical — immediate intervention
    }

    public override string ToString()
    {
        return $"[{TaskNameTR}] Skor:{GameScore:F1} Valgus:{MaxValgusLeft:F1}°L/{MaxValgusRight:F1}°R "
             + $"Sway:{MeanSwayRMS * 1000f:F1}mm SI:{SymmetryIndex:F1}%";
    }
}
