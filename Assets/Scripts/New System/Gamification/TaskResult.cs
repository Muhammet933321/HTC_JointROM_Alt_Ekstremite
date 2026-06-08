using System;

/// <summary>
/// Immutable data container for the outcome of a single gamification task.
/// All risk scores are in [0, 1]; game score is in [0, 100].
///
/// Evidence-informed movement-pattern scoring — key literature:
///   [Hewett 2005]  Peak knee abduction moment is an ACL risk marker; this system uses kinematic proxies, not KAM.
///   [Numata 2017]  2-D DKV cut-off ~10° supports a valgus warning zone in female athletes.
///   [Tamura 2017]  Mean (habitual) valgus reflects neuromuscular control quality.
///   [Saki 2024]    Bilateral activation asymmetry between limbs is an independent risk factor.
///   [Maki 1990]    COP velocity informs balance-risk rationale; current implementation uses pelvis sway proxy.
///   [Kaptein 2006] Sway velocity fraction reflects reactive balance-control rationale.
///   [Read 2019]    LESS / jump-landing screens are practical field tools; this is not a full LESS score.
///   [O'Connor 2020, Bennett 2022] Modified Y-Balance is supportive, but should not be used as a sole injury classifier.
/// </summary>
[Serializable]
public class TaskResult
{
    public const string InterpretationScopeTR =
        "Adolesan/genc sporcularda alt ekstremite hareket-risk paterni taramasi; tani veya tek basina yaralanma tahmini degildir.";

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
    public float MinFlexLeft;
    public float MinFlexRight;
    /// <summary>Range = Max − Min flexion for each side.</summary>
    public float RangeFlexLeft;
    public float RangeFlexRight;
    /// <summary>Population standard deviation of flexion samples.</summary>
    public float StdFlexLeft;
    public float StdFlexRight;

    // ───────────────────────── Sway ─────────────────────────

    public float MeanSwayRMS;
    public float MeanSwayVelocity;

    // ───────────────────────── Threshold Durations ─────────────────────────

    /// <summary>Time left valgus exceeded 8° during the task (seconds).</summary>
    public float ValgusLAboveThreshSec;
    /// <summary>Time right valgus exceeded 8° during the task (seconds).</summary>
    public float ValgusRAboveThreshSec;
    /// <summary>Percentage of task frames where symmetry index exceeded 10%.</summary>
    public float AsymmetryAbovePct;

    // ───────────────────────── Angular Velocity ─────────────────────────

    /// <summary>Mean valgus angular velocity left (degrees/second).</summary>
    public float MeanValgusAngularVelLeft;
    /// <summary>Mean valgus angular velocity right (degrees/second).</summary>
    public float MeanValgusAngularVelRight;

    // ───────────────────────── Jerk (Q87) ─────────────────────────

    /// <summary>Population std-dev of valgus jerk samples left (degrees/second³) — proxy for neuromuscular variability.</summary>
    public float JerkRmsLeft;
    /// <summary>Population std-dev of valgus jerk samples right (degrees/second³).</summary>
    public float JerkRmsRight;

    // ───────────────────────── Data Quality (Q34) ─────────────────────────

    /// <summary>Percentage of collected frames with valid tracker data (100% = no missing frames).</summary>
    public float DataQualityPct;

    // ───────────────────────── Posture Recommendation (Q110) ─────────────────────────

    /// <summary>Turkish prescriptive recommendation based on dominant risk factor.</summary>
    public string PostureRecommendationTR;

    // ───────────────────────── Symmetry ─────────────────────────

    public float SymmetryIndex;

    /// <summary>Absolute degree difference between left and right peak valgus (Saki 2024).</summary>
    public float BilateralValgusAsymmetry;

    /// <summary>Modified Y-Balance anterior reach while standing on the left leg (% approximate leg length).</summary>
    public float MaxLeftStanceAnteriorReachPct;

    /// <summary>Modified Y-Balance anterior reach while standing on the right leg (% approximate leg length).</summary>
    public float MaxRightStanceAnteriorReachPct;

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
    /// Squat target: 60°; landing target: ~45°; single-leg stance tasks: 20°; Standing: 10°.
    /// </summary>
    public float FlexionRiskScore;

    /// <summary>
    /// Balance risk — combined COP velocity (60%) + RMS (40%).
    /// Velocity: safe &lt;20 mm/s, full risk at 50 mm/s (Maki 1990).
    /// RMS: normalised to swayRmsThreshold from TaskDefinition (Kaptein 2006).
    /// </summary>
    public float BalanceRiskScore;

    /// <summary>
    /// Modified anterior-reach risk — supportive, not standalone.
    /// Reach at/above target = safe; full risk 15% below target.
    /// </summary>
    public float ReachRiskScore;

    /// <summary>
    /// Weighted total movement-pattern risk. Weights are task-type-specific and evidence-informed.
    /// </summary>
    public float TotalRiskScore;

    /// <summary>
    /// Game-facing score 0–100. Includes small consistency bonus when all sub-risks are low.
    /// </summary>
    public float GameScore;

    /// <summary>4-zone screening grade: Yeşil / Sarı / Turuncu / Kırmızı.</summary>
    public string RiskGrade;

    /// <summary>Short Turkish interpretation string used in UI/report summaries.</summary>
    public string TaskSummaryTR;

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
        float maxLeftStanceAnteriorReachPct = 0f,
        float maxRightStanceAnteriorReachPct = 0f,
        float swayThreshold = 0.015f,
        float targetReachPct = 65f,
        float landingFlexionTargetDeg = 45f,
        float minFlexLeft  = 0f,
        float minFlexRight = 0f,
        float stdFlexLeft  = 0f,
        float stdFlexRight = 0f,
        float valgusLAboveThreshSec = 0f,
        float valgusRAboveThreshSec = 0f,
        float asymmetryAbovePct = 0f,
        float meanValgusAngularVelLeft  = 0f,
        float meanValgusAngularVelRight = 0f,
        float jerkRmsLeft  = 0f,
        float jerkRmsRight = 0f,
        float dataQualityPct = 100f)
    {
        float meanValgusSource = GetTaskSideValue(taskType, meanValgusLeft, meanValgusRight);
        float peakValgusSource = GetTaskSideValue(taskType, maxValgusLeft, maxValgusRight);

        // ── VALGUS RISK ──────────────────────────────────────────────────────────
        float meanValgusRisk = Clamp01((meanValgusSource - 5f) / 13f);
        float peakValgusRisk = Clamp01((peakValgusSource - 8f) / 10f);
        float combinedValgusRisk = 0.35f * meanValgusRisk + 0.65f * peakValgusRisk;

        float bilateralValgusAsym = Math.Abs(maxValgusLeft - maxValgusRight);
        float bilateralValgusRisk = Clamp01(bilateralValgusAsym / 8f);
        float valgusRisk = Clamp01(0.80f * combinedValgusRisk + 0.20f * bilateralValgusRisk);

        // ── ASYMMETRY RISK ───────────────────────────────────────────────────────
        float asymmetryRisk = IsSingleLegTask(taskType) || HasReachMetric(taskType)
            ? 0f
            : Clamp01((symmetryIndex - 10f) / 15f);

        // ── FLEXION RISK ─────────────────────────────────────────────────────────
        float flexTarget = GetFlexTarget(taskType, landingFlexionTargetDeg);
        float maxFlex = GetTaskSideValue(taskType, maxFlexLeft, maxFlexRight);
        float flexionRisk = (maxFlex >= flexTarget || flexTarget <= 0f)
            ? 0f
            : Clamp01(1f - maxFlex / flexTarget);

        // ── BALANCE RISK ─────────────────────────────────────────────────────────
        float swayVelRisk = Clamp01((meanSwayVelocity - 0.020f) / 0.030f);
        float swayRmsRisk = swayThreshold > 0f ? Clamp01(meanSwayRMS / swayThreshold) : 0f;
        float balanceRisk = Clamp01(0.60f * swayVelRisk + 0.40f * swayRmsRisk);

        // ── REACH RISK ───────────────────────────────────────────────────────────
        float reachPct = GetReachValue(taskType, maxLeftStanceAnteriorReachPct, maxRightStanceAnteriorReachPct);
        float reachRisk = HasReachMetric(taskType)
            ? Clamp01((targetReachPct - reachPct) / 15f)
            : 0f;

        // ── TASK-TYPE WEIGHTS ────────────────────────────────────────────────────
        GetTaskWeights(taskType, out float wV, out float wA, out float wB, out float wF, out float wR);

        float totalRisk = Clamp01(wV * valgusRisk
                                + wA * asymmetryRisk
                                + wB * balanceRisk
                                + wF * flexionRisk
                                + wR * reachRisk);

        // ── GAME SCORE ───────────────────────────────────────────────────────────
        float gameScore = (1f - totalRisk) * 100f;
        if (valgusRisk < 0.20f && balanceRisk < 0.20f && asymmetryRisk < 0.20f
            && flexionRisk < 0.20f && reachRisk < 0.20f)
            gameScore = Math.Min(100f, gameScore + 5f);

        string taskSummary = BuildTaskSummaryTR(taskType, valgusRisk, balanceRisk, flexionRisk, reachRisk, asymmetryRisk);

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
            MinFlexLeft   = minFlexLeft,
            MinFlexRight  = minFlexRight,
            RangeFlexLeft  = maxFlexLeft  - minFlexLeft,
            RangeFlexRight = maxFlexRight - minFlexRight,
            StdFlexLeft   = stdFlexLeft,
            StdFlexRight  = stdFlexRight,

            MeanSwayRMS       = meanSwayRMS,
            MeanSwayVelocity  = meanSwayVelocity,
            SymmetryIndex     = symmetryIndex,

            ValgusLAboveThreshSec = valgusLAboveThreshSec,
            ValgusRAboveThreshSec = valgusRAboveThreshSec,
            AsymmetryAbovePct     = asymmetryAbovePct,
            MeanValgusAngularVelLeft  = meanValgusAngularVelLeft,
            MeanValgusAngularVelRight = meanValgusAngularVelRight,
            JerkRmsLeft   = jerkRmsLeft,
            JerkRmsRight  = jerkRmsRight,
            DataQualityPct = dataQualityPct,
            PostureRecommendationTR = BuildPostureRecommendationTR(valgusRisk, balanceRisk, flexionRisk, asymmetryRisk),

            BilateralValgusAsymmetry = bilateralValgusAsym,
            MaxLeftStanceAnteriorReachPct  = maxLeftStanceAnteriorReachPct,
            MaxRightStanceAnteriorReachPct = maxRightStanceAnteriorReachPct,

            ValgusRiskScore    = valgusRisk,
            AsymmetryRiskScore = asymmetryRisk,
            FlexionRiskScore   = flexionRisk,
            BalanceRiskScore   = balanceRisk,
            ReachRiskScore     = reachRisk,
            TotalRiskScore     = totalRisk,
            GameScore          = gameScore,
            RiskGrade          = GetRiskGrade(totalRisk),
            TaskSummaryTR      = taskSummary
        };
    }

    // ───────────────────────── Evidence-based helpers ─────────────────────────

    private static float GetFlexTarget(TaskType t, float landingFlexionTargetDeg)
    {
        switch (t)
        {
            case TaskType.MiniSquat:           return 60f;
            case TaskType.LeanForward:         return 30f;
            case TaskType.SingleLegBalance_R:
            case TaskType.SingleLegBalance_L:  return 20f;
            case TaskType.LandingScreen:       return landingFlexionTargetDeg > 0f ? landingFlexionTargetDeg : 45f;
            case TaskType.SingleLegSquat_R:
            case TaskType.SingleLegSquat_L:    return 40f;
            case TaskType.ModifiedYBalanceAnterior_R:
            case TaskType.ModifiedYBalanceAnterior_L:
                                             return 20f;
            default:                           return 10f;
        }
    }

    private static void GetTaskWeights(TaskType t,
        out float wValgus, out float wAsymmetry, out float wBalance, out float wFlexion, out float wReach)
    {
        switch (t)
        {
            case TaskType.Standing:
                wValgus=0.20f; wAsymmetry=0.20f; wBalance=0.50f; wFlexion=0.10f; wReach=0.00f; break;

            case TaskType.LeanRight:
            case TaskType.LeanLeft:
            case TaskType.LeanForward:
                wValgus=0.30f; wAsymmetry=0.25f; wBalance=0.35f; wFlexion=0.10f; wReach=0.00f; break;

            case TaskType.SingleLegBalance_R:
            case TaskType.SingleLegBalance_L:
                wValgus=0.30f; wAsymmetry=0.00f; wBalance=0.55f; wFlexion=0.15f; wReach=0.00f; break;

            case TaskType.MiniSquat:
                wValgus=0.40f; wAsymmetry=0.15f; wBalance=0.20f; wFlexion=0.25f; wReach=0.00f; break;

            case TaskType.WalkSimulation:
                wValgus=0.35f; wAsymmetry=0.25f; wBalance=0.30f; wFlexion=0.10f; wReach=0.00f; break;

            case TaskType.LandingScreen:
                wValgus=0.40f; wAsymmetry=0.05f; wBalance=0.20f; wFlexion=0.35f; wReach=0.00f; break;

            case TaskType.ModifiedYBalanceAnterior_R:
            case TaskType.ModifiedYBalanceAnterior_L:
                wValgus=0.20f; wAsymmetry=0.00f; wBalance=0.30f; wFlexion=0.10f; wReach=0.40f; break;

            case TaskType.SingleLegSquat_R:
            case TaskType.SingleLegSquat_L:
                wValgus=0.40f; wAsymmetry=0.00f; wBalance=0.25f; wFlexion=0.35f; wReach=0.00f; break;

            default:
                wValgus=0.35f; wAsymmetry=0.20f; wBalance=0.25f; wFlexion=0.20f; wReach=0.00f; break;
        }
    }

    public static bool HasReachMetric(TaskType t)
    {
        return t == TaskType.ModifiedYBalanceAnterior_R || t == TaskType.ModifiedYBalanceAnterior_L;
    }

    private static bool IsSingleLegTask(TaskType t)
    {
        switch (t)
        {
            case TaskType.SingleLegBalance_R:
            case TaskType.SingleLegBalance_L:
            case TaskType.ModifiedYBalanceAnterior_R:
            case TaskType.ModifiedYBalanceAnterior_L:
            case TaskType.SingleLegSquat_R:
            case TaskType.SingleLegSquat_L:
                return true;
            default:
                return false;
        }
    }

    private static float GetTaskSideValue(TaskType t, float left, float right)
    {
        switch (t)
        {
            case TaskType.SingleLegBalance_R:
            case TaskType.ModifiedYBalanceAnterior_R:
            case TaskType.SingleLegSquat_R:
                return right;

            case TaskType.SingleLegBalance_L:
            case TaskType.ModifiedYBalanceAnterior_L:
            case TaskType.SingleLegSquat_L:
                return left;

            default:
                return Math.Max(left, right);
        }
    }

    private static float GetReachValue(TaskType t, float leftStanceReachPct, float rightStanceReachPct)
    {
        switch (t)
        {
            case TaskType.ModifiedYBalanceAnterior_R: return rightStanceReachPct;
            case TaskType.ModifiedYBalanceAnterior_L: return leftStanceReachPct;
            default: return 0f;
        }
    }

    private static string BuildTaskSummaryTR(
        TaskType taskType,
        float valgusRisk,
        float balanceRisk,
        float flexionRisk,
        float reachRisk,
        float asymmetryRisk)
    {
        switch (taskType)
        {
            case TaskType.LandingScreen:
                if (valgusRisk >= 0.60f && flexionRisk >= 0.50f)
                    return "İnişte diz içe kaçışı ve sert iniş paterni birlikte görüldü.";
                if (valgusRisk >= 0.60f)
                    return "İniş sırasında diz içe kaçışı belirgin.";
                if (flexionRisk >= 0.50f)
                    return "İniş sert; diz ve kalça bükülmesi yetersiz.";
                if (balanceRisk >= 0.50f)
                    return "İnişten sonra denge toparlanması yavaş.";
                return "İniş kontrolü kabul edilebilir görünüyor.";

            case TaskType.ModifiedYBalanceAnterior_R:
            case TaskType.ModifiedYBalanceAnterior_L:
                if (reachRisk >= 0.60f && balanceRisk >= 0.50f)
                    return "Uzanma mesafesi kısa ve stance denge kontrolü zayıf.";
                if (reachRisk >= 0.60f)
                    return "Anterior reach kapasitesi beklenenin altında.";
                if (valgusRisk >= 0.60f)
                    return "Reach sırasında stance dizinde medial çökme izlendi.";
                if (balanceRisk >= 0.50f)
                    return "Reach sırasında salınım arttı; dinamik denge zorlandı.";
                return "Reach ve stance kontrolü kabul edilebilir.";

            case TaskType.SingleLegSquat_R:
            case TaskType.SingleLegSquat_L:
                if (valgusRisk >= 0.60f && flexionRisk >= 0.50f)
                    return "Tek ayak squat sırasında kontrollü yük kabulü zayıf; diz içe kaçıyor.";
                if (valgusRisk >= 0.60f)
                    return "Tek ayak squat sırasında stance dizinde içe kaçış izlendi.";
                if (flexionRisk >= 0.50f)
                    return "Squat derinliği yetersiz; güvenli yük kabulü sınırlı.";
                if (balanceRisk >= 0.50f)
                    return "Tek ayak squat sırasında denge kontrolü zayıf.";
                return "Tek ayak squat kontrolü kabul edilebilir.";

            default:
                if (valgusRisk >= 0.60f)
                    return "Valgus kontrolü öncelikli takip gerektiriyor.";
                if (balanceRisk >= 0.60f)
                    return "Denge kontrolü öncelikli takip gerektiriyor.";
                if (asymmetryRisk >= 0.60f)
                    return "Sağ-sol asimetri belirgin.";
                if (flexionRisk >= 0.60f)
                    return "Fleksiyon stratejisi yetersiz görünüyor.";
                return "Belirgin bir yüksek-risk paterni görülmedi.";
        }
    }

    private static string BuildPostureRecommendationTR(
        float valgusRisk, float balanceRisk, float flexionRisk, float asymmetryRisk)
    {
        if (valgusRisk >= 0.50f)
            return "Kalça abduktör ve kuadriseps güçlendirme egzersizleri önerilebilir.";
        if (balanceRisk >= 0.50f)
            return "Propriyosepsiyon ve dinamik denge egzersizleri önerilebilir.";
        if (flexionRisk >= 0.50f)
            return "Diz ve kalça fleksiyon açıklığını artıran mobilite çalışmaları önerilebilir.";
        if (asymmetryRisk >= 0.50f)
            return "Sağ-sol asimetriyi azaltmaya yönelik tek taraflı güçlendirme önerilebilir.";
        return "Genel hareket kalitesi kabul edilebilir; koruyucu egzersizlere devam edilmesi önerilir.";
    }

    private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

    public static string RiskLabel(float risk)
    {
        if (risk < 0.25f) return "Düşük Uyarı";
        if (risk < 0.50f) return "Orta Uyarı";
        if (risk < 0.75f) return "Yüksek Uyarı";
        return "Kritik Uyarı";
    }

    public static string GetRiskGrade(float totalRisk)
    {
        if (totalRisk < 0.25f) return "Yeşil";
        if (totalRisk < 0.50f) return "Sarı";
        if (totalRisk < 0.75f) return "Turuncu";
        return "Kırmızı";
    }

    public override string ToString()
    {
        string reachText = HasReachMetric(TaskType)
            ? $" Reach:{GetReachValue(TaskType, MaxLeftStanceAnteriorReachPct, MaxRightStanceAnteriorReachPct):F1}%"
            : "";

        return $"[{TaskNameTR}] Skor:{GameScore:F1} Valgus:{MaxValgusLeft:F1}°L/{MaxValgusRight:F1}°R "
             + $"Sway:{MeanSwayRMS * 1000f:F1}mm SI:{SymmetryIndex:F1}%{reachText}";
    }
}
