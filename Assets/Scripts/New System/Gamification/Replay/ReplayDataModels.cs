using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ReplayManifest
{
    public string schemaVersion = "1.0.0";
    public string recordingId;
    public string sessionTimestamp;
    public string sceneName;
    public string applicationVersion;
    public string unityVersion;
    public string platform;
    public string persistentDataPath;
    public string interpretationScopeTR;
    public string sessionName;
    public string sessionDescriptionTR;
    public int taskCount;
    public int frameCount;
    public int eventCount;
    public float durationSeconds;
    public ReplayRecordingSettings recordingSettings;
    public ReplayCalibrationSnapshot calibration;
    public ReplayTrackerAssignment trackerAssignment;
    public List<ReplayTaskContext> tasks = new();
    public List<string> sourcePoseIds = new();
    public List<string> ikTargetPoseIds = new();
    public List<string> avatarBonePoseIds = new();
    public List<string> recordablePoseIds = new();
    public string framesFile = "frames.jsonl";
    public string eventsFile = "events.jsonl";
    public string linkedSessionReportJson;
    public string notes;
}

[Serializable]
public class ReplayRecordingSettings
{
    public float sampleRateHz = 30f;
    public bool recordSourceDevices = true;
    public bool recordIkTargets = true;
    public bool recordAvatarBones = true;
    public bool recordAdditionalRecordables = true;
    public bool recordMetrics = true;
    public bool recordCountdownAndRestFrames = true;
}

[Serializable]
public class ReplayCalibrationSnapshot
{
    public bool calibratorCalibrated;
    public bool ikCalibrated;
    public bool trackingAssigned;
    public bool mappingCalibrated;
    public int ikCalibrationVersion;
    public bool simulatedInput;
    public string dataSourceTR;
}

[Serializable]
public class ReplayTrackerAssignment
{
    public int pelvisTrackerIndex = -1;
    public int leftFootTrackerIndex = -1;
    public int rightFootTrackerIndex = -1;
    public int leftKneeTrackerIndex = -1;
    public int rightKneeTrackerIndex = -1;
}

[Serializable]
public class ReplayTaskContext
{
    public int taskIndex;
    public string assetName;
    public string taskType;
    public string taskNameTR;
    public string instructionsTR;
    public float durationSeconds;
    public float countdownSeconds;
    public float restAfterSeconds;
    public float valgusThresholdDeg;
    public float asymmetryThresholdPct;
    public float minFlexionDeg;
    public float swayRmsThreshold;
    public float targetReachPct;
    public float landingFlexionTargetDeg;
    public string demoSequenceName;

    public static ReplayTaskContext FromTask(TaskDefinition task, int taskIndex)
    {
        var context = new ReplayTaskContext { taskIndex = taskIndex };
        if (task == null) return context;

        context.assetName = task.name;
        context.taskType = task.taskType.ToString();
        context.taskNameTR = task.taskNameTR;
        context.instructionsTR = task.instructionsTR;
        context.durationSeconds = task.durationSeconds;
        context.countdownSeconds = task.countdownSeconds;
        context.restAfterSeconds = task.restAfterSeconds;
        context.valgusThresholdDeg = task.valgusThresholdDeg;
        context.asymmetryThresholdPct = task.asymmetryThresholdPct;
        context.minFlexionDeg = task.minFlexionDeg;
        context.swayRmsThreshold = task.swayRmsThreshold;
        context.targetReachPct = task.targetReachPct;
        context.landingFlexionTargetDeg = task.landingFlexionTargetDeg;
        context.demoSequenceName = task.demoSequence != null ? task.demoSequence.name : "";
        return context;
    }
}

[Serializable]
public class ReplayFrame
{
    public int frameIndex;
    public float elapsedSeconds;
    public string phase;
    public int taskIndex = -1;
    public string taskType;
    public string taskNameTR;
    public float taskElapsedSeconds;
    public ReplayCalibrationSnapshot status;
    public ReplayMetricSnapshot metrics;
    public bool isDataGap;
    public List<ReplayPose> sourcePoses = new();
    public List<ReplayPose> ikTargetPoses = new();
    public List<ReplayPose> avatarBonePoses = new();
    public List<ReplayPose> recordablePoses = new();
}

[Serializable]
public class ReplayEvent
{
    public int eventIndex;
    public float elapsedSeconds;
    public string eventType;
    public string phase;
    public int taskIndex = -1;
    public string taskType;
    public string taskNameTR;
    public string message;
    public ReplayTaskContext taskContext;
    public ReplayTaskResultSnapshot result;
}

[Serializable]
public class ReplayMetricSnapshot
{
    public bool leftAvailable;
    public bool rightAvailable;
    public bool leftFallback;
    public bool rightFallback;
    public float leftValgusDeg;
    public float rightValgusDeg;
    public float leftFlexionDeg;
    public float rightFlexionDeg;
    public float pelvisSwayRmsMeters;
    public float swayVelocityMps;
    public float symmetryPct;
    public float leftStanceAnteriorReachPct;
    public float rightStanceAnteriorReachPct;
    public float leftValgusAngularVelDps;
    public float rightValgusAngularVelDps;

    public static ReplayMetricSnapshot FromBiometrics(LowerLimbBiometrics biometrics)
    {
        if (biometrics == null) return null;

        return new ReplayMetricSnapshot
        {
            leftAvailable = biometrics.IsLeftLegAvailable,
            rightAvailable = biometrics.IsRightLegAvailable,
            leftFallback = biometrics.IsUsingLeftAvatarFallback,
            rightFallback = biometrics.IsUsingRightAvatarFallback,
            leftValgusDeg = biometrics.LeftValgusAngle,
            rightValgusDeg = biometrics.RightValgusAngle,
            leftFlexionDeg = biometrics.LeftKneeFlexion,
            rightFlexionDeg = biometrics.RightKneeFlexion,
            pelvisSwayRmsMeters = biometrics.PelvisSwayRMS,
            swayVelocityMps = biometrics.SwayVelocity,
            symmetryPct = biometrics.SymmetryIndex,
            leftStanceAnteriorReachPct = biometrics.LeftStanceAnteriorReachPct,
            rightStanceAnteriorReachPct = biometrics.RightStanceAnteriorReachPct,
            leftValgusAngularVelDps  = biometrics.LeftValgusAngularVelocity,
            rightValgusAngularVelDps = biometrics.RightValgusAngularVelocity
        };
    }
}

[Serializable]
public class ReplayTaskResultSnapshot
{
    public string taskType;
    public string taskNameTR;
    public float measuredDurationSec;
    public float meanValgusLeft;
    public float meanValgusRight;
    public float maxValgusLeft;
    public float maxValgusRight;
    public float meanFlexLeft;
    public float meanFlexRight;
    public float maxFlexLeft;
    public float maxFlexRight;
    public float meanSwayRmsMeters;
    public float meanSwayVelocityMps;
    public float symmetryIndexPct;
    public float maxLeftStanceAnteriorReachPct;
    public float maxRightStanceAnteriorReachPct;
    public float valgusRiskScore;
    public float asymmetryRiskScore;
    public float flexionRiskScore;
    public float balanceRiskScore;
    public float reachRiskScore;
    public float totalRiskScore;
    public float gameScore;
    public string riskGrade;
    public string taskSummaryTR;

    public static ReplayTaskResultSnapshot FromResult(TaskResult result)
    {
        if (result == null) return null;

        return new ReplayTaskResultSnapshot
        {
            taskType = result.TaskType.ToString(),
            taskNameTR = result.TaskNameTR,
            measuredDurationSec = result.MeasuredDurationSec,
            meanValgusLeft = result.MeanValgusLeft,
            meanValgusRight = result.MeanValgusRight,
            maxValgusLeft = result.MaxValgusLeft,
            maxValgusRight = result.MaxValgusRight,
            meanFlexLeft = result.MeanFlexLeft,
            meanFlexRight = result.MeanFlexRight,
            maxFlexLeft = result.MaxFlexLeft,
            maxFlexRight = result.MaxFlexRight,
            meanSwayRmsMeters = result.MeanSwayRMS,
            meanSwayVelocityMps = result.MeanSwayVelocity,
            symmetryIndexPct = result.SymmetryIndex,
            maxLeftStanceAnteriorReachPct = result.MaxLeftStanceAnteriorReachPct,
            maxRightStanceAnteriorReachPct = result.MaxRightStanceAnteriorReachPct,
            valgusRiskScore = result.ValgusRiskScore,
            asymmetryRiskScore = result.AsymmetryRiskScore,
            flexionRiskScore = result.FlexionRiskScore,
            balanceRiskScore = result.BalanceRiskScore,
            reachRiskScore = result.ReachRiskScore,
            totalRiskScore = result.TotalRiskScore,
            gameScore = result.GameScore,
            riskGrade = result.RiskGrade,
            taskSummaryTR = result.TaskSummaryTR
        };
    }
}

[Serializable]
public class ReplayPose
{
    public string id;
    public string objectName;
    public bool valid;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localPosition;
    public Quaternion localRotation;

    public static ReplayPose FromTransform(string id, Transform transform)
    {
        var pose = new ReplayPose
        {
            id = id,
            objectName = transform != null ? transform.name : "",
            valid = transform != null,
            rotation = Quaternion.identity,
            localRotation = Quaternion.identity
        };

        if (transform == null) return pose;

        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.localPosition = transform.localPosition;
        pose.localRotation = transform.localRotation;
        return pose;
    }
}