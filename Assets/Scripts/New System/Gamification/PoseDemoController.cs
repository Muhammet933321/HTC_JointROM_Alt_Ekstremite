using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a "ghost" avatar to demonstrate the correct posture for each task.
///
/// HOW TO RECORD POSES (edit mode, NO Play required):
///   1. Select GhostAvatar_Demo in the Hierarchy.
///   2. In the Inspector → "=== Pose Capture ===" section:
///      a. Pick the TaskType you want to record.
///      b. Rotate the ghost bones to the desired position in the Scene view.
///      c. Click  "► Keyframe Ekle"  — saves the current bone rotations.
///      d. Adjust for the next keyframe and click again.
///   3. Press Play to verify. Captured sequences override the Euler fallback.
/// </summary>
public class PoseDemoController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // BONE REFERENCES
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Ghost Avatar Bone References ===")]
    [Tooltip("Pelvis/root hip bone (mixamorig:Hips).")]
    public Transform hipBone;

    [Tooltip("Lower spine (mixamorig:Spine1 or Spine) — lean counter-rotation.")]
    public Transform spineBone;

    [Tooltip("Left thigh / hip joint (mixamorig:LeftUpLeg).")]
    public Transform leftThighBone;

    [Tooltip("Right thigh / hip joint (mixamorig:RightUpLeg).")]
    public Transform rightThighBone;

    [Tooltip("Left shin / knee joint (mixamorig:LeftLeg).")]
    public Transform leftShinBone;

    [Tooltip("Right shin / knee joint (mixamorig:RightLeg).")]
    public Transform rightShinBone;

    [Tooltip("Left foot / ankle (mixamorig:LeftFoot).")]
    public Transform leftAnkleBone;

    [Tooltip("Right foot / ankle (mixamorig:RightFoot).")]
    public Transform rightAnkleBone;

    // ═══════════════════════════════════════════════════════════════
    // DEPENDENCIES & SETTINGS
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Dependencies ===")]
    public TaskSequencer sequencer;

    [Header("=== Settings ===")]
    public bool playDuringCountdown = true;
    public bool playDuringMeasurement = false;
    public float transitionSpeed = 2f;

    // ═══════════════════════════════════════════════════════════════
    // SNAPSHOT DATA STRUCTURES
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// A single absolute-rotation keyframe. Stored as Vector4 because Unity
    /// can serialise Vector4 but not Quaternion directly in custom classes.
    /// </summary>
    [Serializable]
    public class BoneSnapshot
    {
        [Tooltip("Hold time at this pose (seconds).")]
        public float holdSeconds = 1.5f;

        public Vector4 hip;
        public Vector4 spine;
        public Vector4 thighLeft;
        public Vector4 thighRight;
        public Vector4 shinLeft;
        public Vector4 shinRight;
        public Vector4 ankleLeft;
        public Vector4 ankleRight;

        [Tooltip("Hip bone local position (for squat/lean height offset).")]
        public Vector3 hipPosition;

        public Quaternion HipQ        => ToQ(hip);
        public Quaternion SpineQ      => ToQ(spine);
        public Quaternion ThighLeftQ  => ToQ(thighLeft);
        public Quaternion ThighRightQ => ToQ(thighRight);
        public Quaternion ShinLeftQ   => ToQ(shinLeft);
        public Quaternion ShinRightQ  => ToQ(shinRight);
        public Quaternion AnkleLeftQ  => ToQ(ankleLeft);
        public Quaternion AnkleRightQ => ToQ(ankleRight);

        private static Quaternion ToQ(Vector4 v) => new Quaternion(v.x, v.y, v.z, v.w);
        public  static Vector4    ToV(Quaternion q) => new Vector4(q.x, q.y, q.z, q.w);

        public BoneSnapshot()
        {
            var id = ToV(Quaternion.identity);
            hip = spine = thighLeft = thighRight =
            shinLeft = shinRight = ankleLeft = ankleRight = id;
            hipPosition = Vector3.zero;
        }
    }

    [Serializable]
    public class TaskPoseSequence
    {
        public TaskType taskType;
        public List<BoneSnapshot> keyframes = new List<BoneSnapshot>();
    }

    [Header("=== Captured Pose Sequences ===")]
    [Tooltip("Recorded sequences per task. Use the custom Inspector buttons to populate.")]
    public List<TaskPoseSequence> capturedSequences = new List<TaskPoseSequence>();

    [Header("=== Neutral Pose (edit-mode persistent) ===")]
    [Tooltip("Saved via '⊙ Şu Anki Pozu Nötr Olarak Kaydet' button. Required for 'Nötr Poza Döndür' to work in edit mode.")]
    [SerializeField] public BoneSnapshot neutralSnapshot = new BoneSnapshot();
    [SerializeField] public bool neutralCaptured = false;

    // ═══════════════════════════════════════════════════════════════
    // EULER FALLBACK LIBRARY
    // ═══════════════════════════════════════════════════════════════

    [Serializable]
    public struct DemoPose
    {
        public Vector3 hipLocalEuler;
        public Vector3 spineLocalEuler;
        public Vector3 thighLeftLocalEuler;
        public Vector3 thighRightLocalEuler;
        public Vector3 shinLeftLocalEuler;
        public Vector3 shinRightLocalEuler;
        public Vector3 ankleLeftLocalEuler;
        public Vector3 ankleRightLocalEuler;
        public float holdSeconds;
    }

    private static Dictionary<TaskType, DemoPose[]> _defaultPoses;

    private static Dictionary<TaskType, DemoPose[]> BuildDefaultPoses()
    {
        var n = new DemoPose { holdSeconds = 0.4f };
        return new Dictionary<TaskType, DemoPose[]>
        {
            [TaskType.Standing] = new[] { n },
            [TaskType.LeanRight] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(0,-2,-18), spineLocalEuler=new Vector3(0,2,10),
                    thighRightLocalEuler=new Vector3(8,0,5), shinRightLocalEuler=new Vector3(-12,0,0), holdSeconds=1.8f }, n },
            [TaskType.LeanLeft] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(0,2,18), spineLocalEuler=new Vector3(0,-2,-10),
                    thighLeftLocalEuler=new Vector3(8,0,-5), shinLeftLocalEuler=new Vector3(-12,0,0), holdSeconds=1.8f }, n },
            [TaskType.LeanForward] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(25,0,0), spineLocalEuler=new Vector3(15,0,0),
                    thighLeftLocalEuler=new Vector3(20,0,0), thighRightLocalEuler=new Vector3(20,0,0),
                    shinLeftLocalEuler=new Vector3(-18,0,0), shinRightLocalEuler=new Vector3(-18,0,0),
                    ankleLeftLocalEuler=new Vector3(10,0,0), ankleRightLocalEuler=new Vector3(10,0,0), holdSeconds=1.8f }, n },
            [TaskType.MiniSquat] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(20,0,0), spineLocalEuler=new Vector3(-8,0,0),
                    thighLeftLocalEuler=new Vector3(35,0,0), thighRightLocalEuler=new Vector3(35,0,0),
                    shinLeftLocalEuler=new Vector3(-60,0,0), shinRightLocalEuler=new Vector3(-60,0,0),
                    ankleLeftLocalEuler=new Vector3(15,0,0), ankleRightLocalEuler=new Vector3(15,0,0), holdSeconds=2f }, n },
            [TaskType.SingleLegBalance_R] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(5,0,-5),
                    thighRightLocalEuler=new Vector3(10,0,0), shinRightLocalEuler=new Vector3(-15,0,0),
                    thighLeftLocalEuler=new Vector3(70,20,-10), shinLeftLocalEuler=new Vector3(-90,0,0), holdSeconds=2.5f }, n },
            [TaskType.SingleLegBalance_L] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(5,0,5),
                    thighLeftLocalEuler=new Vector3(10,0,0), shinLeftLocalEuler=new Vector3(-15,0,0),
                    thighRightLocalEuler=new Vector3(70,-20,10), shinRightLocalEuler=new Vector3(-90,0,0), holdSeconds=2.5f }, n },
            [TaskType.WalkSimulation] = new[] { n,
                new DemoPose { hipLocalEuler=new Vector3(5,8,-3),
                    thighRightLocalEuler=new Vector3(30,0,0), shinRightLocalEuler=new Vector3(-15,0,0),
                    thighLeftLocalEuler=new Vector3(-20,0,0), shinLeftLocalEuler=new Vector3(-10,0,0), holdSeconds=0.55f }, n,
                new DemoPose { hipLocalEuler=new Vector3(5,-8,3),
                    thighLeftLocalEuler=new Vector3(30,0,0), shinLeftLocalEuler=new Vector3(-15,0,0),
                    thighRightLocalEuler=new Vector3(-20,0,0), shinRightLocalEuler=new Vector3(-10,0,0), holdSeconds=0.55f }, n },
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // NEUTRAL CAPTURES
    // ═══════════════════════════════════════════════════════════════

    private Quaternion _hipNeutral, _spineNeutral;
    private Quaternion _thighLeftNeutral, _thighRightNeutral;
    private Quaternion _shinLeftNeutral,  _shinRightNeutral;
    private Quaternion _ankleLeftNeutral, _ankleRightNeutral;
    private Vector3    _hipPositionNeutral;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME STATE
    // ═══════════════════════════════════════════════════════════════

    private Coroutine _demoCoroutine;

    // ═══════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _defaultPoses = BuildDefaultPoses();
        CaptureNeutral();
    }

    private void OnEnable()
    {
        if (sequencer == null) return;
        sequencer.OnTaskStarted   += HandleTaskStarted;
        sequencer.OnTaskEnded     += HandleTaskEnded;
        sequencer.OnCountdownTick += HandleCountdownTick;
    }

    private void OnDisable()
    {
        if (sequencer == null) return;
        sequencer.OnTaskStarted   -= HandleTaskStarted;
        sequencer.OnTaskEnded     -= HandleTaskEnded;
        sequencer.OnCountdownTick -= HandleCountdownTick;
    }

    private void Update()
    {
        if (!playDuringMeasurement && sequencer != null
            && sequencer.IsMeasuring && _demoCoroutine != null)
            StopDemo();
    }

    // ═══════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void HandleTaskStarted(TaskDefinition task)
    {
        if (playDuringCountdown || playDuringMeasurement) BeginDemo(task);
    }

    private void HandleTaskEnded(TaskDefinition _) => StopDemo();

    private void HandleCountdownTick(int _)
    {
        if (playDuringCountdown && _demoCoroutine == null
            && sequencer?.CurrentTask != null)
            BeginDemo(sequencer.CurrentTask);
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC DEMO CONTROL
    // ═══════════════════════════════════════════════════════════════

    public void BeginDemo(TaskDefinition task)
    {
        if (_demoCoroutine != null) StopCoroutine(_demoCoroutine);

        var captured = GetCapturedSequence(task.taskType);
        if (captured != null && captured.keyframes.Count > 0)
        {
            _demoCoroutine = StartCoroutine(
                AnimateSnapshots(captured.keyframes, transitionSpeed, task.loopDemo));
            return;
        }

        DemoPose[] poses = null;
        _defaultPoses?.TryGetValue(task.taskType, out poses);
        if (poses == null || poses.Length == 0)
            poses = new[] { new DemoPose { hipLocalEuler = task.demoHipEuler,
                shinLeftLocalEuler = task.demoKneeEuler, shinRightLocalEuler = task.demoKneeEuler,
                holdSeconds = task.demoPauseDuration } };

        _demoCoroutine = StartCoroutine(
            AnimateEulerPoses(poses, task.demoTransitionSpeed, task.loopDemo));
    }

    public void StopDemo()
    {
        if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
        StartCoroutine(ReturnToNeutralCoroutine());
    }

    // ═══════════════════════════════════════════════════════════════
    // COROUTINES — SNAPSHOT PATH
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateSnapshots(List<BoneSnapshot> frames, float speed, bool loop)
    {
        do { foreach (var f in frames) { yield return StartCoroutine(LerpToSnapshot(f, speed)); yield return new WaitForSeconds(f.holdSeconds); } }
        while (loop);
    }

    private IEnumerator LerpToSnapshot(BoneSnapshot target, float speed)
    {
        Quaternion sH=Get(hipBone), sSp=Get(spineBone), sTL=Get(leftThighBone), sTR=Get(rightThighBone);
        Quaternion sSL=Get(leftShinBone), sSR=Get(rightShinBone), sAL=Get(leftAnkleBone), sAR=Get(rightAnkleBone);
        Vector3 sHPos = hipBone ? hipBone.localPosition : Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed; float s = Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
            Set(hipBone, sH, target.HipQ, s);           Set(spineBone, sSp, target.SpineQ, s);
            Set(leftThighBone, sTL, target.ThighLeftQ, s);  Set(rightThighBone, sTR, target.ThighRightQ, s);
            Set(leftShinBone, sSL, target.ShinLeftQ, s);    Set(rightShinBone, sSR, target.ShinRightQ, s);
            Set(leftAnkleBone, sAL, target.AnkleLeftQ, s);  Set(rightAnkleBone, sAR, target.AnkleRightQ, s);
            if (hipBone) hipBone.localPosition = Vector3.Lerp(sHPos, target.hipPosition, s);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // COROUTINES — EULER FALLBACK PATH
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateEulerPoses(DemoPose[] poses, float speed, bool loop)
    {
        do { foreach (var p in poses) { yield return StartCoroutine(LerpToEulerPose(p, speed)); yield return new WaitForSeconds(p.holdSeconds); } }
        while (loop);
    }

    private IEnumerator LerpToEulerPose(DemoPose target, float speed)
    {
        Quaternion sH=Get(hipBone), sSp=Get(spineBone), sTL=Get(leftThighBone), sTR=Get(rightThighBone);
        Quaternion sSL=Get(leftShinBone), sSR=Get(rightShinBone), sAL=Get(leftAnkleBone), sAR=Get(rightAnkleBone);

        Quaternion tH  = _hipNeutral        * Quaternion.Euler(target.hipLocalEuler);
        Quaternion tSp = _spineNeutral      * Quaternion.Euler(target.spineLocalEuler);
        Quaternion tTL = _thighLeftNeutral  * Quaternion.Euler(target.thighLeftLocalEuler);
        Quaternion tTR = _thighRightNeutral * Quaternion.Euler(target.thighRightLocalEuler);
        Quaternion tSL = _shinLeftNeutral   * Quaternion.Euler(target.shinLeftLocalEuler);
        Quaternion tSR = _shinRightNeutral  * Quaternion.Euler(target.shinRightLocalEuler);
        Quaternion tAL = _ankleLeftNeutral  * Quaternion.Euler(target.ankleLeftLocalEuler);
        Quaternion tAR = _ankleRightNeutral * Quaternion.Euler(target.ankleRightLocalEuler);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed; float s = Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
            Set(hipBone,sH,tH,s); Set(spineBone,sSp,tSp,s);
            Set(leftThighBone,sTL,tTL,s); Set(rightThighBone,sTR,tTR,s);
            Set(leftShinBone,sSL,tSL,s);  Set(rightShinBone,sSR,tSR,s);
            Set(leftAnkleBone,sAL,tAL,s); Set(rightAnkleBone,sAR,tAR,s);
            yield return null;
        }
    }

    private IEnumerator ReturnToNeutralCoroutine()
    {
        // Prefer serialized neutralSnapshot (works in edit mode too).
        // Fall back to runtime-captured quaternions if not yet saved.
        BoneSnapshot snap;
        if (neutralCaptured)
        {
            snap = neutralSnapshot;
        }
        else
        {
            snap = new BoneSnapshot {
                hip        = BoneSnapshot.ToV(_hipNeutral),
                spine      = BoneSnapshot.ToV(_spineNeutral),
                thighLeft  = BoneSnapshot.ToV(_thighLeftNeutral),
                thighRight = BoneSnapshot.ToV(_thighRightNeutral),
                shinLeft   = BoneSnapshot.ToV(_shinLeftNeutral),
                shinRight  = BoneSnapshot.ToV(_shinRightNeutral),
                ankleLeft  = BoneSnapshot.ToV(_ankleLeftNeutral),
                ankleRight = BoneSnapshot.ToV(_ankleRightNeutral),
                hipPosition = _hipPositionNeutral,
                holdSeconds = 0f
            };
        }
        yield return StartCoroutine(LerpToSnapshot(snap, transitionSpeed));
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC SNAPSHOT CAPTURE  (called by Editor)
    // ═══════════════════════════════════════════════════════════════

    public BoneSnapshot CaptureCurrentPose(float holdSec = 1.5f) => new BoneSnapshot {
        holdSeconds = holdSec,
        hip         = BoneSnapshot.ToV(hipBone        ? hipBone.localRotation        : Quaternion.identity),
        spine       = BoneSnapshot.ToV(spineBone      ? spineBone.localRotation      : Quaternion.identity),
        thighLeft   = BoneSnapshot.ToV(leftThighBone  ? leftThighBone.localRotation  : Quaternion.identity),
        thighRight  = BoneSnapshot.ToV(rightThighBone ? rightThighBone.localRotation : Quaternion.identity),
        shinLeft    = BoneSnapshot.ToV(leftShinBone   ? leftShinBone.localRotation   : Quaternion.identity),
        shinRight   = BoneSnapshot.ToV(rightShinBone  ? rightShinBone.localRotation  : Quaternion.identity),
        ankleLeft   = BoneSnapshot.ToV(leftAnkleBone  ? leftAnkleBone.localRotation  : Quaternion.identity),
        ankleRight  = BoneSnapshot.ToV(rightAnkleBone ? rightAnkleBone.localRotation : Quaternion.identity),
        hipPosition = hipBone ? hipBone.localPosition : Vector3.zero,
    };

    public void ApplySnapshot(BoneSnapshot s)
    {
        if (hipBone)        { hipBone.localRotation = s.HipQ; hipBone.localPosition = s.hipPosition; }
        if (spineBone)      spineBone.localRotation      = s.SpineQ;
        if (leftThighBone)  leftThighBone.localRotation  = s.ThighLeftQ;
        if (rightThighBone) rightThighBone.localRotation = s.ThighRightQ;
        if (leftShinBone)   leftShinBone.localRotation   = s.ShinLeftQ;
        if (rightShinBone)  rightShinBone.localRotation  = s.ShinRightQ;
        if (leftAnkleBone)  leftAnkleBone.localRotation  = s.AnkleLeftQ;
        if (rightAnkleBone) rightAnkleBone.localRotation = s.AnkleRightQ;
    }

    public void ApplyNeutral()
    {
        if (neutralCaptured)
        {
            ApplySnapshot(neutralSnapshot);
            return;
        }
        // Runtime fallback (only valid during Play — Awake must have run)
        if (hipBone)        { hipBone.localRotation = _hipNeutral; hipBone.localPosition = _hipPositionNeutral; }
        if (spineBone)      spineBone.localRotation      = _spineNeutral;
        if (leftThighBone)  leftThighBone.localRotation  = _thighLeftNeutral;
        if (rightThighBone) rightThighBone.localRotation = _thighRightNeutral;
        if (leftShinBone)   leftShinBone.localRotation   = _shinLeftNeutral;
        if (rightShinBone)  rightShinBone.localRotation  = _shinRightNeutral;
        if (leftAnkleBone)  leftAnkleBone.localRotation  = _ankleLeftNeutral;
        if (rightAnkleBone) rightAnkleBone.localRotation = _ankleRightNeutral;
    }

    public TaskPoseSequence GetCapturedSequence(TaskType t)
    {
        foreach (var s in capturedSequences) if (s.taskType == t) return s;
        return null;
    }

    public TaskPoseSequence GetOrCreateSequence(TaskType t)
    {
        var s = GetCapturedSequence(t);
        if (s == null) { s = new TaskPoseSequence { taskType = t }; capturedSequences.Add(s); }
        return s;
    }

    // ═══════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static Quaternion Get(Transform bone) =>
        bone ? bone.localRotation : Quaternion.identity;

    private static void Set(Transform bone, Quaternion from, Quaternion to, float t)
    {
        if (bone) bone.localRotation = Quaternion.Slerp(from, to, t);
    }

    private void CaptureNeutral()
    {
        _hipNeutral         = Get(hipBone);
        _spineNeutral       = Get(spineBone);
        _thighLeftNeutral   = Get(leftThighBone);
        _thighRightNeutral  = Get(rightThighBone);
        _shinLeftNeutral    = Get(leftShinBone);
        _shinRightNeutral   = Get(rightShinBone);
        _ankleLeftNeutral   = Get(leftAnkleBone);
        _hipPositionNeutral = hipBone ? hipBone.localPosition : Vector3.zero;
        // Also refresh the serialized neutralSnapshot so Euler fallback stays consistent.
        if (!neutralCaptured) neutralSnapshot = CaptureCurrentPose(0f);
        _ankleRightNeutral  = Get(rightAnkleBone);
    }
}
