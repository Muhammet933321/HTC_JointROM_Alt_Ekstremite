using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghost avatar'ı her görev için doğru pozu gösterecek şekilde animasyonlar.
/// ScriptableObject tabanlı: TaskDefinition.demoSequence → PoseSequenceSO → PoseSnapshotSO.
///
/// POZ KAYDETME ADIMLARI (edit modda, Play gerekmez):
///   1. Ghost avatar kemiklerini dik duruşa al.
///   2. Inspector → "⊙ Nötr Poz Kaydet" → onay ver. (Bir kerelik)
///   3. Kemikleri istediğin göreve uygun pozisyona getir.
///   4. Inspector → Hedef PoseSequenceSO'yu seç → "► Keyframe Ekle".
///      Yeni bir PoseSnapshotSO asset'i Assets/Gamification/Poses/Snapshots/ klasöründe oluşturulur.
///   5. Birden fazla keyframe için 3-4'ü tekrarla.
///   6. PoseSequenceSO'yu TaskDefinition.demoSequence alanına ata.
///   7. Play modunda TaskSequencer ilgili görevi başlatınca demo otomatik çalışır.
///
/// Öncelik sırası:
///   1. task.demoSequence (PoseSequenceSO) — varsa ve en az 1 geçerli keyframe içeriyorsa
///   2. Dahili DemoPose Euler fallback library — SO yoksa otomatik devreye girer
/// </summary>
public class PoseDemoController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    // BONE REFERENCES
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Ghost Avatar Bone References ===")]
    [Tooltip("Hips kemiği (mixamorig:Hips).")]
    public Transform hipBone;

    [Tooltip("İlk omurga segmenti (mixamorig:Spine).")]
    public Transform lowerSpineBone;

    [Tooltip("İkinci omurga segmenti (mixamorig:Spine1) — eğilme karşı-rotasyonu için.")]
    public Transform spineBone;

    [Tooltip("Üçüncü omurga segmenti / chest (mixamorig:Spine2).")]
    public Transform chestBone;

    [Tooltip("Boyun (mixamorig:Neck).")]
    public Transform neckBone;

    [Tooltip("Baş (mixamorig:Head).")]
    public Transform headBone;

    [Tooltip("Sol clavicle / shoulder root (mixamorig:LeftShoulder).")]
    public Transform leftShoulderBone;

    [Tooltip("Sağ clavicle / shoulder root (mixamorig:RightShoulder).")]
    public Transform rightShoulderBone;

    [Tooltip("Sol üst kol (mixamorig:LeftArm).")]
    public Transform leftUpperArmBone;

    [Tooltip("Sağ üst kol (mixamorig:RightArm).")]
    public Transform rightUpperArmBone;

    [Tooltip("Sol ön kol (mixamorig:LeftForeArm).")]
    public Transform leftForearmBone;

    [Tooltip("Sağ ön kol (mixamorig:RightForeArm).")]
    public Transform rightForearmBone;

    [Tooltip("Sol el (mixamorig:LeftHand).")]
    public Transform leftHandBone;

    [Tooltip("Sağ el (mixamorig:RightHand).")]
    public Transform rightHandBone;

    [Tooltip("Sol uyluk (mixamorig:LeftUpLeg).")]
    public Transform leftThighBone;

    [Tooltip("Sağ uyluk (mixamorig:RightUpLeg).")]
    public Transform rightThighBone;

    [Tooltip("Sol bacak / diz (mixamorig:LeftLeg).")]
    public Transform leftShinBone;

    [Tooltip("Sağ bacak / diz (mixamorig:RightLeg).")]
    public Transform rightShinBone;

    [Tooltip("Sol ayak (mixamorig:LeftFoot).")]
    public Transform leftAnkleBone;

    [Tooltip("Sağ ayak (mixamorig:RightFoot).")]
    public Transform rightAnkleBone;

    // ═══════════════════════════════════════════════════════════════
    // DEPENDENCIES & SETTINGS
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Dependencies ===")]
    public TaskSequencer sequencer;

    [Header("=== Settings ===")]
    [Tooltip("Geri sayım sırasında demo göster.")]
    public bool playDuringCountdown = true;

    [Tooltip("Ölçüm sırasında demo göstermeye devam et.")]
    public bool playDuringMeasurement = false;

    [Tooltip("Keyframeler arası varsayılan geçiş hızı (PoseSequenceSO.transitionSpeedOverride > 0 ise o önceliklidir).")]
    public float transitionSpeed = 2f;

    // ═══════════════════════════════════════════════════════════════
    // NEUTRAL POSE  (ScriptableObject — edit modda kalıcı)
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Neutral Pose ===")]
    [Tooltip("Ghost avatar nötr / dik duruş pozu.\n" +
             "Inspector'daki '⊙ Nötr Poz Kaydet' butonu bu SO'yu oluşturur / günceller.\n" +
             "Boş bırakılırsa Awake'te runtime'da yakalanan rotasyonlar kullanılır.")]
    public PoseSnapshotSO neutralPose;

    // ═══════════════════════════════════════════════════════════════
    // EULER FALLBACK LIBRARY  (SO ataması yoksa devreye girer)
    // ═══════════════════════════════════════════════════════════════

    [System.Serializable]
    public struct DemoPose
    {
        public Vector3 hipLocalPositionOffset;
        public Vector3 hipLocalEuler;
        public Vector3 lowerSpineLocalEuler;
        public Vector3 spineLocalEuler;
        public Vector3 chestLocalEuler;
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
            [TaskType.LandingScreen] = new[] { n,
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.05f,0.02f),
                    hipLocalEuler=new Vector3(15,0,0), lowerSpineLocalEuler=new Vector3(-5,0,0),
                    spineLocalEuler=new Vector3(-6,0,0), chestLocalEuler=new Vector3(8,0,0),
                    thighLeftLocalEuler=new Vector3(20,0,0), thighRightLocalEuler=new Vector3(20,0,0),
                    shinLeftLocalEuler=new Vector3(-20,0,0), shinRightLocalEuler=new Vector3(-20,0,0),
                    ankleLeftLocalEuler=new Vector3(8,0,0), ankleRightLocalEuler=new Vector3(8,0,0), holdSeconds=0.5f },
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.13f,0.04f),
                    hipLocalEuler=new Vector3(25,0,0), lowerSpineLocalEuler=new Vector3(-8,0,0),
                    spineLocalEuler=new Vector3(-10,0,0), chestLocalEuler=new Vector3(12,0,0),
                    thighLeftLocalEuler=new Vector3(40,0,0), thighRightLocalEuler=new Vector3(40,0,0),
                    shinLeftLocalEuler=new Vector3(-55,0,0), shinRightLocalEuler=new Vector3(-55,0,0),
                    ankleLeftLocalEuler=new Vector3(18,0,0), ankleRightLocalEuler=new Vector3(18,0,0), holdSeconds=1.2f },
                n },
            [TaskType.ModifiedYBalanceAnterior_R] = new[] { n,
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.06f,0.04f),
                    hipLocalEuler=new Vector3(12,0,-6), lowerSpineLocalEuler=new Vector3(0,0,3),
                    spineLocalEuler=new Vector3(4,0,5), chestLocalEuler=new Vector3(6,0,4),
                    thighRightLocalEuler=new Vector3(18,0,0), shinRightLocalEuler=new Vector3(-20,0,0), ankleRightLocalEuler=new Vector3(8,0,0),
                    thighLeftLocalEuler=new Vector3(60,0,0), shinLeftLocalEuler=new Vector3(-20,0,0), ankleLeftLocalEuler=new Vector3(8,0,0), holdSeconds=1.8f },
                n },
            [TaskType.ModifiedYBalanceAnterior_L] = new[] { n,
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.06f,0.04f),
                    hipLocalEuler=new Vector3(12,0,6), lowerSpineLocalEuler=new Vector3(0,0,-3),
                    spineLocalEuler=new Vector3(4,0,-5), chestLocalEuler=new Vector3(6,0,-4),
                    thighLeftLocalEuler=new Vector3(18,0,0), shinLeftLocalEuler=new Vector3(-20,0,0), ankleLeftLocalEuler=new Vector3(8,0,0),
                    thighRightLocalEuler=new Vector3(60,0,0), shinRightLocalEuler=new Vector3(-20,0,0), ankleRightLocalEuler=new Vector3(8,0,0), holdSeconds=1.8f },
                n },
            [TaskType.SingleLegSquat_R] = new[] { n,
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.11f,0.03f),
                    hipLocalEuler=new Vector3(18,0,-5), lowerSpineLocalEuler=new Vector3(-4,0,2),
                    spineLocalEuler=new Vector3(-6,0,4), chestLocalEuler=new Vector3(8,0,3),
                    thighRightLocalEuler=new Vector3(45,0,0), shinRightLocalEuler=new Vector3(-58,0,0), ankleRightLocalEuler=new Vector3(16,0,0),
                    thighLeftLocalEuler=new Vector3(18,0,0), shinLeftLocalEuler=new Vector3(-10,0,0), holdSeconds=1.8f },
                n },
            [TaskType.SingleLegSquat_L] = new[] { n,
                new DemoPose {
                    hipLocalPositionOffset=new Vector3(0f,-0.11f,0.03f),
                    hipLocalEuler=new Vector3(18,0,5), lowerSpineLocalEuler=new Vector3(-4,0,-2),
                    spineLocalEuler=new Vector3(-6,0,-4), chestLocalEuler=new Vector3(8,0,-3),
                    thighLeftLocalEuler=new Vector3(45,0,0), shinLeftLocalEuler=new Vector3(-58,0,0), ankleLeftLocalEuler=new Vector3(16,0,0),
                    thighRightLocalEuler=new Vector3(18,0,0), shinRightLocalEuler=new Vector3(-10,0,0), holdSeconds=1.8f },
                n },
        };
    }

    private static void EnsureDefaultPoseLibrary()
    {
        if (_defaultPoses == null)
            _defaultPoses = BuildDefaultPoses();
    }

    public static bool TryGetDefaultDemoPoses(TaskType taskType, out DemoPose[] poses)
    {
        EnsureDefaultPoseLibrary();
        return _defaultPoses.TryGetValue(taskType, out poses) && poses != null && poses.Length > 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // NEUTRAL RUNTIME CACHE  (Awake'te yakalanır — SO yoksa fallback)
    // ═══════════════════════════════════════════════════════════════

    private Quaternion _hipNeutral, _lowerSpineNeutral, _spineNeutral;
    private Quaternion _chestNeutral, _neckNeutral, _headNeutral;
    private Quaternion _leftShoulderNeutral, _rightShoulderNeutral;
    private Quaternion _leftUpperArmNeutral, _rightUpperArmNeutral;
    private Quaternion _leftForearmNeutral, _rightForearmNeutral;
    private Quaternion _leftHandNeutral, _rightHandNeutral;
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
        EnsureDefaultPoseLibrary();
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

        // Priority 1: PoseSequenceSO (ScriptableObject path)
        var seq = task.demoSequence;
        if (seq != null && seq.ValidCount() > 0)
        {
            float speed = seq.transitionSpeedOverride > 0f
                ? seq.transitionSpeedOverride
                : (task.demoTransitionSpeedOverride > 0f ? task.demoTransitionSpeedOverride : transitionSpeed);
            _demoCoroutine = StartCoroutine(AnimateSequence(seq, speed));
            return;
        }

        // Priority 2: Built-in Euler fallback
        DemoPose[] poses = null;
        TryGetDefaultDemoPoses(task.taskType, out poses);
        if (poses == null || poses.Length == 0)
            poses = new[] { new DemoPose { holdSeconds = 2f } };

        float eulerSpeed = task.demoTransitionSpeedOverride > 0f ? task.demoTransitionSpeedOverride : transitionSpeed;
        _demoCoroutine = StartCoroutine(AnimateEulerPoses(poses, eulerSpeed, loop: true));
    }

    public void StopDemo()
    {
        if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
        StartCoroutine(ReturnToNeutralCoroutine());
    }

    // ═══════════════════════════════════════════════════════════════
    // COROUTINES — SO PATH
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateSequence(PoseSequenceSO seq, float speed)
    {
        do
        {
            foreach (var snap in seq.keyframes)
            {
                if (snap == null) continue;
                yield return StartCoroutine(LerpToSnapshotSO(snap, speed));
                yield return new WaitForSeconds(snap.holdSeconds);
            }
        }
        while (seq.loop);
    }

    private IEnumerator LerpToSnapshotSO(PoseSnapshotSO target, float speed)
    {
        Quaternion sH = GetRot(hipBone), sLowerSp = GetRot(lowerSpineBone), sSp = GetRot(spineBone), sChest = GetRot(chestBone);
        Quaternion sNeck = GetRot(neckBone), sHead = GetRot(headBone);
        Quaternion sLS = GetRot(leftShoulderBone), sRS = GetRot(rightShoulderBone);
        Quaternion sLUA = GetRot(leftUpperArmBone), sRUA = GetRot(rightUpperArmBone);
        Quaternion sLF = GetRot(leftForearmBone), sRF = GetRot(rightForearmBone);
        Quaternion sLH = GetRot(leftHandBone), sRH = GetRot(rightHandBone);
        Quaternion sTL = GetRot(leftThighBone), sTR = GetRot(rightThighBone);
        Quaternion sSL = GetRot(leftShinBone), sSR = GetRot(rightShinBone);
        Quaternion sAL = GetRot(leftAnkleBone), sAR = GetRot(rightAnkleBone);
        Vector3 sHP = hipBone ? hipBone.localPosition : Vector3.zero;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            SetRot(hipBone,        sH,  target.HipQ,        s);
            SetOptionalRot(lowerSpineBone, target.hasLowerSpine, sLowerSp, target.LowerSpineQ, s);
            SetRot(spineBone,      sSp, target.SpineQ,      s);
            SetOptionalRot(chestBone, target.hasChest, sChest, target.ChestQ, s);
            SetOptionalRot(neckBone, target.hasNeck, sNeck, target.NeckQ, s);
            SetOptionalRot(headBone, target.hasHead, sHead, target.HeadQ, s);
            SetOptionalRot(leftShoulderBone, target.hasLeftShoulder, sLS, target.ShoulderLeftQ, s);
            SetOptionalRot(rightShoulderBone, target.hasRightShoulder, sRS, target.ShoulderRightQ, s);
            SetOptionalRot(leftUpperArmBone, target.hasLeftUpperArm, sLUA, target.UpperArmLeftQ, s);
            SetOptionalRot(rightUpperArmBone, target.hasRightUpperArm, sRUA, target.UpperArmRightQ, s);
            SetOptionalRot(leftForearmBone, target.hasLeftForearm, sLF, target.ForearmLeftQ, s);
            SetOptionalRot(rightForearmBone, target.hasRightForearm, sRF, target.ForearmRightQ, s);
            SetOptionalRot(leftHandBone, target.hasLeftHand, sLH, target.HandLeftQ, s);
            SetOptionalRot(rightHandBone, target.hasRightHand, sRH, target.HandRightQ, s);
            SetRot(leftThighBone,  sTL, target.ThighLeftQ,  s);
            SetRot(rightThighBone, sTR, target.ThighRightQ, s);
            SetRot(leftShinBone,   sSL, target.ShinLeftQ,   s);
            SetRot(rightShinBone,  sSR, target.ShinRightQ,  s);
            SetRot(leftAnkleBone,  sAL, target.AnkleLeftQ,  s);
            SetRot(rightAnkleBone, sAR, target.AnkleRightQ, s);
            if (hipBone) hipBone.localPosition = Vector3.Lerp(sHP, target.hipPosition, s);

            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // COROUTINES — EULER FALLBACK PATH
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateEulerPoses(DemoPose[] poses, float speed, bool loop)
    {
        do
        {
            foreach (var p in poses)
            {
                yield return StartCoroutine(LerpToEulerPose(p, speed));
                yield return new WaitForSeconds(p.holdSeconds);
            }
        }
        while (loop);
    }

    private IEnumerator LerpToEulerPose(DemoPose target, float speed)
    {
        Quaternion sH  = GetRot(hipBone),        sLowerSp = GetRot(lowerSpineBone), sSp = GetRot(spineBone), sChest = GetRot(chestBone);
        Quaternion sTL = GetRot(leftThighBone),  sTR = GetRot(rightThighBone);
        Quaternion sSL = GetRot(leftShinBone),   sSR = GetRot(rightShinBone);
        Quaternion sAL = GetRot(leftAnkleBone),  sAR = GetRot(rightAnkleBone);
        Vector3 sHP = hipBone ? hipBone.localPosition : Vector3.zero;

        Quaternion tH   = _hipNeutral        * Quaternion.Euler(target.hipLocalEuler);
        Quaternion tLowerSp = _lowerSpineNeutral * Quaternion.Euler(target.lowerSpineLocalEuler);
        Quaternion tSp  = _spineNeutral      * Quaternion.Euler(target.spineLocalEuler);
        Quaternion tChest = _chestNeutral    * Quaternion.Euler(target.chestLocalEuler);
        Quaternion tTL  = _thighLeftNeutral  * Quaternion.Euler(target.thighLeftLocalEuler);
        Quaternion tTR  = _thighRightNeutral * Quaternion.Euler(target.thighRightLocalEuler);
        Quaternion tSL  = _shinLeftNeutral   * Quaternion.Euler(target.shinLeftLocalEuler);
        Quaternion tSR  = _shinRightNeutral  * Quaternion.Euler(target.shinRightLocalEuler);
        Quaternion tAL  = _ankleLeftNeutral  * Quaternion.Euler(target.ankleLeftLocalEuler);
        Quaternion tAR  = _ankleRightNeutral * Quaternion.Euler(target.ankleRightLocalEuler);
        Vector3 tHP = _hipPositionNeutral + target.hipLocalPositionOffset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            SetRot(hipBone,        sH,  tH,  s);
            SetRot(lowerSpineBone, sLowerSp, tLowerSp, s);
            SetRot(spineBone,      sSp, tSp, s);
            SetRot(chestBone,      sChest, tChest, s);
            SetRot(leftThighBone,  sTL, tTL, s);
            SetRot(rightThighBone, sTR, tTR, s);
            SetRot(leftShinBone,   sSL, tSL, s);
            SetRot(rightShinBone,  sSR, tSR, s);
            SetRot(leftAnkleBone,  sAL, tAL, s);
            SetRot(rightAnkleBone, sAR, tAR, s);
            if (hipBone) hipBone.localPosition = Vector3.Lerp(sHP, tHP, s);

            yield return null;
        }
    }

    private IEnumerator ReturnToNeutralCoroutine()
    {
        PoseSnapshotSO target = BuildNeutralSnapshot();
        yield return StartCoroutine(LerpToSnapshotSO(target, transitionSpeed));
        DestroyTempSnapshot(target);
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API  (Editor + runtime)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Mevcut kemik transformlarını verilen PoseSnapshotSO'ya yazar.
    /// Editor scripti bunu çağırır, ardından AssetDatabase.SaveAssets() çağırmalı.
    /// </summary>
    public void CaptureIntoPoseSnapshotSO(PoseSnapshotSO target)
    {
        target.CaptureFrom(
            hipBone, lowerSpineBone, spineBone,
            chestBone, neckBone, headBone,
            leftShoulderBone, rightShoulderBone,
            leftUpperArmBone, rightUpperArmBone,
            leftForearmBone, rightForearmBone,
            leftHandBone, rightHandBone,
            leftThighBone, rightThighBone,
            leftShinBone,  rightShinBone,
            leftAnkleBone, rightAnkleBone);
    }

    public void CaptureNeutralFromCurrentBones()
    {
        CaptureNeutral();
    }

    public void ApplyDemoPoseImmediate(DemoPose pose)
    {
        if (hipBone)
        {
            hipBone.localRotation = _hipNeutral * Quaternion.Euler(pose.hipLocalEuler);
            hipBone.localPosition = _hipPositionNeutral + pose.hipLocalPositionOffset;
        }
        if (lowerSpineBone) lowerSpineBone.localRotation = _lowerSpineNeutral * Quaternion.Euler(pose.lowerSpineLocalEuler);
        if (spineBone) spineBone.localRotation = _spineNeutral * Quaternion.Euler(pose.spineLocalEuler);
        if (chestBone) chestBone.localRotation = _chestNeutral * Quaternion.Euler(pose.chestLocalEuler);
        if (leftThighBone) leftThighBone.localRotation = _thighLeftNeutral * Quaternion.Euler(pose.thighLeftLocalEuler);
        if (rightThighBone) rightThighBone.localRotation = _thighRightNeutral * Quaternion.Euler(pose.thighRightLocalEuler);
        if (leftShinBone) leftShinBone.localRotation = _shinLeftNeutral * Quaternion.Euler(pose.shinLeftLocalEuler);
        if (rightShinBone) rightShinBone.localRotation = _shinRightNeutral * Quaternion.Euler(pose.shinRightLocalEuler);
        if (leftAnkleBone) leftAnkleBone.localRotation = _ankleLeftNeutral * Quaternion.Euler(pose.ankleLeftLocalEuler);
        if (rightAnkleBone) rightAnkleBone.localRotation = _ankleRightNeutral * Quaternion.Euler(pose.ankleRightLocalEuler);
    }

    /// <summary>Verilen PoseSnapshotSO'yu kemiklere anında uygular (Lerp yok — editor preview için).</summary>
    public void ApplySnapshotSO(PoseSnapshotSO snap)
    {
        if (snap == null) return;
        snap.ApplyTo(
            hipBone, lowerSpineBone, spineBone,
            chestBone, neckBone, headBone,
            leftShoulderBone, rightShoulderBone,
            leftUpperArmBone, rightUpperArmBone,
            leftForearmBone, rightForearmBone,
            leftHandBone, rightHandBone,
            leftThighBone, rightThighBone,
            leftShinBone,  rightShinBone,
            leftAnkleBone, rightAnkleBone);
    }

    /// <summary>Nötr pozu kemiklere uygular.</summary>
    public void ApplyNeutral()
    {
        PoseSnapshotSO target = BuildNeutralSnapshot();
        ApplySnapshotSO(target);
        DestroyTempSnapshot(target);
    }

    // ═══════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static Quaternion GetRot(Transform b) =>
        b ? b.localRotation : Quaternion.identity;

    private static void SetRot(Transform b, Quaternion from, Quaternion to, float t)
    {
        if (b) b.localRotation = Quaternion.Slerp(from, to, t);
    }

    private static void SetOptionalRot(Transform b, bool hasTarget, Quaternion from, Quaternion to, float t)
    {
        if (b != null && hasTarget)
            b.localRotation = Quaternion.Slerp(from, to, t);
    }

    private PoseSnapshotSO BuildNeutralSnapshot()
    {
        var snapshot = ScriptableObject.CreateInstance<PoseSnapshotSO>();

        if (neutralPose != null)
            CopySnapshot(neutralPose, snapshot);
        else
            FillRuntimeNeutral(snapshot);

        FillMissingOptionalNeutralBones(snapshot);
        snapshot.holdSeconds = 0f;
        return snapshot;
    }

    private void FillRuntimeNeutral(PoseSnapshotSO snapshot)
    {
        snapshot.hip = PoseSnapshotSO.ToV(_hipNeutral);
        snapshot.hasLowerSpine = lowerSpineBone != null;
        snapshot.lowerSpine = PoseSnapshotSO.ToV(_lowerSpineNeutral);
        snapshot.spine = PoseSnapshotSO.ToV(_spineNeutral);
        snapshot.thighLeft = PoseSnapshotSO.ToV(_thighLeftNeutral);
        snapshot.thighRight = PoseSnapshotSO.ToV(_thighRightNeutral);
        snapshot.shinLeft = PoseSnapshotSO.ToV(_shinLeftNeutral);
        snapshot.shinRight = PoseSnapshotSO.ToV(_shinRightNeutral);
        snapshot.ankleLeft = PoseSnapshotSO.ToV(_ankleLeftNeutral);
        snapshot.ankleRight = PoseSnapshotSO.ToV(_ankleRightNeutral);
        snapshot.hipPosition = _hipPositionNeutral;
    }

    private void FillMissingOptionalNeutralBones(PoseSnapshotSO snapshot)
    {
        if (!Application.isPlaying)
            return;

        if (!snapshot.hasLowerSpine && lowerSpineBone != null)
        {
            snapshot.hasLowerSpine = true;
            snapshot.lowerSpine = PoseSnapshotSO.ToV(_lowerSpineNeutral);
        }
        if (!snapshot.hasChest && chestBone != null)
        {
            snapshot.hasChest = true;
            snapshot.chest = PoseSnapshotSO.ToV(_chestNeutral);
        }
        if (!snapshot.hasNeck && neckBone != null)
        {
            snapshot.hasNeck = true;
            snapshot.neck = PoseSnapshotSO.ToV(_neckNeutral);
        }
        if (!snapshot.hasHead && headBone != null)
        {
            snapshot.hasHead = true;
            snapshot.head = PoseSnapshotSO.ToV(_headNeutral);
        }
        if (!snapshot.hasLeftShoulder && leftShoulderBone != null)
        {
            snapshot.hasLeftShoulder = true;
            snapshot.shoulderLeft = PoseSnapshotSO.ToV(_leftShoulderNeutral);
        }
        if (!snapshot.hasRightShoulder && rightShoulderBone != null)
        {
            snapshot.hasRightShoulder = true;
            snapshot.shoulderRight = PoseSnapshotSO.ToV(_rightShoulderNeutral);
        }
        if (!snapshot.hasLeftUpperArm && leftUpperArmBone != null)
        {
            snapshot.hasLeftUpperArm = true;
            snapshot.upperArmLeft = PoseSnapshotSO.ToV(_leftUpperArmNeutral);
        }
        if (!snapshot.hasRightUpperArm && rightUpperArmBone != null)
        {
            snapshot.hasRightUpperArm = true;
            snapshot.upperArmRight = PoseSnapshotSO.ToV(_rightUpperArmNeutral);
        }
        if (!snapshot.hasLeftForearm && leftForearmBone != null)
        {
            snapshot.hasLeftForearm = true;
            snapshot.forearmLeft = PoseSnapshotSO.ToV(_leftForearmNeutral);
        }
        if (!snapshot.hasRightForearm && rightForearmBone != null)
        {
            snapshot.hasRightForearm = true;
            snapshot.forearmRight = PoseSnapshotSO.ToV(_rightForearmNeutral);
        }
        if (!snapshot.hasLeftHand && leftHandBone != null)
        {
            snapshot.hasLeftHand = true;
            snapshot.handLeft = PoseSnapshotSO.ToV(_leftHandNeutral);
        }
        if (!snapshot.hasRightHand && rightHandBone != null)
        {
            snapshot.hasRightHand = true;
            snapshot.handRight = PoseSnapshotSO.ToV(_rightHandNeutral);
        }
    }

    private static void CopySnapshot(PoseSnapshotSO source, PoseSnapshotSO target)
    {
        target.poseName = source.poseName;
        target.descriptionTR = source.descriptionTR;
        target.holdSeconds = source.holdSeconds;
        target.hipPosition = source.hipPosition;
        target.hip = source.hip;
        target.hasLowerSpine = source.hasLowerSpine;
        target.lowerSpine = source.lowerSpine;
        target.spine = source.spine;
        target.hasChest = source.hasChest;
        target.chest = source.chest;
        target.hasNeck = source.hasNeck;
        target.neck = source.neck;
        target.hasHead = source.hasHead;
        target.head = source.head;
        target.thighLeft = source.thighLeft;
        target.thighRight = source.thighRight;
        target.shinLeft = source.shinLeft;
        target.shinRight = source.shinRight;
        target.ankleLeft = source.ankleLeft;
        target.ankleRight = source.ankleRight;
        target.hasLeftShoulder = source.hasLeftShoulder;
        target.shoulderLeft = source.shoulderLeft;
        target.hasRightShoulder = source.hasRightShoulder;
        target.shoulderRight = source.shoulderRight;
        target.hasLeftUpperArm = source.hasLeftUpperArm;
        target.upperArmLeft = source.upperArmLeft;
        target.hasRightUpperArm = source.hasRightUpperArm;
        target.upperArmRight = source.upperArmRight;
        target.hasLeftForearm = source.hasLeftForearm;
        target.forearmLeft = source.forearmLeft;
        target.hasRightForearm = source.hasRightForearm;
        target.forearmRight = source.forearmRight;
        target.hasLeftHand = source.hasLeftHand;
        target.handLeft = source.handLeft;
        target.hasRightHand = source.hasRightHand;
        target.handRight = source.handRight;
    }

    private static void DestroyTempSnapshot(PoseSnapshotSO snapshot)
    {
        if (snapshot == null) return;

        if (Application.isPlaying)
            Destroy(snapshot);
        else
            DestroyImmediate(snapshot);
    }

    private void CaptureNeutral()
    {
        _hipNeutral         = GetRot(hipBone);
        _lowerSpineNeutral  = GetRot(lowerSpineBone);
        _spineNeutral       = GetRot(spineBone);
        _chestNeutral       = GetRot(chestBone);
        _neckNeutral        = GetRot(neckBone);
        _headNeutral        = GetRot(headBone);
        _leftShoulderNeutral = GetRot(leftShoulderBone);
        _rightShoulderNeutral = GetRot(rightShoulderBone);
        _leftUpperArmNeutral = GetRot(leftUpperArmBone);
        _rightUpperArmNeutral = GetRot(rightUpperArmBone);
        _leftForearmNeutral = GetRot(leftForearmBone);
        _rightForearmNeutral = GetRot(rightForearmBone);
        _leftHandNeutral = GetRot(leftHandBone);
        _rightHandNeutral = GetRot(rightHandBone);
        _thighLeftNeutral   = GetRot(leftThighBone);
        _thighRightNeutral  = GetRot(rightThighBone);
        _shinLeftNeutral    = GetRot(leftShinBone);
        _shinRightNeutral   = GetRot(rightShinBone);
        _ankleLeftNeutral   = GetRot(leftAnkleBone);
        _ankleRightNeutral  = GetRot(rightAnkleBone);
        _hipPositionNeutral = hipBone ? hipBone.localPosition : Vector3.zero;

        // neutralPose yoksa Awake'te runtime cache yeterli;
        // varsa SO zaten edit modda kaydedilmiş demektir.
    }
}
