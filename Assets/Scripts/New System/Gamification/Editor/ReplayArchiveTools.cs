#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class ReplayArchiveEntry
{
    public string Key;
    public string TimestampRaw;
    public DateTime Timestamp;
    public bool HasTimestamp;
    public string GameName;
    public string SourceLabel;

    public string ReplayFolderPath;
    public string ManifestPath;
    public string FramesPath;
    public string EventsPath;
    public string SessionJsonPath;
    public string SessionCsvPath;
    public string DiagnosticsCsvPath;

    public ReplayManifest Manifest;
    public SessionReportJson SessionReport;
    public DiagnosticsSummary Diagnostics;
    public FrameAnalysisSummary FrameAnalysis;
    public bool EventsLoaded;
    public readonly List<ReplayEvent> Events = new();
    public readonly List<ArchiveTaskResult> TaskResults = new();

    public bool HasReplay => !string.IsNullOrEmpty(ReplayFolderPath) && Directory.Exists(ReplayFolderPath);
    public bool HasManifest => !string.IsNullOrEmpty(ManifestPath) && File.Exists(ManifestPath);
    public bool HasFrames => !string.IsNullOrEmpty(FramesPath) && File.Exists(FramesPath);
    public bool HasEvents => !string.IsNullOrEmpty(EventsPath) && File.Exists(EventsPath);
    public bool HasSessionReport => (!string.IsNullOrEmpty(SessionJsonPath) && File.Exists(SessionJsonPath)) || (!string.IsNullOrEmpty(SessionCsvPath) && File.Exists(SessionCsvPath));
    public bool HasDiagnostics => !string.IsNullOrEmpty(DiagnosticsCsvPath) && File.Exists(DiagnosticsCsvPath);

    public int TaskCount
    {
        get
        {
            if (TaskResults.Count > 0) return TaskResults.Count;
            if (Manifest != null && Manifest.taskCount > 0) return Manifest.taskCount;
            if (SessionReport != null && SessionReport.taskCount > 0) return SessionReport.taskCount;
            return 0;
        }
    }

    public float DurationSeconds => Manifest != null ? Manifest.durationSeconds : SumTaskDurations();
    public int FrameCount => Manifest != null ? Manifest.frameCount : FrameAnalysis != null ? FrameAnalysis.frameCount : 0;
    public int EventCount => Manifest != null ? Manifest.eventCount : Events.Count;

    public float AverageGameScore
    {
        get
        {
            if (TaskResults.Count == 0) return 0f;
            float total = 0f;
            for (int index = 0; index < TaskResults.Count; index++)
                total += TaskResults[index].gameScore;
            return total / TaskResults.Count;
        }
    }

    public string TimestampDisplay => HasTimestamp ? Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : TimestampRaw;
    public string DisplayName => $"{TimestampDisplay} | {GameName}";

    private float SumTaskDurations()
    {
        float total = 0f;
        for (int index = 0; index < TaskResults.Count; index++)
            total += TaskResults[index].durationSec;
        return total;
    }
}

[Serializable]
internal sealed class SessionReportJson
{
    public string sessionTimestamp;
    public string interpretationScopeTR;
    public int taskCount;
    public List<SessionReportTaskJson> tasks = new();
}

[Serializable]
internal sealed class SessionReportTaskJson
{
    public string taskType;
    public string taskNameTR;
    public float durationSec;
    public SessionPairJson valgus;
    public SessionPairJson flexion;
    public float swayRMS_mm;
    public float swayVelocity_mps;
    public float symmetryIndex;
    public SessionReachJson reach;
    public SessionRisksJson risks;
    public float gameScore;
    public string taskSummaryTR;
}

[Serializable]
internal sealed class SessionPairJson
{
    public float meanL;
    public float meanR;
    public float maxL;
    public float maxR;
}

[Serializable]
internal sealed class SessionReachJson
{
    public float leftStancePct;
    public float rightStancePct;
}

[Serializable]
internal sealed class SessionRisksJson
{
    public float valgus;
    public float asymmetry;
    public float flexion;
    public float balance;
    public float reach;
    public float total;
}

internal sealed class ArchiveTaskResult
{
    public int taskIndex;
    public string taskType;
    public string taskNameTR;
    public float durationSec;
    public float meanValgusLeft;
    public float meanValgusRight;
    public float maxValgusLeft;
    public float maxValgusRight;
    public float meanFlexLeft;
    public float meanFlexRight;
    public float maxFlexLeft;
    public float maxFlexRight;
    public float swayRmsMm;
    public float swayVelocityMps;
    public float symmetryIndexPct;
    public float leftReachPct;
    public float rightReachPct;
    public float valgusRisk;
    public float asymmetryRisk;
    public float flexionRisk;
    public float balanceRisk;
    public float reachRisk;
    public float totalRisk;
    public float gameScore;
    public string riskGrade;
    public string taskSummaryTR;

    public static ArchiveTaskResult FromSession(SessionReportTaskJson task, int index)
    {
        var result = new ArchiveTaskResult { taskIndex = index };
        if (task == null) return result;

        result.taskType = task.taskType;
        result.taskNameTR = task.taskNameTR;
        result.durationSec = task.durationSec;
        if (task.valgus != null)
        {
            result.meanValgusLeft = task.valgus.meanL;
            result.meanValgusRight = task.valgus.meanR;
            result.maxValgusLeft = task.valgus.maxL;
            result.maxValgusRight = task.valgus.maxR;
        }
        if (task.flexion != null)
        {
            result.meanFlexLeft = task.flexion.meanL;
            result.meanFlexRight = task.flexion.meanR;
            result.maxFlexLeft = task.flexion.maxL;
            result.maxFlexRight = task.flexion.maxR;
        }
        result.swayRmsMm = task.swayRMS_mm;
        result.swayVelocityMps = task.swayVelocity_mps;
        result.symmetryIndexPct = task.symmetryIndex;
        if (task.reach != null)
        {
            result.leftReachPct = task.reach.leftStancePct;
            result.rightReachPct = task.reach.rightStancePct;
        }
        if (task.risks != null)
        {
            result.valgusRisk = task.risks.valgus;
            result.asymmetryRisk = task.risks.asymmetry;
            result.flexionRisk = task.risks.flexion;
            result.balanceRisk = task.risks.balance;
            result.reachRisk = task.risks.reach;
            result.totalRisk = task.risks.total;
        }
        result.gameScore = task.gameScore;
        result.riskGrade = TaskResult.GetRiskGrade(result.totalRisk);
        result.taskSummaryTR = task.taskSummaryTR;
        return result;
    }

    public static ArchiveTaskResult FromReplayResult(ReplayTaskResultSnapshot result, int index)
    {
        var task = new ArchiveTaskResult { taskIndex = index };
        if (result == null) return task;

        task.taskType = result.taskType;
        task.taskNameTR = result.taskNameTR;
        task.durationSec = result.measuredDurationSec;
        task.meanValgusLeft = result.meanValgusLeft;
        task.meanValgusRight = result.meanValgusRight;
        task.maxValgusLeft = result.maxValgusLeft;
        task.maxValgusRight = result.maxValgusRight;
        task.meanFlexLeft = result.meanFlexLeft;
        task.meanFlexRight = result.meanFlexRight;
        task.maxFlexLeft = result.maxFlexLeft;
        task.maxFlexRight = result.maxFlexRight;
        task.swayRmsMm = result.meanSwayRmsMeters * 1000f;
        task.swayVelocityMps = result.meanSwayVelocityMps;
        task.symmetryIndexPct = result.symmetryIndexPct;
        task.leftReachPct = result.maxLeftStanceAnteriorReachPct;
        task.rightReachPct = result.maxRightStanceAnteriorReachPct;
        task.valgusRisk = result.valgusRiskScore;
        task.asymmetryRisk = result.asymmetryRiskScore;
        task.flexionRisk = result.flexionRiskScore;
        task.balanceRisk = result.balanceRiskScore;
        task.reachRisk = result.reachRiskScore;
        task.totalRisk = result.totalRiskScore;
        task.gameScore = result.gameScore;
        task.riskGrade = !string.IsNullOrEmpty(result.riskGrade) ? result.riskGrade : TaskResult.GetRiskGrade(result.totalRiskScore);
        task.taskSummaryTR = result.taskSummaryTR;
        return task;
    }
}

internal sealed class DiagnosticsSummary
{
    public int sampleCount;
    public int markerCount;
    public int calibratorReadyCount;
    public int ikReadyCount;
    public int trackingReadyCount;
    public int mappingReadyCount;
    public int simulatedInputCount;
    public int leftFallbackCount;
    public int rightFallbackCount;
    public readonly FloatStats leftValgus = new();
    public readonly FloatStats rightValgus = new();
    public readonly FloatStats leftFlexion = new();
    public readonly FloatStats rightFlexion = new();
    public readonly FloatStats swayRmsMm = new();
    public readonly FloatStats swayVelocity = new();
    public readonly FloatStats symmetry = new();

    public float Percent(int count) => sampleCount > 0 ? 100f * count / sampleCount : 0f;
}

internal sealed class FrameAnalysisSummary
{
    public int frameCount;
    public float firstTime;
    public float lastTime;
    public int calibratorMissingFrames;
    public int ikMissingFrames;
    public int trackingMissingFrames;
    public int mappingMissingFrames;
    public int simulatedFrames;
    public int leftAvailableFrames;
    public int rightAvailableFrames;
    public int leftFallbackFrames;
    public int rightFallbackFrames;
    public readonly Dictionary<string, int> phaseCounts = new();
    public readonly Dictionary<string, int> taskCounts = new();
    public readonly FloatStats leftValgus = new();
    public readonly FloatStats rightValgus = new();
    public readonly FloatStats leftFlexion = new();
    public readonly FloatStats rightFlexion = new();
    public readonly FloatStats swayRmsMm = new();
    public readonly FloatStats swayVelocity = new();
    public readonly FloatStats symmetry = new();
    public readonly FloatStats leftReach = new();
    public readonly FloatStats rightReach = new();
}

internal sealed class FloatStats
{
    public int count;
    public float min = float.PositiveInfinity;
    public float max = float.NegativeInfinity;
    public double sum;

    public float Mean => count > 0 ? (float)(sum / count) : 0f;

    public void Add(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        count++;
        sum += value;
        if (value < min) min = value;
        if (value > max) max = value;
    }

    public string Format(string unit = "")
    {
        if (count == 0) return "veri yok";
        return $"ort {Mean:F2}{unit} | min {min:F2}{unit} | max {max:F2}{unit}";
    }
}

internal static class ReplayArchiveScanner
{
    public const string DefaultArchiveRoot = "Assets/KayitSonuclari";

    public static List<ReplayArchiveEntry> Scan(string rootFolder)
    {
        var entries = new List<ReplayArchiveEntry>();
        rootFolder = NormalizeFolder(rootFolder);
        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
            return entries;

        Dictionary<string, ReplayArchiveEntry> sessionsByFile = ScanSessionReports(rootFolder, entries);
        ScanReplayFolders(rootFolder, entries, sessionsByFile);
        AttachDiagnostics(rootFolder, entries);

        entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return entries;
    }

    public static void LoadEvents(ReplayArchiveEntry entry)
    {
        if (entry == null || entry.EventsLoaded) return;
        entry.EventsLoaded = true;
        entry.Events.Clear();

        if (!entry.HasEvents) return;

        foreach (string line in File.ReadLines(entry.EventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                ReplayEvent replayEvent = JsonUtility.FromJson<ReplayEvent>(line);
                if (replayEvent == null) continue;
                entry.Events.Add(replayEvent);

                if (replayEvent.eventType == "result_ready" && IsRealResult(replayEvent.result) && entry.TaskResults.Count == 0)
                    entry.TaskResults.Add(ArchiveTaskResult.FromReplayResult(replayEvent.result, replayEvent.taskIndex));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ReplayArchiveScanner] Event okunamadi: {entry.EventsPath}\n{ex.Message}");
            }
        }
    }

    public static FrameAnalysisSummary AnalyzeFrames(ReplayArchiveEntry entry)
    {
        if (entry == null || !entry.HasFrames) return null;

        var summary = new FrameAnalysisSummary();
        foreach (string line in File.ReadLines(entry.FramesPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            ReplayFrame frame;
            try
            {
                frame = JsonUtility.FromJson<ReplayFrame>(line);
            }
            catch
            {
                continue;
            }

            if (frame == null) continue;
            summary.frameCount++;
            if (summary.frameCount == 1) summary.firstTime = frame.elapsedSeconds;
            summary.lastTime = frame.elapsedSeconds;
            AddCount(summary.phaseCounts, string.IsNullOrEmpty(frame.phase) ? "Unknown" : frame.phase);
            AddCount(summary.taskCounts, string.IsNullOrEmpty(frame.taskNameTR) ? $"Task {frame.taskIndex}" : frame.taskNameTR);

            if (frame.status != null)
            {
                if (!frame.status.calibratorCalibrated) summary.calibratorMissingFrames++;
                if (!frame.status.ikCalibrated) summary.ikMissingFrames++;
                if (!frame.status.trackingAssigned) summary.trackingMissingFrames++;
                if (!frame.status.mappingCalibrated) summary.mappingMissingFrames++;
                if (frame.status.simulatedInput) summary.simulatedFrames++;
            }

            ReplayMetricSnapshot metrics = frame.metrics;
            if (metrics == null) continue;
            if (metrics.leftAvailable) summary.leftAvailableFrames++;
            if (metrics.rightAvailable) summary.rightAvailableFrames++;
            if (metrics.leftFallback) summary.leftFallbackFrames++;
            if (metrics.rightFallback) summary.rightFallbackFrames++;
            summary.leftValgus.Add(metrics.leftValgusDeg);
            summary.rightValgus.Add(metrics.rightValgusDeg);
            summary.leftFlexion.Add(metrics.leftFlexionDeg);
            summary.rightFlexion.Add(metrics.rightFlexionDeg);
            summary.swayRmsMm.Add(metrics.pelvisSwayRmsMeters * 1000f);
            summary.swayVelocity.Add(metrics.swayVelocityMps);
            summary.symmetry.Add(metrics.symmetryPct);
            summary.leftReach.Add(metrics.leftStanceAnteriorReachPct);
            summary.rightReach.Add(metrics.rightStanceAnteriorReachPct);
        }

        entry.FrameAnalysis = summary;
        return summary;
    }

    public static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return DefaultArchiveRoot;
        return folder.Replace('\\', '/').TrimEnd('/');
    }

    public static bool TryParseTimestamp(string value, out DateTime timestamp)
    {
        timestamp = DateTime.MinValue;
        if (string.IsNullOrEmpty(value)) return false;
        return DateTime.TryParseExact(value, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    private static Dictionary<string, ReplayArchiveEntry> ScanSessionReports(string rootFolder, List<ReplayArchiveEntry> entries)
    {
        var sessionsByFile = new Dictionary<string, ReplayArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var sessionFiles = Directory.GetFiles(rootFolder, "session_*.json", SearchOption.AllDirectories);
        foreach (string sessionPath in sessionFiles)
        {
            if (sessionPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            ReplayArchiveEntry entry = CreateSessionEntry(sessionPath);
            if (entry == null) continue;

            string csvPath = Path.ChangeExtension(sessionPath, ".csv");
            if (File.Exists(csvPath)) entry.SessionCsvPath = NormalizeFolder(csvPath);

            entries.Add(entry);
            sessionsByFile[Path.GetFileName(sessionPath)] = entry;
        }

        foreach (string csvPath in Directory.GetFiles(rootFolder, "session_*.csv", SearchOption.AllDirectories))
        {
            if (csvPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (sessionsByFile.ContainsKey(Path.GetFileName(Path.ChangeExtension(csvPath, ".json")))) continue;

            ReplayArchiveEntry entry = CreateCsvOnlySessionEntry(csvPath);
            entries.Add(entry);
            sessionsByFile[Path.GetFileName(csvPath)] = entry;
        }

        return sessionsByFile;
    }

    private static ReplayArchiveEntry CreateSessionEntry(string sessionPath)
    {
        SessionReportJson report = null;
        try
        {
            report = JsonUtility.FromJson<SessionReportJson>(File.ReadAllText(sessionPath, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ReplayArchiveScanner] Session JSON okunamadi: {sessionPath}\n{ex.Message}");
        }

        string timestampRaw = report != null && !string.IsNullOrEmpty(report.sessionTimestamp)
            ? report.sessionTimestamp
            : TimestampFromFile(sessionPath, "session_");

        TryParseTimestamp(timestampRaw, out DateTime timestamp);
        var entry = new ReplayArchiveEntry
        {
            Key = Path.GetFileNameWithoutExtension(sessionPath),
            SourceLabel = "Session Report",
            GameName = "Session Report",
            TimestampRaw = timestampRaw,
            Timestamp = timestamp,
            HasTimestamp = timestamp != DateTime.MinValue,
            SessionJsonPath = NormalizeFolder(sessionPath),
            SessionReport = report
        };

        if (report != null && report.tasks != null)
        {
            for (int index = 0; index < report.tasks.Count; index++)
                entry.TaskResults.Add(ArchiveTaskResult.FromSession(report.tasks[index], index));
        }

        return entry;
    }

    private static ReplayArchiveEntry CreateCsvOnlySessionEntry(string csvPath)
    {
        string timestampRaw = TimestampFromFile(csvPath, "session_");
        TryParseTimestamp(timestampRaw, out DateTime timestamp);
        var entry = new ReplayArchiveEntry
        {
            Key = Path.GetFileNameWithoutExtension(csvPath),
            SourceLabel = "Session CSV",
            GameName = "Session CSV",
            TimestampRaw = timestampRaw,
            Timestamp = timestamp,
            HasTimestamp = timestamp != DateTime.MinValue,
            SessionCsvPath = NormalizeFolder(csvPath)
        };
        LoadCsvTasks(entry);
        return entry;
    }

    private static void ScanReplayFolders(string rootFolder, List<ReplayArchiveEntry> entries, Dictionary<string, ReplayArchiveEntry> sessionsByFile)
    {
        foreach (string replayFolder in Directory.GetDirectories(rootFolder, "replay_*", SearchOption.AllDirectories))
        {
            string manifestPath = Path.Combine(replayFolder, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            ReplayManifest manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<ReplayManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ReplayArchiveScanner] Manifest okunamadi: {manifestPath}\n{ex.Message}");
            }

            string timestampRaw = manifest != null && !string.IsNullOrEmpty(manifest.sessionTimestamp)
                ? manifest.sessionTimestamp
                : TimestampFromFile(replayFolder, "replay_");
            TryParseTimestamp(timestampRaw, out DateTime timestamp);

            ReplayArchiveEntry entry = MatchReportForReplay(manifest, timestamp, sessionsByFile, entries);
            if (entry == null)
            {
                entry = new ReplayArchiveEntry();
                entries.Add(entry);
            }

            entry.Key = !string.IsNullOrEmpty(entry.Key) ? entry.Key : Path.GetFileName(replayFolder);
            entry.SourceLabel = entry.HasSessionReport ? "Replay + Report" : "Replay";
            entry.GameName = manifest != null && !string.IsNullOrEmpty(manifest.sessionName) ? manifest.sessionName : Path.GetFileName(replayFolder);
            entry.TimestampRaw = timestampRaw;
            entry.Timestamp = timestamp;
            entry.HasTimestamp = timestamp != DateTime.MinValue;
            entry.ReplayFolderPath = NormalizeFolder(replayFolder);
            entry.ManifestPath = NormalizeFolder(manifestPath);
            entry.FramesPath = NormalizeFolder(Path.Combine(replayFolder, manifest != null && !string.IsNullOrEmpty(manifest.framesFile) ? manifest.framesFile : "frames.jsonl"));
            entry.EventsPath = NormalizeFolder(Path.Combine(replayFolder, manifest != null && !string.IsNullOrEmpty(manifest.eventsFile) ? manifest.eventsFile : "events.jsonl"));
            entry.Manifest = manifest;
        }
    }

    private static ReplayArchiveEntry MatchReportForReplay(ReplayManifest manifest, DateTime replayTime, Dictionary<string, ReplayArchiveEntry> sessionsByFile, List<ReplayArchiveEntry> entries)
    {
        if (manifest != null && !string.IsNullOrEmpty(manifest.linkedSessionReportJson))
        {
            string linkedFile = Path.GetFileName(manifest.linkedSessionReportJson);
            if (!string.IsNullOrEmpty(linkedFile) && sessionsByFile.TryGetValue(linkedFile, out ReplayArchiveEntry linkedEntry))
                return linkedEntry;
        }

        ReplayArchiveEntry best = null;
        double bestSeconds = double.MaxValue;
        for (int index = 0; index < entries.Count; index++)
        {
            ReplayArchiveEntry candidate = entries[index];
            if (!candidate.HasSessionReport || !candidate.HasTimestamp || replayTime == DateTime.MinValue) continue;

            double seconds = Math.Abs((candidate.Timestamp - replayTime).TotalSeconds);
            if (seconds < bestSeconds)
            {
                bestSeconds = seconds;
                best = candidate;
            }
        }

        return bestSeconds <= 900.0 ? best : null;
    }

    private static void AttachDiagnostics(string rootFolder, List<ReplayArchiveEntry> entries)
    {
        foreach (string diagnosticsPath in Directory.GetFiles(rootFolder, "lower_limb_diagnostics_*.csv", SearchOption.AllDirectories))
        {
            if (diagnosticsPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string timestampRaw = TimestampFromFile(diagnosticsPath, "lower_limb_diagnostics_");
            TryParseTimestamp(timestampRaw, out DateTime timestamp);
            ReplayArchiveEntry entry = FindClosestEntry(entries, timestamp, 7200.0);

            if (entry == null)
            {
                entry = new ReplayArchiveEntry
                {
                    Key = Path.GetFileNameWithoutExtension(diagnosticsPath),
                    SourceLabel = "Diagnostics",
                    GameName = "Diagnostics",
                    TimestampRaw = timestampRaw,
                    Timestamp = timestamp,
                    HasTimestamp = timestamp != DateTime.MinValue
                };
                entries.Add(entry);
            }

            entry.DiagnosticsCsvPath = NormalizeFolder(diagnosticsPath);
            entry.Diagnostics = LoadDiagnostics(diagnosticsPath);
        }
    }

    private static ReplayArchiveEntry FindClosestEntry(List<ReplayArchiveEntry> entries, DateTime timestamp, double maxSeconds)
    {
        if (timestamp == DateTime.MinValue) return null;
        ReplayArchiveEntry best = null;
        double bestSeconds = double.MaxValue;
        for (int index = 0; index < entries.Count; index++)
        {
            ReplayArchiveEntry entry = entries[index];
            if (!entry.HasTimestamp) continue;
            double seconds = Math.Abs((entry.Timestamp - timestamp).TotalSeconds);
            if (seconds < bestSeconds)
            {
                bestSeconds = seconds;
                best = entry;
            }
        }
        return bestSeconds <= maxSeconds ? best : null;
    }

    private static DiagnosticsSummary LoadDiagnostics(string path)
    {
        var summary = new DiagnosticsSummary();
        using var reader = new StreamReader(path, Encoding.UTF8);
        string headerLine = reader.ReadLine();
        if (string.IsNullOrEmpty(headerLine)) return summary;

        string[] headers = headerLine.Split(',');
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < headers.Length; index++)
            columns[headers[index]] = index;

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(',');
            summary.sampleCount++;
            if (ReadBool(parts, columns, "mark")) summary.markerCount++;
            if (ReadBool(parts, columns, "calibratorCalibrated")) summary.calibratorReadyCount++;
            if (ReadBool(parts, columns, "ikCalibrated")) summary.ikReadyCount++;
            if (ReadBool(parts, columns, "trackingAssigned")) summary.trackingReadyCount++;
            if (ReadBool(parts, columns, "mappingCalibrated")) summary.mappingReadyCount++;
            if (ReadBool(parts, columns, "simulatedInput")) summary.simulatedInputCount++;
            if (ReadBool(parts, columns, "leftFallback")) summary.leftFallbackCount++;
            if (ReadBool(parts, columns, "rightFallback")) summary.rightFallbackCount++;
            summary.leftValgus.Add(ReadFloat(parts, columns, "leftValgus_deg"));
            summary.rightValgus.Add(ReadFloat(parts, columns, "rightValgus_deg"));
            summary.leftFlexion.Add(ReadFloat(parts, columns, "leftFlex_deg"));
            summary.rightFlexion.Add(ReadFloat(parts, columns, "rightFlex_deg"));
            summary.swayRmsMm.Add(ReadFloat(parts, columns, "swayRMS_mm"));
            summary.swayVelocity.Add(ReadFloat(parts, columns, "swayVelocity_mps"));
            summary.symmetry.Add(ReadFloat(parts, columns, "symmetry_pct"));
        }

        return summary;
    }

    private static void LoadCsvTasks(ReplayArchiveEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.SessionCsvPath) || !File.Exists(entry.SessionCsvPath)) return;
        using var reader = new StreamReader(entry.SessionCsvPath, Encoding.UTF8);
        string headerLine = reader.ReadLine();
        if (string.IsNullOrEmpty(headerLine)) return;

        string[] headers = ParseCsvLine(headerLine).ToArray();
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < headers.Length; index++) columns[headers[index]] = index;

        string line;
        int taskIndex = 0;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            List<string> cells = ParseCsvLine(line);
            if (Cell(cells, columns, "TaskType") == "Session Average") continue;

            var result = new ArchiveTaskResult
            {
                taskIndex = taskIndex++,
                taskType = Cell(cells, columns, "TaskType"),
                taskNameTR = Cell(cells, columns, "TaskName"),
                durationSec = CellFloat(cells, columns, "Duration_s"),
                meanValgusLeft = CellFloat(cells, columns, "MeanValgusL_deg"),
                meanValgusRight = CellFloat(cells, columns, "MeanValgusR_deg"),
                maxValgusLeft = CellFloat(cells, columns, "MaxValgusL_deg"),
                maxValgusRight = CellFloat(cells, columns, "MaxValgusR_deg"),
                meanFlexLeft = CellFloat(cells, columns, "MeanFlexL_deg"),
                meanFlexRight = CellFloat(cells, columns, "MeanFlexR_deg"),
                maxFlexLeft = CellFloat(cells, columns, "MaxFlexL_deg"),
                maxFlexRight = CellFloat(cells, columns, "MaxFlexR_deg"),
                swayRmsMm = CellFloat(cells, columns, "MeanSwayRMS_mm"),
                swayVelocityMps = CellFloat(cells, columns, "MeanSwayVelocity_mps"),
                symmetryIndexPct = CellFloat(cells, columns, "SymmetryIndex_pct"),
                leftReachPct = CellFloat(cells, columns, "MaxLeftStanceAnteriorReach_pct"),
                rightReachPct = CellFloat(cells, columns, "MaxRightStanceAnteriorReach_pct"),
                valgusRisk = CellFloat(cells, columns, "ValgusRisk"),
                asymmetryRisk = CellFloat(cells, columns, "AsymmetryRisk"),
                flexionRisk = CellFloat(cells, columns, "FlexionRisk"),
                balanceRisk = CellFloat(cells, columns, "BalanceRisk"),
                reachRisk = CellFloat(cells, columns, "ReachRisk"),
                totalRisk = CellFloat(cells, columns, "TotalRisk"),
                gameScore = CellFloat(cells, columns, "GameScore"),
                taskSummaryTR = Cell(cells, columns, "TaskSummaryTR")
            };
            result.riskGrade = TaskResult.GetRiskGrade(result.totalRisk);
            entry.TaskResults.Add(result);
        }
    }

    private static bool IsRealResult(ReplayTaskResultSnapshot result)
    {
        return result != null && (!string.IsNullOrEmpty(result.taskType) || !string.IsNullOrEmpty(result.taskNameTR) || result.gameScore > 0f || result.totalRiskScore > 0f);
    }

    private static void AddCount(Dictionary<string, int> counts, string key)
    {
        if (string.IsNullOrEmpty(key)) key = "Unknown";
        counts.TryGetValue(key, out int current);
        counts[key] = current + 1;
    }

    private static string TimestampFromFile(string path, string prefix)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(name)) name = Path.GetFileName(path);
        return name != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name.Substring(prefix.Length) : name;
    }

    private static bool ReadBool(string[] parts, Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out int index) || index < 0 || index >= parts.Length) return false;
        string value = parts[index];
        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static float ReadFloat(string[] parts, Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out int index) || index < 0 || index >= parts.Length) return 0f;
        return float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
    }

    private static string Cell(List<string> cells, Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out int index) || index < 0 || index >= cells.Count) return "";
        return cells[index];
    }

    private static float CellFloat(List<string> cells, Dictionary<string, int> columns, string name)
    {
        string value = Cell(cells, columns, name);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}

public sealed class KayitAnaliziWindow : EditorWindow
{
    private string _rootFolder = ReplayArchiveScanner.DefaultArchiveRoot;
    private List<ReplayArchiveEntry> _entries = new();
    private int _selectedIndex;
    private int _tabIndex;
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private Transform _avatarRoot;

    private static readonly string[] Tabs = { "Ozet", "Gorevler", "Timeline", "Frame", "Diagnostics", "Dosyalar" };

    [MenuItem("Tools/Gamification/Kayit Analizi", priority = 180)]
    private static void OpenWindow()
    {
        var window = GetWindow<KayitAnaliziWindow>("Kayit Analizi");
        window.minSize = new Vector2(920f, 620f);
        window.Refresh();
    }

    private void OnEnable()
    {
        if (_entries.Count == 0) Refresh();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        DrawEntryList();
        DrawSelectedEntry();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Kayit Kok Klasoru", GUILayout.Width(110f));
        _rootFolder = EditorGUILayout.TextField(_rootFolder, EditorStyles.toolbarTextField);
        if (GUILayout.Button("Sec", EditorStyles.toolbarButton, GUILayout.Width(42f)))
        {
            string selected = EditorUtility.OpenFolderPanel("KayitSonuclari klasoru", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected)) _rootFolder = ReplayArchiveScanner.NormalizeFolder(selected);
        }
        if (GUILayout.Button("Tumunu Export", EditorStyles.toolbarButton, GUILayout.Width(92f))) ExportAllEntries();
        if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(64f))) Refresh();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEntryList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(330f));
        EditorGUILayout.LabelField($"Kayitlar ({_entries.Count})", EditorStyles.boldLabel);
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUI.skin.box);

        for (int index = 0; index < _entries.Count; index++)
        {
            ReplayArchiveEntry entry = _entries[index];
            GUIStyle style = index == _selectedIndex ? EditorStyles.helpBox : GUI.skin.button;
            if (GUILayout.Button(BuildEntryButtonText(entry), style, GUILayout.MinHeight(54f)))
                _selectedIndex = index;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedEntry()
    {
        EditorGUILayout.BeginVertical();
        if (_entries.Count == 0)
        {
            EditorGUILayout.HelpBox("Kayit bulunamadi. Varsayilan klasor: Assets/KayitSonuclari", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _entries.Count - 1);
        ReplayArchiveEntry entry = _entries[_selectedIndex];

        EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Kaynak: {entry.SourceLabel} | Gorev: {entry.TaskCount} | Sure: {entry.DurationSeconds:F1}s | Frame: {entry.FrameCount} | Event: {entry.EventCount}");
        EditorGUILayout.LabelField($"Ortalama skor: {entry.AverageGameScore:F1} | Replay: {(entry.HasReplay ? "var" : "yok")} | Frame: {(entry.HasFrames ? "var" : "yok")} | Report: {(entry.HasSessionReport ? "var" : "yok")}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analiz Raporu Export", GUILayout.Height(26f)))
            ExportEntry(entry, true);
        GUI.enabled = entry.HasFrames;
        if (GUILayout.Button("Frame Analizi Hesapla", GUILayout.Height(26f)))
            ReplayArchiveScanner.AnalyzeFrames(entry);
        if (GUILayout.Button("Replay Sahnesini Hazirla", GUILayout.Height(26f)))
            ReplaySceneSetupUtility.PrepareReplayScene(entry, _avatarRoot, false, out _);
        if (GUILayout.Button("Hazirla ve Oynat", GUILayout.Height(26f)))
            ReplaySceneSetupUtility.PrepareReplayScene(entry, _avatarRoot, true, out _);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _avatarRoot = (Transform)EditorGUILayout.ObjectField("Replay Avatar Root", _avatarRoot, typeof(Transform), true);
        if (GUILayout.Button("Sahneden Bul", GUILayout.Width(96f)))
            _avatarRoot = ReplaySceneSetupUtility.FindBestReplayAvatarRoot();
        EditorGUILayout.EndHorizontal();

        _tabIndex = GUILayout.Toolbar(_tabIndex, Tabs);
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
        switch (_tabIndex)
        {
            case 0: DrawSummary(entry); break;
            case 1: DrawTasks(entry); break;
            case 2: DrawTimeline(entry); break;
            case 3: DrawFrameAnalysis(entry); break;
            case 4: DrawDiagnostics(entry); break;
            case 5: DrawFiles(entry); break;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSummary(ReplayArchiveEntry entry)
    {
        if (entry.TaskResults.Count == 0)
            ReplayArchiveScanner.LoadEvents(entry);

        EditorGUILayout.Space(6f);
        if (entry.Manifest != null)
        {
            EditorGUILayout.HelpBox(
                $"Session: {entry.Manifest.sessionName}\n" +
                $"Scene: {entry.Manifest.sceneName} | Platform: {entry.Manifest.platform}\n" +
                $"Kalibrasyon: calibrator={entry.Manifest.calibration?.calibratorCalibrated}, IK={entry.Manifest.calibration?.ikCalibrated}, tracking={entry.Manifest.calibration?.trackingAssigned}, mapping={entry.Manifest.calibration?.mappingCalibrated}\n" +
                $"Veri kaynagi: {entry.Manifest.calibration?.dataSourceTR}\n" +
                $"Kapsam: {entry.Manifest.interpretationScopeTR}",
                MessageType.Info);
        }

        EditorGUILayout.LabelField("Risk Dagilimi", EditorStyles.boldLabel);
        Dictionary<string, int> gradeCounts = BuildGradeCounts(entry);
        foreach (KeyValuePair<string, int> pair in gradeCounts)
            EditorGUILayout.LabelField(pair.Key, pair.Value.ToString(CultureInfo.InvariantCulture));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Oncelikli Gorevler", EditorStyles.boldLabel);
        List<ArchiveTaskResult> sorted = new(entry.TaskResults);
        sorted.Sort((a, b) => a.gameScore.CompareTo(b.gameScore));
        int count = Mathf.Min(5, sorted.Count);
        for (int index = 0; index < count; index++)
        {
            ArchiveTaskResult task = sorted[index];
            EditorGUILayout.HelpBox($"{task.taskNameTR} | Skor {task.gameScore:F1} | Risk {task.totalRisk:F3} ({task.riskGrade})\n{task.taskSummaryTR}", MessageType.None);
        }
    }

    private void DrawTasks(ReplayArchiveEntry entry)
    {
        if (entry.TaskResults.Count == 0)
        {
            ReplayArchiveScanner.LoadEvents(entry);
            if (entry.TaskResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Gorev sonucu bulunamadi. Session JSON/CSV veya result_ready event'i gerekli.", MessageType.Warning);
                return;
            }
        }

        for (int index = 0; index < entry.TaskResults.Count; index++)
        {
            ArchiveTaskResult task = entry.TaskResults[index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{index + 1}. {task.taskNameTR} ({task.taskType})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Sure: {task.durationSec:F1}s | Skor: {task.gameScore:F1} | Risk: {task.totalRisk:F3} | Grade: {task.riskGrade}");
            EditorGUILayout.LabelField($"Valgus L/R mean: {task.meanValgusLeft:F1}/{task.meanValgusRight:F1} deg | max: {task.maxValgusLeft:F1}/{task.maxValgusRight:F1} deg");
            EditorGUILayout.LabelField($"Flex L/R mean: {task.meanFlexLeft:F1}/{task.meanFlexRight:F1} deg | max: {task.maxFlexLeft:F1}/{task.maxFlexRight:F1} deg");
            EditorGUILayout.LabelField($"Sway RMS: {task.swayRmsMm:F1} mm | Sway vel: {task.swayVelocityMps:F3} m/s | SI: {task.symmetryIndexPct:F1}% | Reach L/R: {task.leftReachPct:F1}/{task.rightReachPct:F1}%");
            EditorGUILayout.LabelField($"Riskler | valgus {task.valgusRisk:F3}, asimetri {task.asymmetryRisk:F3}, fleksiyon {task.flexionRisk:F3}, denge {task.balanceRisk:F3}, reach {task.reachRisk:F3}");
            EditorGUILayout.LabelField(task.taskSummaryTR, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawTimeline(ReplayArchiveEntry entry)
    {
        ReplayArchiveScanner.LoadEvents(entry);
        if (entry.Events.Count == 0)
        {
            EditorGUILayout.HelpBox("Event dosyasi bulunamadi veya bos.", MessageType.Warning);
            return;
        }

        for (int index = 0; index < entry.Events.Count; index++)
        {
            ReplayEvent replayEvent = entry.Events[index];
            string resultText = replayEvent.eventType == "result_ready" && replayEvent.result != null
                ? $" | Skor {replayEvent.result.gameScore:F1} | {replayEvent.result.riskGrade}"
                : "";
            EditorGUILayout.LabelField($"{replayEvent.elapsedSeconds,7:F2}s | {replayEvent.eventType,-18} | {replayEvent.phase,-12} | {replayEvent.taskNameTR}{resultText}");
        }
    }

    private void DrawFrameAnalysis(ReplayArchiveEntry entry)
    {
        if (!entry.HasFrames)
        {
            EditorGUILayout.HelpBox("Bu kayitta frames.jsonl yok; replay oynatilamaz ve frame analizi yapilamaz.", MessageType.Warning);
            return;
        }

        if (entry.FrameAnalysis == null)
        {
            EditorGUILayout.HelpBox("Frame analizi henuz hesaplanmadi. Buyuk dosyalarda birkac saniye surebilir.", MessageType.Info);
            if (GUILayout.Button("Frame Analizi Hesapla", GUILayout.Height(30f)))
                ReplayArchiveScanner.AnalyzeFrames(entry);
            return;
        }

        FrameAnalysisSummary frame = entry.FrameAnalysis;
        EditorGUILayout.LabelField($"Frame sayisi: {frame.frameCount} | Zaman: {frame.firstTime:F2}s - {frame.lastTime:F2}s", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Kalibrasyon eksik frame: {frame.calibratorMissingFrames} | IK eksik: {frame.ikMissingFrames} | Tracking eksik: {frame.trackingMissingFrames} | Mapping eksik: {frame.mappingMissingFrames} | Simulated: {frame.simulatedFrames}");
        EditorGUILayout.LabelField($"Sol/Sag available: {frame.leftAvailableFrames}/{frame.rightAvailableFrames} | fallback: {frame.leftFallbackFrames}/{frame.rightFallbackFrames}");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Metrik Araliklari", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Valgus L", frame.leftValgus.Format(" deg"));
        EditorGUILayout.LabelField("Valgus R", frame.rightValgus.Format(" deg"));
        EditorGUILayout.LabelField("Flex L", frame.leftFlexion.Format(" deg"));
        EditorGUILayout.LabelField("Flex R", frame.rightFlexion.Format(" deg"));
        EditorGUILayout.LabelField("Sway RMS", frame.swayRmsMm.Format(" mm"));
        EditorGUILayout.LabelField("Sway vel", frame.swayVelocity.Format(" m/s"));
        EditorGUILayout.LabelField("Symmetry", frame.symmetry.Format("%"));
        EditorGUILayout.LabelField("Reach L", frame.leftReach.Format("%"));
        EditorGUILayout.LabelField("Reach R", frame.rightReach.Format("%"));

        EditorGUILayout.Space(4f);
        DrawCounts("Phase dagilimi", frame.phaseCounts);
        DrawCounts("Task frame dagilimi", frame.taskCounts);
    }

    private void DrawDiagnostics(ReplayArchiveEntry entry)
    {
        if (!entry.HasDiagnostics || entry.Diagnostics == null)
        {
            EditorGUILayout.HelpBox("Bu kayitla eslesen diagnostics CSV yok.", MessageType.Info);
            return;
        }

        DiagnosticsSummary d = entry.Diagnostics;
        EditorGUILayout.LabelField($"Sample: {d.sampleCount} | Marker: {d.markerCount}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Calibrator ready: {d.Percent(d.calibratorReadyCount):F1}% | IK: {d.Percent(d.ikReadyCount):F1}% | Tracking: {d.Percent(d.trackingReadyCount):F1}% | Mapping: {d.Percent(d.mappingReadyCount):F1}%");
        EditorGUILayout.LabelField($"Simulated input: {d.Percent(d.simulatedInputCount):F1}% | Fallback L/R: {d.Percent(d.leftFallbackCount):F1}% / {d.Percent(d.rightFallbackCount):F1}%");
        EditorGUILayout.LabelField("Valgus L", d.leftValgus.Format(" deg"));
        EditorGUILayout.LabelField("Valgus R", d.rightValgus.Format(" deg"));
        EditorGUILayout.LabelField("Flex L", d.leftFlexion.Format(" deg"));
        EditorGUILayout.LabelField("Flex R", d.rightFlexion.Format(" deg"));
        EditorGUILayout.LabelField("Sway RMS", d.swayRmsMm.Format(" mm"));
        EditorGUILayout.LabelField("Sway vel", d.swayVelocity.Format(" m/s"));
        EditorGUILayout.LabelField("Symmetry", d.symmetry.Format("%"));
    }

    private void DrawFiles(ReplayArchiveEntry entry)
    {
        DrawFileRow("Replay", entry.ReplayFolderPath);
        DrawFileRow("Manifest", entry.ManifestPath);
        DrawFileRow("Frames", entry.FramesPath);
        DrawFileRow("Events", entry.EventsPath);
        DrawFileRow("Session JSON", entry.SessionJsonPath);
        DrawFileRow("Session CSV", entry.SessionCsvPath);
        DrawFileRow("Diagnostics", entry.DiagnosticsCsvPath);
    }

    private void DrawCounts(string title, Dictionary<string, int> counts)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (KeyValuePair<string, int> pair in counts)
            EditorGUILayout.LabelField(pair.Key, pair.Value.ToString(CultureInfo.InvariantCulture));
    }

    private void DrawFileRow(string label, string path)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(95f));
        EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(path) ? "-" : path, GUILayout.Height(18f));
        GUI.enabled = !string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path));
        if (GUILayout.Button("Ac", GUILayout.Width(42f))) EditorUtility.RevealInFinder(path);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private string BuildEntryButtonText(ReplayArchiveEntry entry)
    {
        string flags = $"R:{(entry.HasReplay ? "1" : "0")} F:{(entry.HasFrames ? "1" : "0")} J:{(entry.HasSessionReport ? "1" : "0")}";
        return $"{entry.TimestampDisplay}\n{entry.GameName}\n{flags} | Skor {entry.AverageGameScore:F1}";
    }

    private Dictionary<string, int> BuildGradeCounts(ReplayArchiveEntry entry)
    {
        var counts = new Dictionary<string, int>
        {
            ["Yesil"] = 0,
            ["Sari"] = 0,
            ["Turuncu"] = 0,
            ["Kirmizi"] = 0
        };

        for (int index = 0; index < entry.TaskResults.Count; index++)
        {
            string grade = entry.TaskResults[index].riskGrade;
            if (grade == "Yeşil") counts["Yesil"]++;
            else if (grade == "Sarı") counts["Sari"]++;
            else if (grade == "Turuncu") counts["Turuncu"]++;
            else if (grade == "Kırmızı") counts["Kirmizi"]++;
        }

        return counts;
    }

    private void Refresh()
    {
        _entries = ReplayArchiveScanner.Scan(_rootFolder);
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _entries.Count - 1));
        Repaint();
    }

    private void ExportAllEntries()
    {
        if (_entries == null || _entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Export", "Export edilecek kayit yok.", "Tamam");
            return;
        }

        for (int index = 0; index < _entries.Count; index++)
            ExportEntry(_entries[index], false);

        string exportRoot = Path.Combine(ReplayArchiveScanner.DefaultArchiveRoot, "AnalizRaporlari");
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(exportRoot);
    }

    private void ExportEntry(ReplayArchiveEntry entry, bool reveal)
    {
        if (entry == null) return;
        ReplayArchiveScanner.LoadEvents(entry);
        if (entry.HasFrames && entry.FrameAnalysis == null)
            ReplayArchiveScanner.AnalyzeFrames(entry);

        string exportRoot = Path.Combine(ReplayArchiveScanner.DefaultArchiveRoot, "AnalizRaporlari");
        Directory.CreateDirectory(exportRoot);
        string safeName = MakeSafeFileName($"{entry.TimestampRaw}_{entry.GameName}");
        string mdPath = Path.Combine(exportRoot, safeName + "_analiz.md");
        string csvPath = Path.Combine(exportRoot, safeName + "_gorevler.csv");

        File.WriteAllText(mdPath, BuildMarkdownReport(entry), Encoding.UTF8);
        File.WriteAllText(csvPath, BuildTasksCsv(entry), Encoding.UTF8);
        AssetDatabase.Refresh();
        if (reveal) EditorUtility.RevealInFinder(mdPath);
    }

    private string BuildMarkdownReport(ReplayArchiveEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Kayit Analizi - {entry.DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"- Kaynak: {entry.SourceLabel}");
        sb.AppendLine($"- Gorev sayisi: {entry.TaskCount}");
        sb.AppendLine($"- Sure: {entry.DurationSeconds:F1} s");
        sb.AppendLine($"- Frame: {entry.FrameCount}");
        sb.AppendLine($"- Event: {entry.EventCount}");
        sb.AppendLine($"- Ortalama skor: {entry.AverageGameScore:F1}");
        if (entry.Manifest != null)
        {
            sb.AppendLine($"- Scene: {entry.Manifest.sceneName}");
            sb.AppendLine($"- Platform: {entry.Manifest.platform}");
            sb.AppendLine($"- Veri kaynagi: {entry.Manifest.calibration?.dataSourceTR}");
            sb.AppendLine($"- Kapsam: {entry.Manifest.interpretationScopeTR}");
        }

        sb.AppendLine();
        sb.AppendLine("## Gorev Sonuclari");
        sb.AppendLine();
        sb.AppendLine("| # | Gorev | Skor | Grade | Total Risk | Ozet |");
        sb.AppendLine("|---|---|---:|---|---:|---|");
        for (int index = 0; index < entry.TaskResults.Count; index++)
        {
            ArchiveTaskResult task = entry.TaskResults[index];
            sb.AppendLine($"| {index + 1} | {EscapeMarkdown(task.taskNameTR)} | {task.gameScore:F1} | {task.riskGrade} | {task.totalRisk:F3} | {EscapeMarkdown(task.taskSummaryTR)} |");
        }

        if (entry.FrameAnalysis != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Frame Analizi");
            sb.AppendLine($"- Frame sayisi: {entry.FrameAnalysis.frameCount}");
            sb.AppendLine($"- Valgus L: {entry.FrameAnalysis.leftValgus.Format(" deg")}");
            sb.AppendLine($"- Valgus R: {entry.FrameAnalysis.rightValgus.Format(" deg")}");
            sb.AppendLine($"- Flex L: {entry.FrameAnalysis.leftFlexion.Format(" deg")}");
            sb.AppendLine($"- Flex R: {entry.FrameAnalysis.rightFlexion.Format(" deg")}");
            sb.AppendLine($"- Sway RMS: {entry.FrameAnalysis.swayRmsMm.Format(" mm")}");
        }

        if (entry.Diagnostics != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Diagnostics");
            sb.AppendLine($"- Sample: {entry.Diagnostics.sampleCount}");
            sb.AppendLine($"- Calibrator ready: {entry.Diagnostics.Percent(entry.Diagnostics.calibratorReadyCount):F1}%");
            sb.AppendLine($"- IK ready: {entry.Diagnostics.Percent(entry.Diagnostics.ikReadyCount):F1}%");
            sb.AppendLine($"- Tracking ready: {entry.Diagnostics.Percent(entry.Diagnostics.trackingReadyCount):F1}%");
            sb.AppendLine($"- Mapping ready: {entry.Diagnostics.Percent(entry.Diagnostics.mappingReadyCount):F1}%");
        }

        return sb.ToString();
    }

    private string BuildTasksCsv(ReplayArchiveEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Index,TaskType,TaskName,Duration_s,GameScore,RiskGrade,TotalRisk,ValgusRisk,AsymmetryRisk,FlexionRisk,BalanceRisk,ReachRisk,MeanValgusL,MeanValgusR,MaxValgusL,MaxValgusR,MeanFlexL,MeanFlexR,MaxFlexL,MaxFlexR,SwayRMS_mm,SwayVelocity_mps,Symmetry_pct,LeftReach_pct,RightReach_pct,SummaryTR");
        for (int index = 0; index < entry.TaskResults.Count; index++)
        {
            ArchiveTaskResult task = entry.TaskResults[index];
            sb.AppendLine(string.Join(",",
                index + 1,
                EscapeCsv(task.taskType),
                EscapeCsv(task.taskNameTR),
                F(task.durationSec),
                F(task.gameScore),
                EscapeCsv(task.riskGrade),
                F(task.totalRisk),
                F(task.valgusRisk),
                F(task.asymmetryRisk),
                F(task.flexionRisk),
                F(task.balanceRisk),
                F(task.reachRisk),
                F(task.meanValgusLeft),
                F(task.meanValgusRight),
                F(task.maxValgusLeft),
                F(task.maxValgusRight),
                F(task.meanFlexLeft),
                F(task.meanFlexRight),
                F(task.maxFlexLeft),
                F(task.maxFlexRight),
                F(task.swayRmsMm),
                F(task.swayVelocityMps),
                F(task.symmetryIndexPct),
                F(task.leftReachPct),
                F(task.rightReachPct),
                EscapeCsv(task.taskSummaryTR)));
        }
        return sb.ToString();
    }

    private static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static string EscapeMarkdown(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.Replace("|", "\\|").Replace("\n", " ");
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace(' ', '_');
    }
}

public sealed class ReplaySceneSetupWindow : EditorWindow
{
    private string _rootFolder = ReplayArchiveScanner.DefaultArchiveRoot;
    private List<ReplayArchiveEntry> _entries = new();
    private int _selectedIndex;
    private Transform _avatarRoot;
    private Vector2 _scroll;

    [MenuItem("Tools/Gamification/Replay Sahnesi Hazirla", priority = 181)]
    private static void OpenWindow()
    {
        var window = GetWindow<ReplaySceneSetupWindow>("Replay Sahnesi");
        window.minSize = new Vector2(560f, 520f);
        window.Refresh();
    }

    private void OnEnable()
    {
        if (_entries.Count == 0) Refresh();
        if (_avatarRoot == null) _avatarRoot = ReplaySceneSetupUtility.FindBestReplayAvatarRoot();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("Replay Sahnesi Hazirlama", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Bu arac secilen replay icin [ReplayReview] objesini olusturur, ReplayPlaybackController ekler, Animator varsa humanoid kemiklerini; Animator yoksa SkinnedMeshRenderer rootBone hiyerarsisini ReplayAvatarDriver'a baglar ve frame verisi varsa Play Mode'da direkt oynatabilir.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        _rootFolder = EditorGUILayout.TextField("Kayit Kok Klasoru", _rootFolder);
        if (GUILayout.Button("Sec", GUILayout.Width(44f)))
        {
            string selected = EditorUtility.OpenFolderPanel("KayitSonuclari klasoru", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected)) _rootFolder = ReplayArchiveScanner.NormalizeFolder(selected);
        }
        if (GUILayout.Button("Yenile", GUILayout.Width(60f))) Refresh();
        EditorGUILayout.EndHorizontal();

        _avatarRoot = (Transform)EditorGUILayout.ObjectField("Avatar Root / Mesh / Bone", _avatarRoot, typeof(Transform), true);
        if (GUILayout.Button("Sahneden Avatar Kemik Root Bul"))
            _avatarRoot = ReplaySceneSetupUtility.FindBestReplayAvatarRoot();

        EditorGUILayout.Space(6f);
        DrawReplayList();
        EditorGUILayout.Space(6f);
        DrawActions();
        EditorGUILayout.EndScrollView();
    }

    private void DrawReplayList()
    {
        if (_entries.Count == 0)
        {
            EditorGUILayout.HelpBox("Replay kaydi bulunamadi.", MessageType.Warning);
            return;
        }

        string[] options = new string[_entries.Count];
        for (int index = 0; index < _entries.Count; index++)
            options[index] = $"{_entries[index].TimestampDisplay} | {_entries[index].GameName} | frame {(_entries[index].HasFrames ? "var" : "yok")}";

        _selectedIndex = EditorGUILayout.Popup("Replay", Mathf.Clamp(_selectedIndex, 0, _entries.Count - 1), options);
        ReplayArchiveEntry entry = _entries[_selectedIndex];
        EditorGUILayout.LabelField("Klasor", string.IsNullOrEmpty(entry.ReplayFolderPath) ? "-" : entry.ReplayFolderPath);
        EditorGUILayout.LabelField("Frame", entry.HasFrames ? $"var ({entry.FrameCount})" : "yok");
        EditorGUILayout.LabelField("Report", entry.HasSessionReport ? "var" : "yok");
    }

    private void DrawActions()
    {
        if (_entries.Count == 0) return;
        ReplayArchiveEntry entry = _entries[_selectedIndex];
        GUI.enabled = entry.HasFrames;
        if (GUILayout.Button("Sahneyi Hazirla", GUILayout.Height(34f)))
        {
            ReplaySceneSetupUtility.PrepareReplayScene(entry, _avatarRoot, false, out string message);
            Debug.Log(message);
        }
        if (GUILayout.Button("Sahneyi Hazirla ve Oynat", GUILayout.Height(38f)))
        {
            ReplaySceneSetupUtility.PrepareReplayScene(entry, _avatarRoot, true, out string message);
            Debug.Log(message);
        }
        GUI.enabled = true;
    }

    private void Refresh()
    {
        _entries = ReplayArchiveScanner.Scan(_rootFolder).FindAll(entry => entry.HasReplay);
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _entries.Count - 1));
        Repaint();
    }
}

internal static class ReplaySceneSetupUtility
{
    public static Transform FindBestReplayAvatarRoot()
    {
        if (Selection.activeTransform != null)
        {
            Transform selectedRoot = ResolveAvatarRootFromTransform(Selection.activeTransform);
            if (selectedRoot != null)
                return selectedRoot;
        }

        Animator[] animators = UnityEngine.Object.FindObjectsOfType<Animator>();
        Transform fallbackAnimator = null;
        for (int index = 0; index < animators.Length; index++)
        {
            Animator animator = animators[index];
            if (animator == null) continue;
            if (fallbackAnimator == null) fallbackAnimator = animator.transform;
            if (animator.isHuman) return animator.transform;
        }

        SkinnedMeshRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
        for (int index = 0; index < renderers.Length; index++)
        {
            Transform root = ResolveAvatarRootFromRenderer(renderers[index]);
            if (root != null) return root;
        }

        return fallbackAnimator;
    }

    public static bool PrepareReplayScene(ReplayArchiveEntry entry, Transform avatarRoot, bool playAfterPrepare, out string message)
    {
        if (entry == null)
        {
            message = "Replay kaydi secilmedi.";
            EditorUtility.DisplayDialog("Replay", message, "Tamam");
            return false;
        }

        if (!entry.HasFrames)
        {
            message = "Secilen kayitta frames.jsonl yok; replay oynatilamaz.";
            EditorUtility.DisplayDialog("Replay", message, "Tamam");
            return false;
        }

        avatarRoot = ResolveAvatarRootFromTransform(avatarRoot);
        if (avatarRoot == null)
            avatarRoot = FindBestReplayAvatarRoot();

        if (avatarRoot == null)
        {
            message = "Sahnede replay icin kullanilabilecek Animator veya SkinnedMeshRenderer/rootBone hiyerarsisi bulunamadi. Ch36 mesh objesini ya da HumanModel/mixamorig root'unu secip tekrar deneyin.";
            EditorUtility.DisplayDialog("Replay Avatar Gerekli", message, "Tamam");
            return false;
        }

        Animator avatarAnimator = avatarRoot.GetComponentInChildren<Animator>();
        ReplayAvatarDriver driver = avatarRoot.GetComponent<ReplayAvatarDriver>();
        if (driver == null)
        {
            if (!Application.isPlaying) Undo.AddComponent<ReplayAvatarDriver>(avatarRoot.gameObject);
            driver = avatarRoot.GetComponent<ReplayAvatarDriver>();
            if (driver == null) driver = avatarRoot.gameObject.AddComponent<ReplayAvatarDriver>();
        }
        driver.ConfigureSkeleton(avatarAnimator, avatarRoot);

        GameObject reviewObject = GameObject.Find("[ReplayReview]");
        if (reviewObject == null)
        {
            reviewObject = new GameObject("[ReplayReview]");
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(reviewObject, "Create Replay Review Object");
        }

        ReplayPlaybackController controller = reviewObject.GetComponent<ReplayPlaybackController>();
        if (controller == null)
        {
            if (!Application.isPlaying) Undo.AddComponent<ReplayPlaybackController>(reviewObject);
            controller = reviewObject.GetComponent<ReplayPlaybackController>();
            if (controller == null) controller = reviewObject.AddComponent<ReplayPlaybackController>();
        }

        controller.ConfigureReplay(entry.ReplayFolderPath, driver, true, true, false);
        EditorUtility.SetDirty(driver);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(reviewObject);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = reviewObject;

        if (Application.isPlaying)
        {
            controller.LoadReplay(entry.ReplayFolderPath, true);
        }
        else if (playAfterPrepare)
        {
            EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
        }

        message = playAfterPrepare
            ? $"Replay hazirlandi ve Play Mode'da acilacak: {entry.ReplayFolderPath}"
            : $"Replay sahnesi hazirlandi: {entry.ReplayFolderPath}";
        return true;
    }

    private static Transform ResolveAvatarRootFromTransform(Transform input)
    {
        if (input == null) return null;

        Animator animator = input.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman) return animator.transform;

        SkinnedMeshRenderer renderer = input.GetComponent<SkinnedMeshRenderer>();
        if (renderer != null)
            return ResolveAvatarRootFromRenderer(renderer);

        renderer = input.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer != null)
            return ResolveAvatarRootFromRenderer(renderer);

        renderer = input.GetComponentInParent<SkinnedMeshRenderer>();
        if (renderer != null)
            return ResolveAvatarRootFromRenderer(renderer);

        if (LooksLikeSkeletonRoot(input))
            return input;

        return input;
    }

    private static Transform ResolveAvatarRootFromRenderer(SkinnedMeshRenderer renderer)
    {
        if (renderer == null) return null;
        if (renderer.rootBone == null) return renderer.transform;

        Transform commonRoot = FindCommonAncestor(renderer.transform, renderer.rootBone);
        return commonRoot != null ? commonRoot : renderer.rootBone;
    }

    private static Transform FindCommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null) return null;

        var ancestors = new HashSet<Transform>();
        Transform current = a;
        while (current != null)
        {
            ancestors.Add(current);
            current = current.parent;
        }

        current = b;
        while (current != null)
        {
            if (ancestors.Contains(current)) return current;
            current = current.parent;
        }

        return null;
    }

    private static bool LooksLikeSkeletonRoot(Transform transform)
    {
        if (transform == null) return false;
        string name = transform.name.ToLowerInvariant();
        return name.Contains("hips") || name.Contains("mixamorig") || name.Contains("armature") || name.Contains("humanmodel");
    }
}
#endif