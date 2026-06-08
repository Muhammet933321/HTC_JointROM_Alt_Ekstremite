using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes session data to CSV and JSON files at the end of each session.
/// Files are saved to Application.persistentDataPath/Reports/.
///
/// Subscribe to TaskSequencer.OnSessionCompleted or call WriteReport() manually.
/// </summary>
public class SessionReportWriter : MonoBehaviour
{
    // ───────────────────────── Inspector ─────────────────────────

    [Header("=== Dependencies ===")]
    public TaskSequencer sequencer;

    [Header("=== Settings ===")]
    [Tooltip("Sub-folder name inside Application.persistentDataPath.")]
    [SerializeField] private string reportFolder = "Reports";

    [Tooltip("Also write a JSON copy of the report.")]
    [SerializeField] private bool writeJson = true;

    [Tooltip("Log the report folder path to Console after writing.")]
    [SerializeField] private bool logPathOnWrite = true;

    // ───────────────────────── Public State ─────────────────────────

    /// <summary>Full path of the most recently written CSV file.</summary>
    public string LastCsvPath { get; private set; }

    /// <summary>Full path of the most recently written JSON file.</summary>
    public string LastJsonPath { get; private set; }

    // ───────────────────────── Unity ─────────────────────────

    private void OnEnable()
    {
        if (sequencer != null)
            sequencer.OnSessionCompleted += HandleSessionCompleted;
    }

    private void OnDisable()
    {
        if (sequencer != null)
            sequencer.OnSessionCompleted -= HandleSessionCompleted;
    }

    // ───────────────────────── Handlers ─────────────────────────

    private void HandleSessionCompleted(List<TaskResult> results)
    {
        WriteReport(results);
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Manually write a report for the provided results list.</summary>
    public void WriteReport(List<TaskResult> results)
    {
        if (results == null || results.Count == 0)
        {
            Debug.LogWarning("[SessionReportWriter] No results to write.");
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(Application.persistentDataPath, reportFolder);
        Directory.CreateDirectory(folder);

        try
        {
            WriteCsv(results, folder, timestamp);
            if (writeJson) WriteJson(results, folder, timestamp);

            // Backup kopyası — otomatik yedekleme (Q157)
            string backupDir = Path.Combine(folder, "Backup");
            Directory.CreateDirectory(backupDir);
            if (!string.IsNullOrEmpty(LastCsvPath) && File.Exists(LastCsvPath))
                File.Copy(LastCsvPath, Path.Combine(backupDir, Path.GetFileName(LastCsvPath)), overwrite: true);
            if (!string.IsNullOrEmpty(LastJsonPath) && File.Exists(LastJsonPath))
                File.Copy(LastJsonPath, Path.Combine(backupDir, Path.GetFileName(LastJsonPath)), overwrite: true);

            if (logPathOnWrite)
                Debug.Log($"[SessionReportWriter] Report saved to: {folder}");
        }
        catch (Exception e)
        {
            // Kısmi CSV yedekleme — dosya sistemi hatalarında veri kaybını önler (Q25)
            string partialPath = Path.Combine(folder, $"session_{timestamp}.partial.csv");
            try { File.WriteAllText(partialPath, _lastCsvContent, System.Text.Encoding.UTF8); }
            catch { /* partial da başarısız olursa sessiz geç */ }
            Debug.LogError($"[SessionReportWriter] Kayıt hatası — kısmi dosya: {partialPath}\n{e}");
        }
    }

    private string _lastCsvContent = "";

    // ───────────────────────── CSV ─────────────────────────

    private void WriteCsv(List<TaskResult> results, string folder, string timestamp)
    {
        string path = Path.Combine(folder, $"session_{timestamp}.csv");
        LastCsvPath = path;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(
            "TaskType,TaskName,Duration_s," +
            "MeanValgusL_deg,MeanValgusR_deg,MaxValgusL_deg,MaxValgusR_deg," +
            "MeanFlexL_deg,MeanFlexR_deg,MaxFlexL_deg,MaxFlexR_deg," +
            "MinFlexL_deg,MinFlexR_deg,RangeFlexL_deg,RangeFlexR_deg," +
            "StdFlexL_deg,StdFlexR_deg," +
            "MeanSwayRMS_mm,MeanSwayVelocity_mps," +
            "SymmetryIndex_pct,MaxLeftStanceAnteriorReach_pct,MaxRightStanceAnteriorReach_pct," +
            "ValgusLAboveThresh_s,ValgusRAboveThresh_s,AsymmetryAbove_pct," +
            "MeanValgusAngVelL_dps,MeanValgusAngVelR_dps," +
            "JerkRmsL_dps3,JerkRmsR_dps3,DataQuality_pct," +
            "ValgusRisk,AsymmetryRisk,FlexionRisk,BalanceRisk,ReachRisk,TotalRisk," +
            "GameScore,PostureRecommendationTR,TaskSummaryTR");

        const int ColCount = 39;

        foreach (var r in results)
        {
            sb.AppendLine(string.Join(",",
                r.TaskType,
                EscapeCsv(r.TaskNameTR),
                F(r.MeasuredDurationSec),
                F(r.MeanValgusLeft),   F(r.MeanValgusRight),
                F(r.MaxValgusLeft),    F(r.MaxValgusRight),
                F(r.MeanFlexLeft),     F(r.MeanFlexRight),
                F(r.MaxFlexLeft),      F(r.MaxFlexRight),
                F(r.MinFlexLeft),      F(r.MinFlexRight),
                F(r.RangeFlexLeft),    F(r.RangeFlexRight),
                F(r.StdFlexLeft),      F(r.StdFlexRight),
                F(r.MeanSwayRMS * 1000f),  // convert m → mm
                F(r.MeanSwayVelocity),
                F(r.SymmetryIndex),
                F(r.MaxLeftStanceAnteriorReachPct),
                F(r.MaxRightStanceAnteriorReachPct),
                F(r.ValgusLAboveThreshSec), F(r.ValgusRAboveThreshSec),
                F(r.AsymmetryAbovePct),
                F(r.MeanValgusAngularVelLeft), F(r.MeanValgusAngularVelRight),
                F(r.JerkRmsLeft),  F(r.JerkRmsRight),
                F(r.DataQualityPct),
                F(r.ValgusRiskScore),  F(r.AsymmetryRiskScore),
                F(r.FlexionRiskScore), F(r.BalanceRiskScore),
                F(r.ReachRiskScore),
                F(r.TotalRiskScore),
                F(r.GameScore),
                EscapeCsv(r.PostureRecommendationTR),
                EscapeCsv(r.TaskSummaryTR)));
        }

        // Summary row
        float sessionAvg = 0f;
        foreach (var r in results) sessionAvg += r.GameScore;
        sessionAvg /= results.Count;
        sb.AppendLine();
        string[] summaryRow = new string[ColCount];
        summaryRow[0]        = "Session Average";
        summaryRow[ColCount - 2] = F(sessionAvg);
        sb.AppendLine(string.Join(",", summaryRow));

        _lastCsvContent = sb.ToString();
        File.WriteAllText(path, _lastCsvContent, Encoding.UTF8);
    }

    // ───────────────────────── JSON ─────────────────────────

    private void WriteJson(List<TaskResult> results, string folder, string timestamp)
    {
        string path = Path.Combine(folder, $"session_{timestamp}.json");
        LastJsonPath = path;

        // Manual JSON serialization (avoids JsonUtility limitation with List<>)
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"sessionTimestamp\": \"{timestamp}\",");
        sb.AppendLine($"  \"interpretationScopeTR\": \"{EscapeJson(TaskResult.InterpretationScopeTR)}\",");
        sb.AppendLine($"  \"taskCount\": {results.Count},");
        sb.AppendLine("  \"tasks\": [");

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            bool last = i == results.Count - 1;

            sb.AppendLine("    {");
            sb.AppendLine($"      \"taskType\": \"{r.TaskType}\",");
            sb.AppendLine($"      \"taskNameTR\": \"{EscapeJson(r.TaskNameTR)}\",");
            sb.AppendLine($"      \"durationSec\": {F(r.MeasuredDurationSec)},");
            sb.AppendLine($"      \"valgus\": {{ \"meanL\": {F(r.MeanValgusLeft)}, \"meanR\": {F(r.MeanValgusRight)}, \"maxL\": {F(r.MaxValgusLeft)}, \"maxR\": {F(r.MaxValgusRight)} }},");
            sb.AppendLine($"      \"flexion\": {{ \"meanL\": {F(r.MeanFlexLeft)}, \"meanR\": {F(r.MeanFlexRight)}, \"maxL\": {F(r.MaxFlexLeft)}, \"maxR\": {F(r.MaxFlexRight)}, \"minL\": {F(r.MinFlexLeft)}, \"minR\": {F(r.MinFlexRight)}, \"rangeL\": {F(r.RangeFlexLeft)}, \"rangeR\": {F(r.RangeFlexRight)}, \"stdL\": {F(r.StdFlexLeft)}, \"stdR\": {F(r.StdFlexRight)} }},");
            sb.AppendLine($"      \"swayRMS_mm\": {F(r.MeanSwayRMS * 1000f)},");
            sb.AppendLine($"      \"swayVelocity_mps\": {F(r.MeanSwayVelocity)},");
            sb.AppendLine($"      \"symmetryIndex\": {F(r.SymmetryIndex)},");
            sb.AppendLine($"      \"reach\": {{ \"leftStancePct\": {F(r.MaxLeftStanceAnteriorReachPct)}, \"rightStancePct\": {F(r.MaxRightStanceAnteriorReachPct)} }},");
            sb.AppendLine($"      \"valgusAboveThresh_s\": {{ \"left\": {F(r.ValgusLAboveThreshSec)}, \"right\": {F(r.ValgusRAboveThreshSec)} }},");
            sb.AppendLine($"      \"asymmetryAbove_pct\": {F(r.AsymmetryAbovePct)},");
            sb.AppendLine($"      \"valgusAngularVel_dps\": {{ \"meanLeft\": {F(r.MeanValgusAngularVelLeft)}, \"meanRight\": {F(r.MeanValgusAngularVelRight)} }},");
            sb.AppendLine($"      \"jerkRms_dps3\": {{ \"left\": {F(r.JerkRmsLeft)}, \"right\": {F(r.JerkRmsRight)} }},");
            sb.AppendLine($"      \"dataQuality_pct\": {F(r.DataQualityPct)},");
            sb.AppendLine($"      \"risks\": {{ \"valgus\": {F(r.ValgusRiskScore)}, \"asymmetry\": {F(r.AsymmetryRiskScore)}, \"flexion\": {F(r.FlexionRiskScore)}, \"balance\": {F(r.BalanceRiskScore)}, \"reach\": {F(r.ReachRiskScore)}, \"total\": {F(r.TotalRiskScore)} }},");
            sb.AppendLine($"      \"gameScore\": {F(r.GameScore)},");
            sb.AppendLine($"      \"postureRecommendationTR\": \"{EscapeJson(r.PostureRecommendationTR)}\",");
            sb.AppendLine($"      \"taskSummaryTR\": \"{EscapeJson(r.TaskSummaryTR)}\"");
            sb.AppendLine(last ? "    }" : "    },");
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static string F(float v) => v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

    private static string EscapeCsv(string s)
    {
        if (s == null) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
