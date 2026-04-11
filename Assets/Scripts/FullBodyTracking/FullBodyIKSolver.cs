using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Full-body IK solver that drives a Mixamo humanoid avatar using:
/// - 1 HMD (head)
/// - 2 Controllers (hands)
/// - 1 Pelvis tracker (waist/chest)
/// - 2 Shin trackers (kaval kemiğine monte — diz ile ayak bileği arası)
/// - (Opsiyonel) 2 Üst bacak tracker (femura monte — doğrudan FK modu)
///
/// Shin-mounted tracker modu (varsayılan):
///   Ayak bileği pozisyonu ve diz hint'i shin tracker'dan hesaplanır.
///   Tracker rotasyonu sayesinde diz bükülme düzlemi çok stabil olur.
///
/// 5-tracker modu (shin + thigh per bacak):
///   Hem üst hem alt bacak rotasyonları doğrudan tracker'dan okunur (FK).
///   IK kullanılmaz — en hassas mod.
///
/// Uses custom TwoBoneIKSolver for arms and legs (when not in FK mode).
/// Drives Hips (NOT Spine) as the pelvis bone to fix the known hierarchy bug.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FullBodyIKSolver : MonoBehaviour
{
    // ───────────────────────── Bone References ─────────────────────────
    [Header("=== Kemik Referansları (Mixamo Humanoid) ===")]

    [Tooltip("mixamorig1:Hips — Ana pelvis kemiği. Spine DEĞİL!")]
    [SerializeField] private Transform hipsBone;

    [Header("Omurga Zinciri")]
    [SerializeField] private Transform spineBone;   // mixamorig1:Spine
    [SerializeField] private Transform spine1Bone;  // mixamorig1:Spine1
    [SerializeField] private Transform spine2Bone;  // mixamorig1:Spine2
    [SerializeField] private Transform neckBone;    // mixamorig1:Neck
    [SerializeField] private Transform headBone;    // mixamorig1:Head

    [Header("Sol Kol")]
    [SerializeField] private Transform leftShoulderBone;  // mixamorig1:LeftShoulder
    [SerializeField] private Transform leftUpperArmBone;  // mixamorig1:LeftArm
    [SerializeField] private Transform leftForeArmBone;   // mixamorig1:LeftForeArm
    [SerializeField] private Transform leftHandBone;      // mixamorig1:LeftHand

    [Header("Sağ Kol")]
    [SerializeField] private Transform rightShoulderBone; // mixamorig1:RightShoulder
    [SerializeField] private Transform rightUpperArmBone; // mixamorig1:RightArm
    [SerializeField] private Transform rightForeArmBone;  // mixamorig1:RightForeArm
    [SerializeField] private Transform rightHandBone;     // mixamorig1:RightHand

    [Header("Sol Bacak")]
    [SerializeField] private Transform leftUpLegBone;  // mixamorig1:LeftUpLeg
    [SerializeField] private Transform leftLegBone;    // mixamorig1:LeftLeg
    [SerializeField] private Transform leftFootBone;   // mixamorig1:LeftFoot

    [Header("Sağ Bacak")]
    [SerializeField] private Transform rightUpLegBone; // mixamorig1:RightUpLeg
    [SerializeField] private Transform rightLegBone;   // mixamorig1:RightLeg
    [SerializeField] private Transform rightFootBone;  // mixamorig1:RightFoot

    // ───────────────────────── IK Targets ─────────────────────────
    [Header("=== IK Hedefleri (Tracker/Controller pozisyonları) ===")]

    [Tooltip("HMD pozisyon ve rotasyonu")]
    [SerializeField] private Transform headTarget;

    [Tooltip("Sol kontrolcü (el hedefi)")]
    [SerializeField] private Transform leftHandTarget;

    [Tooltip("Sağ kontrolcü (el hedefi)")]
    [SerializeField] private Transform rightHandTarget;

    [Tooltip("Pelvis/Bel tracker")]
    [SerializeField] private Transform pelvisTarget;

    [Tooltip("Sol bacak tracker (shinMountedTrackers aktifken kaval kemiğine monte,\n" +
             "kapalıyken ayak bileği tracker)")]
    [SerializeField] private Transform leftFootTarget;

    [Tooltip("Sağ bacak tracker (shinMountedTrackers aktifken kaval kemiğine monte,\n" +
             "kapalıyken ayak bileği tracker)")]
    [SerializeField] private Transform rightFootTarget;

    [Header("Opsiyonel Dirsek Hint Objeleri")]
    [Tooltip("Sol dirsek bend düzlemi için referans.")]
    [SerializeField] private Transform leftElbowHintTarget;
    [Tooltip("Sağ dirsek bend düzlemi için referans.")]
    [SerializeField] private Transform rightElbowHintTarget;

    [Header("Opsiyonel Diz Hint Objeleri")]
    [Tooltip("Sol dizin bukunme duzlemi icin hint hedefi.")]
    [FormerlySerializedAs("leftKneeTracker")]
    [SerializeField] private Transform leftKneeHintTarget;
    [Tooltip("Sag dizin bukunme duzlemi icin hint hedefi.")]
    [FormerlySerializedAs("rightKneeTracker")]
    [SerializeField] private Transform rightKneeHintTarget;

    [Header("=== Tracker Yerleşim Ayarları ===")]
    [Tooltip("Tracker'lar ayak bileği yerine kaval kemiğine (tibia) monte edilmişse aktif edin.\n" +
             "Aktifken: ayak bileği pozisyonu ve diz hint'i tracker'dan otomatik hesaplanır.")]
    [SerializeField] private bool shinMountedTrackers = true;

    [Header("Opsiyonel Üst Bacak Trackerları (5-tracker kurulum)")]
    [Tooltip("Sol üst bacak (femur) tracker. Atandığında bacak doğrudan FK ile sürülür.")]
    [SerializeField] private Transform leftThighTracker;
    [Tooltip("Sağ üst bacak (femur) tracker. Atandığında bacak doğrudan FK ile sürülür.")]
    [SerializeField] private Transform rightThighTracker;

    // ───────────────────────── Settings ─────────────────────────
    [Header("=== Ayarlar ===")]

    [Tooltip("Kol IK ağırlığı (0 = FK, 1 = tam IK)")]
    [SerializeField, Range(0f, 1f)] private float armIKWeight = 1f;

    [Tooltip("Bacak IK ağırlığı (0 = FK, 1 = tam IK)")]
    [SerializeField, Range(0f, 1f)] private float legIKWeight = 1f;

    [Tooltip("Baş takip ağırlığı")]
    [SerializeField, Range(0f, 1f)] private float headWeight = 1f;

    [Tooltip("Pelvis takip ağırlığı")]
    [SerializeField, Range(0f, 1f)] private float pelvisWeight = 1f;

    [Tooltip("Omurga sertliği — HMD-pelvis arası omurga interpolasyonu (0 = sadece pelvis, 1 = tamamen HMD'ye bak)")]
    [SerializeField, Range(0f, 1f)] private float spineStiffness = 0.5f;

    [Tooltip("Diz hint uzaklığı (tracker yokken otomatik hesaplama)")]
    [SerializeField] private float kneeHintDistance = 0.4f;

    [Tooltip("Dirsek hint uzaklığı")]
    [SerializeField] private float elbowHintDistance = 0.3f;

    [Tooltip("Hedef rotasyonu el/ayağa da uygula")]
    [SerializeField] private bool applyTargetRotation = true;

    // ───────────────────────── Calibration Data ─────────────────────────
    private bool _calibrated;

    // Tracker → Bone offset quaternions
    private Quaternion _pelvisOffset = Quaternion.identity;
    private Quaternion _leftFootOffset = Quaternion.identity;
    private Quaternion _rightFootOffset = Quaternion.identity;
    private Quaternion _leftFootShinOffset = Quaternion.identity;
    private Quaternion _rightFootShinOffset = Quaternion.identity;
    private Quaternion _headOffset = Quaternion.identity;
    private Quaternion _leftHandOffset = Quaternion.identity;
    private Quaternion _rightHandOffset = Quaternion.identity;

    // Per-limb hint directions stored in pelvis-local space at calibration time.
    // At calibration (T-pose), we record exactly which direction each mid-joint
    // (elbow / knee) points relative to the pelvis. At runtime we rotate these
    // directions by the pelvis rotation to get a stable, body-relative hint that
    // is never collinear with the limb axis regardless of body orientation.
    private Vector3 _leftElbowHintDirLocal  = Vector3.back;
    private Vector3 _rightElbowHintDirLocal = Vector3.back;
    private Vector3 _leftKneeHintDirLocal   = Vector3.forward;
    private Vector3 _rightKneeHintDirLocal  = Vector3.forward;

    // Shin tracker calibration: ankle/knee positions in tracker-local space
    private Vector3 _leftShinToAnkleLocal;
    private Vector3 _rightShinToAnkleLocal;
    private Vector3 _leftShinToKneeLocal;
    private Vector3 _rightShinToKneeLocal;
    // Shin tracker → bone rotation offsets
    private Quaternion _leftShinToLegRot  = Quaternion.identity;
    private Quaternion _rightShinToLegRot = Quaternion.identity;
    private Quaternion _leftShinToFootRot  = Quaternion.identity;
    private Quaternion _rightShinToFootRot = Quaternion.identity;
    // Thigh tracker → bone rotation offsets (5-tracker mode)
    private Quaternion _leftThighToUpLegRot  = Quaternion.identity;
    private Quaternion _rightThighToUpLegRot = Quaternion.identity;

    // Initial bone local rotations (model bind pose)
    private Quaternion _hipsInitLocal;
    private Quaternion _spineInitLocal;
    private Quaternion _spine1InitLocal;
    private Quaternion _spine2InitLocal;
    private Quaternion _neckInitLocal;
    private Quaternion _headInitLocal;

    // Initial bone local rotations for limb bones (bind-pose reset each frame)
    private Quaternion _leftShoulderInitLocal;
    private Quaternion _leftUpperArmInitLocal;
    private Quaternion _leftForeArmInitLocal;
    private Quaternion _leftHandInitLocal;
    private Quaternion _rightShoulderInitLocal;
    private Quaternion _rightUpperArmInitLocal;
    private Quaternion _rightForeArmInitLocal;
    private Quaternion _rightHandInitLocal;
    private Quaternion _leftUpLegInitLocal;
    private Quaternion _leftLegInitLocal;
    private Quaternion _leftFootInitLocal;
    private Quaternion _rightUpLegInitLocal;
    private Quaternion _rightLegInitLocal;
    private Quaternion _rightFootInitLocal;

    // Calibration version
    public int CalibrationVersion { get; private set; }

    // ───────────────────────── Unity Lifecycle ─────────────────────────

    private void Start()
    {
        CacheInitialBoneRotations();
    }

    /// <summary>
    /// Edit Mode'da IK'yı test etmek için: Inspector'da sağ tık → "Editor Kalibrasyon ve Test".
    /// Kemikleri mevcut halinden kalibre eder, ardından LateUpdate Edit Mode'da da çalışır.
    /// IK target objelerini Scene view'da sürükleyerek sonucu anlık görebilirsiniz.
    /// </summary>
    [ContextMenu("Editor Kalibrasyon ve Test")]
    public void EditorCalibrateAndTest()
    {
        CacheInitialBoneRotations();
        SnapTargetsToCurrentBones();
        Calibrate();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[FullBodyIKSolver] Editor kalibrasyon tamam — IK target'ları Scene view'da sürükleyebilirsiniz.");
    }

    private void LateUpdate()
    {
        // Edit mode'da her LateUpdate çağrısında solve çalışır.
        // IK target objelerini Scene view'da sürüklediğinizde avatar anlık güncellenir.
        if (!_calibrated) return;
        if (!hipsBone) return;

        // 0. Reset to bind pose — prevents accumulated IK errors from previous frames,
        //    fixes undo corruption, and ensures a clean starting state for each solve.
        RestoreBindPose();

        // 1. Pelvis (Hips) — DOĞRU kemik, Spine DEĞİL!
        SolvePelvis();

        // 2. Omurga — HMD ve pelvis arası interpolasyon
        SolveSpine();

        // 3. Baş — HMD takibi
        SolveHead();

        // 4. Kollar — Two-Bone IK
        SolveArm(leftUpperArmBone, leftForeArmBone, leftHandBone,
                 leftHandTarget, leftElbowHintTarget, isLeft: true);
        SolveArm(rightUpperArmBone, rightForeArmBone, rightHandBone,
                 rightHandTarget, rightElbowHintTarget, isLeft: false);

        // 5. Bacaklar — Two-Bone IK
        SolveLeg(leftUpLegBone, leftLegBone, leftFootBone,
                 leftFootTarget, leftKneeHintTarget, _leftFootOffset, isLeft: true);
        SolveLeg(rightUpLegBone, rightLegBone, rightFootBone,
                 rightFootTarget, rightKneeHintTarget, _rightFootOffset, isLeft: false);
    }

    // ───────────────────────── Calibration ─────────────────────────

    /// <summary>
    /// Calibrate the system. User should be standing upright in a natural pose.
    /// </summary>
    public void Calibrate()
    {
        if (!hipsBone || !headTarget || !pelvisTarget)
        {
            Debug.LogWarning("[FullBodyIKSolver] Kalibrasyon başarısız: Gerekli referanslar atanmamış.");
            return;
        }

        // Pelvis offset
        if (pelvisTarget)
        {
            _pelvisOffset = Quaternion.Inverse(pelvisTarget.rotation) * hipsBone.rotation;
        }

        // Head offset
        if (headTarget && headBone)
        {
            _headOffset = Quaternion.Inverse(headTarget.rotation) * headBone.rotation;
        }

        // Foot offsets
        if (leftFootTarget && leftFootBone)
        {
            _leftFootOffset = Quaternion.Inverse(leftFootTarget.rotation) * leftFootBone.rotation;
        }
        if (rightFootTarget && rightFootBone)
        {
            _rightFootOffset = Quaternion.Inverse(rightFootTarget.rotation) * rightFootBone.rotation;
        }

        // Hand offsets
        if (leftHandTarget && leftHandBone)
        {
            _leftHandOffset = Quaternion.Inverse(leftHandTarget.rotation) * leftHandBone.rotation;
        }
        if (rightHandTarget && rightHandBone)
        {
            _rightHandOffset = Quaternion.Inverse(rightHandTarget.rotation) * rightHandBone.rotation;
        }

        // --- Compute foot-to-shin offsets ---
        // Record foot rotation relative to shin direction so at runtime the foot
        // always follows the shin naturally regardless of tracker orientation.
        CalibrateFootShinOffset(leftLegBone, leftFootBone, ref _leftFootShinOffset);
        CalibrateFootShinOffset(rightLegBone, rightFootBone, ref _rightFootShinOffset);

        // --- Compute per-limb hint directions in pelvis-local space ---
        // At T-pose the mid-joint (elbow/knee) position relative to the root
        // joint projected off the limb axis gives us the exact bend direction.
        // Storing it in pelvis-local space means at runtime we just rotate it
        // by the pelvis rotation — always correct, never flips.
        CalibrateHintDirection(leftUpperArmBone,  leftForeArmBone,  leftHandBone,  ref _leftElbowHintDirLocal);
        CalibrateHintDirection(rightUpperArmBone, rightForeArmBone, rightHandBone, ref _rightElbowHintDirLocal);
        CalibrateHintDirection(leftUpLegBone,     leftLegBone,      leftFootBone,  ref _leftKneeHintDirLocal);
        CalibrateHintDirection(rightUpLegBone,    rightLegBone,     rightFootBone, ref _rightKneeHintDirLocal);

        // --- Shin tracker offsets (shin-mounted mode) ---
        if (shinMountedTrackers)
        {
            CalibrateShinTracker(leftFootTarget, leftLegBone, leftFootBone,
                ref _leftShinToAnkleLocal, ref _leftShinToKneeLocal,
                ref _leftShinToLegRot, ref _leftShinToFootRot);
            CalibrateShinTracker(rightFootTarget, rightLegBone, rightFootBone,
                ref _rightShinToAnkleLocal, ref _rightShinToKneeLocal,
                ref _rightShinToLegRot, ref _rightShinToFootRot);
        }

        // --- Thigh tracker offsets (5-tracker mode) ---
        if (leftThighTracker && leftUpLegBone)
            _leftThighToUpLegRot = Quaternion.Inverse(leftThighTracker.rotation) * leftUpLegBone.rotation;
        if (rightThighTracker && rightUpLegBone)
            _rightThighToUpLegRot = Quaternion.Inverse(rightThighTracker.rotation) * rightUpLegBone.rotation;

        _calibrated = true;
        CalibrationVersion++;
        Debug.Log("[FullBodyIKSolver] Kalibrasyon tamamlandı.");
    }

    /// <summary>
    /// Resets calibration.
    /// </summary>
    public void ResetCalibration()
    {
        _calibrated = false;
        Debug.Log("[FullBodyIKSolver] Kalibrasyon sıfırlandı.");
    }

    public bool IsCalibrated => _calibrated;

    /// <summary>
    /// Aligns IK target transforms to the avatar's current bone transforms.
    /// Useful in editor simulation when scene targets were left in an old location.
    /// </summary>
    public void SnapTargetsToCurrentBones()
    {
        SnapTarget(pelvisTarget, hipsBone);
        SnapTarget(headTarget, headBone);
        SnapTarget(leftHandTarget, leftHandBone);
        SnapTarget(rightHandTarget, rightHandBone);

        // Shin-mounted mode: snap foot targets to shin midpoint; otherwise snap to foot bone
        if (shinMountedTrackers)
        {
            if (leftFootTarget != null && leftLegBone != null && leftFootBone != null)
            {
                Vector3 shinMid = (leftLegBone.position + leftFootBone.position) * 0.5f;
                leftFootTarget.SetPositionAndRotation(shinMid, leftLegBone.rotation);
            }
            if (rightFootTarget != null && rightLegBone != null && rightFootBone != null)
            {
                Vector3 shinMid = (rightLegBone.position + rightFootBone.position) * 0.5f;
                rightFootTarget.SetPositionAndRotation(shinMid, rightLegBone.rotation);
            }
        }
        else
        {
            SnapTarget(leftFootTarget, leftFootBone);
            SnapTarget(rightFootTarget, rightFootBone);
        }

        // Thigh trackers (5-tracker mode): snap to thigh midpoint
        if (leftThighTracker != null && leftUpLegBone != null && leftLegBone != null)
        {
            Vector3 thighMid = (leftUpLegBone.position + leftLegBone.position) * 0.5f;
            leftThighTracker.SetPositionAndRotation(thighMid, leftUpLegBone.rotation);
        }
        if (rightThighTracker != null && rightUpLegBone != null && rightLegBone != null)
        {
            Vector3 thighMid = (rightUpLegBone.position + rightLegBone.position) * 0.5f;
            rightThighTracker.SetPositionAndRotation(thighMid, rightUpLegBone.rotation);
        }

        if (leftKneeHintTarget != null && leftLegBone != null)
        {
            Vector3 fwd = hipsBone != null ? hipsBone.forward : Vector3.forward;
            leftKneeHintTarget.position = leftLegBone.position + fwd * kneeHintDistance;
        }

        if (rightKneeHintTarget != null && rightLegBone != null)
        {
            Vector3 fwd = hipsBone != null ? hipsBone.forward : Vector3.forward;
            rightKneeHintTarget.position = rightLegBone.position + fwd * kneeHintDistance;
        }
    }

    // ───────────────────────── Solver Methods ─────────────────────────

    private void SolvePelvis()
    {
        if (!pelvisTarget || pelvisWeight <= 0f) return;

        // CRITICAL FIX: Drive Hips (mixamorig1:Hips), NOT Spine!
        // Hips is the parent of both Spine and legs, so rotating Hips
        // correctly rotates the entire body including legs.
        Quaternion targetRot = pelvisTarget.rotation * _pelvisOffset;
        Vector3 targetPos = pelvisTarget.position;

        if (pelvisWeight >= 1f)
        {
            hipsBone.rotation = targetRot;
            hipsBone.position = targetPos;
        }
        else
        {
            hipsBone.rotation = Quaternion.Slerp(hipsBone.rotation, targetRot, pelvisWeight);
            hipsBone.position = Vector3.Lerp(hipsBone.position, targetPos, pelvisWeight);
        }
    }

    private void SolveSpine()
    {
        if (!headTarget || !spineBone) return;
        if (spineStiffness <= 0f) return;

        // Goal: distribute the rotation between pelvis and head across the spine chain.
        // We interpolate each spine bone partially towards the head direction.

        Vector3 headWorldPos = headTarget.position;
        Vector3 hipsPos = hipsBone.position;
        Vector3 hipsToHead = (headWorldPos - hipsPos).normalized;

        if (hipsToHead.sqrMagnitude < 0.001f) return;

        // Current spine direction (from hips towards head in the model)
        Vector3 currentSpineUp = (headBone != null ? headBone.position : spineBone.position + spineBone.up) - hipsPos;
        currentSpineUp = currentSpineUp.normalized;

        if (currentSpineUp.sqrMagnitude < 0.001f) return;

        // Calculate the delta rotation from current spine direction to desired (HMD) direction
        Quaternion spineAdjust = Quaternion.FromToRotation(currentSpineUp, hipsToHead);

        // Distribute across spine bones with decreasing weight
        Transform[] spineChain = GetSpineChain();
        float totalWeight = spineStiffness;

        for (int i = 0; i < spineChain.Length; i++)
        {
            if (spineChain[i] == null) continue;

            // Each spine bone gets a fraction of the total rotation
            float boneWeight = totalWeight / spineChain.Length;
            Quaternion partial = Quaternion.Slerp(Quaternion.identity, spineAdjust, boneWeight);
            spineChain[i].rotation = partial * spineChain[i].rotation;
        }
    }

    private void SolveHead()
    {
        if (!headTarget || !headBone || headWeight <= 0f) return;

        Quaternion targetRot = headTarget.rotation * _headOffset;

        if (headWeight >= 1f)
        {
            headBone.rotation = targetRot;
        }
        else
        {
            headBone.rotation = Quaternion.Slerp(headBone.rotation, targetRot, headWeight);
        }
    }

    private void SolveArm(Transform upperArm, Transform foreArm, Transform hand,
                          Transform target, Transform elbowHint, bool isLeft)
    {
        if (!upperArm || !foreArm || !hand || !target) return;
        if (armIKWeight <= 0f) return;

        Vector3 hintPos;
        if (elbowHint != null)
        {
            hintPos = elbowHint.position;
        }
        else
        {
            float upperLen = (foreArm.position - upperArm.position).magnitude;
            float lowerLen = (hand.position - foreArm.position).magnitude;
            float limbLen = Mathf.Max(upperLen + lowerLen, 0.1f);

            // Project the calibrated bend direction off the UPPER ARM axis (not the full
            // shoulder→hand axis). The upper arm direction is always well-defined and
            // never collinear with the bend direction regardless of hand position.
            Vector3 upperArmDir = (foreArm.position - upperArm.position).normalized;
            if (upperArmDir.sqrMagnitude < 0.001f)
                upperArmDir = isLeft ? -hipsBone.right : hipsBone.right;

            Vector3 calibDirLocal = isLeft ? _leftElbowHintDirLocal : _rightElbowHintDirLocal;
            Vector3 preferredBend = hipsBone.TransformDirection(calibDirLocal);

            Vector3 hintDir = Vector3.ProjectOnPlane(preferredBend, upperArmDir);
            if (hintDir.sqrMagnitude < 0.001f)
                hintDir = Vector3.ProjectOnPlane(-hipsBone.forward, upperArmDir);
            if (hintDir.sqrMagnitude < 0.001f)
                hintDir = Vector3.ProjectOnPlane(Vector3.down, upperArmDir);
            hintDir = hintDir.normalized;

            // Place the hint at the midpoint of the shoulder→hand line, then push it
            // perpendicularly by hintDistance. This guarantees the vector from root
            // to hint always has a large perpendicular component, avoiding the
            // near-collinear singularity inside TwoBoneIKSolver regardless of arm pose.
            Vector3 rootToHandDir = (target.position - upperArm.position).normalized;
            if (rootToHandDir.sqrMagnitude < 0.001f) rootToHandDir = isLeft ? -hipsBone.right : hipsBone.right;
            Vector3 midPoint = upperArm.position + rootToHandDir * (limbLen * 0.5f);
            float hintDistance = Mathf.Max(elbowHintDistance, limbLen * 0.6f);
            hintPos = midPoint + hintDir * hintDistance;
        }

        // Target rotation
        Quaternion handOffset = isLeft ? _leftHandOffset : _rightHandOffset;
        Quaternion targetRot = applyTargetRotation ? target.rotation * handOffset : hand.rotation;

        TwoBoneIKSolver.Solve(
            upperArm, foreArm, hand,
            target.position, targetRot,
            hintPos,
            armIKWeight, applyTargetRotation ? armIKWeight : 0f, 1f);
    }

    private void SolveLeg(Transform upLeg, Transform leg, Transform foot,
                          Transform footTarget, Transform kneeHint,
                          Quaternion footOffset, bool isLeft)
    {
        if (!upLeg || !leg || !foot || !footTarget) return;
        if (legIKWeight <= 0f) return;

        // ── 5-tracker Direct FK Mode ──
        Transform thighTracker = isLeft ? leftThighTracker : rightThighTracker;
        if (shinMountedTrackers && thighTracker != null)
        {
            SolveLegDirectFK(upLeg, leg, foot, footTarget, thighTracker, isLeft);
            return;
        }

        float upperLen = (leg.position - upLeg.position).magnitude;
        float lowerLen = (foot.position - leg.position).magnitude;
        float limbLen = Mathf.Max(upperLen + lowerLen, 0.1f);

        // ── Shin-Mounted Tracker Mode (3-tracker) ──
        // Derive ankle position and knee hint from the shin tracker.
        if (shinMountedTrackers)
        {
            Vector3 ankleTargetPos = footTarget.TransformPoint(
                isLeft ? _leftShinToAnkleLocal : _rightShinToAnkleLocal);
            Vector3 kneeHintPos = footTarget.TransformPoint(
                isLeft ? _leftShinToKneeLocal : _rightShinToKneeLocal);

            // Collinearity safety: if leg is nearly straight, knee hint may be
            // on the hip→ankle axis. Fall back to calibrated bend direction.
            Vector3 hipToAnkle = ankleTargetPos - upLeg.position;
            float hipToAnkleDist = hipToAnkle.magnitude;
            Vector3 hipToAnkleDir = hipToAnkleDist > 0.001f
                ? hipToAnkle / hipToAnkleDist : -Vector3.up;
            Vector3 perpComp = Vector3.ProjectOnPlane(
                kneeHintPos - upLeg.position, hipToAnkleDir);
            if (perpComp.magnitude < limbLen * 0.05f)
            {
                Vector3 calibDir = isLeft ? _leftKneeHintDirLocal : _rightKneeHintDirLocal;
                Vector3 fallbackDir = hipsBone.TransformDirection(calibDir);
                Vector3 hintDir = Vector3.ProjectOnPlane(fallbackDir, hipToAnkleDir);
                if (hintDir.sqrMagnitude < 0.001f)
                    hintDir = Vector3.ProjectOnPlane(Vector3.forward, hipToAnkleDir);
                kneeHintPos = upLeg.position + hipToAnkleDir * (hipToAnkleDist * 0.5f)
                              + hintDir.normalized * kneeHintDistance;
            }

            TwoBoneIKSolver.Solve(
                upLeg, leg, foot,
                ankleTargetPos, foot.rotation,
                kneeHintPos,
                legIKWeight, 0f, 1f);

            AlignFootToShin(leg, foot, isLeft ? _leftFootShinOffset : _rightFootShinOffset);
            return;
        }

        // ── Legacy Ankle-Mounted Mode ──
        // Compute hip→foot direction (used for hint stability checks)
        Vector3 hipToFoot = footTarget.position - upLeg.position;
        float hipToFootDist = hipToFoot.magnitude;
        Vector3 hipToFootDir = hipToFootDist > 0.001f ? hipToFoot / hipToFootDist : -Vector3.up;

        // Get calibrated bend direction in world space
        Vector3 calibDirLocal = isLeft ? _leftKneeHintDirLocal : _rightKneeHintDirLocal;
        Vector3 preferredBend = hipsBone.TransformDirection(calibDirLocal);

        Vector3 hintPos;
        if (kneeHint != null)
        {
            // Use explicit hint position but ensure it has enough perpendicular
            // offset from the hip→foot axis. A static hint can become collinear
            // with the limb axis when the foot moves, causing the knee to flip.
            Vector3 hipToHint = kneeHint.position - upLeg.position;
            Vector3 perpComponent = Vector3.ProjectOnPlane(hipToHint, hipToFootDir);
            float minPerp = limbLen * 0.1f;

            if (perpComponent.magnitude < minPerp)
            {
                // Hint is nearly collinear — add perpendicular offset using
                // the calibrated bend direction to maintain correct knee direction.
                Vector3 bendPerp = Vector3.ProjectOnPlane(preferredBend, hipToFootDir);
                if (bendPerp.sqrMagnitude < 0.001f)
                    bendPerp = Vector3.ProjectOnPlane(Vector3.up, hipToFootDir);
                if (bendPerp.sqrMagnitude < 0.001f)
                    bendPerp = Vector3.ProjectOnPlane(Vector3.forward, hipToFootDir);
                bendPerp = bendPerp.normalized;

                Vector3 onAxis = Vector3.Project(hipToHint, hipToFootDir);
                hintPos = upLeg.position + onAxis + bendPerp * Mathf.Max(minPerp, kneeHintDistance);
            }
            else
            {
                hintPos = kneeHint.position;
            }
        }
        else
        {
            // Auto-compute hint: project the calibrated bend direction perpendicular
            // to the hip→foot axis (not the thigh axis). This ensures the hint is
            // never collinear with the limb regardless of foot target position,
            // preventing knee direction flips when the foot moves in front or behind.
            Vector3 hintDir = Vector3.ProjectOnPlane(preferredBend, hipToFootDir);
            if (hintDir.sqrMagnitude < 0.001f)
                hintDir = Vector3.ProjectOnPlane(Vector3.up, hipToFootDir);
            if (hintDir.sqrMagnitude < 0.001f)
                hintDir = Vector3.ProjectOnPlane(hipsBone.forward, hipToFootDir);
            if (hintDir.sqrMagnitude < 0.001f)
                hintDir = Vector3.ProjectOnPlane(Vector3.forward, hipToFootDir);
            hintDir = hintDir.normalized;

            // Place hint at the midpoint of hip→foot, then push perpendicular
            Vector3 midPoint = upLeg.position + hipToFootDir * (hipToFootDist * 0.5f);
            float hintDistance = Mathf.Max(kneeHintDistance, limbLen * 0.6f);
            hintPos = midPoint + hintDir * hintDistance;
        }

        // Solve IK position only — foot rotation is handled by AlignFootToShin below.
        TwoBoneIKSolver.Solve(
            upLeg, leg, foot,
            footTarget.position, foot.rotation,
            hintPos,
            legIKWeight, 0f, 1f);

        // Align foot to shin direction using the offset recorded at calibration.
        AlignFootToShin(leg, foot, isLeft ? _leftFootShinOffset : _rightFootShinOffset);
    }

    /// <summary>
    /// Direct FK mode for 5-tracker setup (shin + thigh per leg).
    /// Sets bone rotations directly from tracker measurements — no IK needed.
    /// </summary>
    private void SolveLegDirectFK(Transform upLeg, Transform leg, Transform foot,
                                   Transform shinTracker, Transform thighTracker, bool isLeft)
    {
        // Upper leg rotation from thigh tracker
        Quaternion thighOffset = isLeft ? _leftThighToUpLegRot : _rightThighToUpLegRot;
        Quaternion targetUpLegRot = thighTracker.rotation * thighOffset;
        upLeg.rotation = Quaternion.Slerp(upLeg.rotation, targetUpLegRot, legIKWeight);

        // Lower leg (shin) rotation from shin tracker
        Quaternion shinLegOffset = isLeft ? _leftShinToLegRot : _rightShinToLegRot;
        Quaternion targetLegRot = shinTracker.rotation * shinLegOffset;
        leg.rotation = Quaternion.Slerp(leg.rotation, targetLegRot, legIKWeight);

        // Foot follows shin with calibrated offset
        AlignFootToShin(leg, foot, isLeft ? _leftFootShinOffset : _rightFootShinOffset);
    }

    // ───────────────────────── Helpers ─────────────────────────

    /// <summary>
    /// Orients the foot bone so it always follows the shin bone's rotation with
    /// the relative offset recorded at calibration. Uses the leg bone's rotation
    /// directly instead of LookRotation to avoid singularity-based flips.
    /// </summary>
    private static void AlignFootToShin(Transform leg, Transform foot, Quaternion shinOffset)
    {
        if (leg == null || foot == null) return;
        // Apply the foot rotation as a fixed offset from the shin bone's rotation.
        // This preserves the exact anatomical angle between shin and foot from
        // calibration and is inherently stable — no LookRotation singularity.
        foot.rotation = leg.rotation * shinOffset;
    }

    /// <summary>
    /// At calibration: record foot rotation relative to the leg bone's rotation
    /// so AlignFootToShin can reproduce the exact anatomical foot angle at runtime.
    /// </summary>
    private static void CalibrateFootShinOffset(Transform leg, Transform foot,
                                                 ref Quaternion shinOffset)
    {
        if (leg == null || foot == null) return;
        // Store foot rotation in leg-bone-local space.
        // At runtime: foot.rotation = leg.rotation * shinOffset
        shinOffset = Quaternion.Inverse(leg.rotation) * foot.rotation;
    }

    /// <summary>
    /// At calibration time: project the mid-joint offset off the limb axis to
    /// get the pure bend direction, then store it in pelvis-local space so it
    /// rotates correctly with the body at runtime.
    /// </summary>
    private void CalibrateHintDirection(Transform root, Transform mid, Transform tip,
                                         ref Vector3 hintDirLocal)
    {
        if (root == null || mid == null || tip == null) return;

        Vector3 limbAxis = (tip.position - root.position);
        if (limbAxis.sqrMagnitude < 0.0001f) return;
        limbAxis = limbAxis.normalized;

        // Project mid-joint position off the limb axis → pure bend direction
        Vector3 midOffset = Vector3.ProjectOnPlane(mid.position - root.position, limbAxis);
        if (midOffset.sqrMagnitude < 0.0001f) return;  // limb is perfectly straight, keep default

        // Store in pelvis-local space so it follows body rotation at runtime
        hintDirLocal = hipsBone.InverseTransformDirection(midOffset.normalized);
    }

    /// <summary>
    /// Calibrates shin tracker offsets: stores ankle and knee positions in tracker-local
    /// space and rotation offsets from tracker to leg/foot bones.
    /// </summary>
    private static void CalibrateShinTracker(Transform tracker, Transform legBone, Transform footBone,
                                              ref Vector3 toAnkleLocal, ref Vector3 toKneeLocal,
                                              ref Quaternion toLegRot, ref Quaternion toFootRot)
    {
        if (tracker == null) return;
        if (footBone != null)
        {
            toAnkleLocal = tracker.InverseTransformPoint(footBone.position);
            toFootRot = Quaternion.Inverse(tracker.rotation) * footBone.rotation;
        }
        if (legBone != null)
        {
            toKneeLocal = tracker.InverseTransformPoint(legBone.position);
            toLegRot = Quaternion.Inverse(tracker.rotation) * legBone.rotation;
        }
    }

    private void CacheInitialBoneRotations()
    {
        if (hipsBone) _hipsInitLocal = hipsBone.localRotation;
        if (spineBone) _spineInitLocal = spineBone.localRotation;
        if (spine1Bone) _spine1InitLocal = spine1Bone.localRotation;
        if (spine2Bone) _spine2InitLocal = spine2Bone.localRotation;
        if (neckBone) _neckInitLocal = neckBone.localRotation;
        if (headBone) _headInitLocal = headBone.localRotation;
        // Limb bones
        if (leftShoulderBone) _leftShoulderInitLocal = leftShoulderBone.localRotation;
        if (leftUpperArmBone) _leftUpperArmInitLocal = leftUpperArmBone.localRotation;
        if (leftForeArmBone) _leftForeArmInitLocal = leftForeArmBone.localRotation;
        if (leftHandBone) _leftHandInitLocal = leftHandBone.localRotation;
        if (rightShoulderBone) _rightShoulderInitLocal = rightShoulderBone.localRotation;
        if (rightUpperArmBone) _rightUpperArmInitLocal = rightUpperArmBone.localRotation;
        if (rightForeArmBone) _rightForeArmInitLocal = rightForeArmBone.localRotation;
        if (rightHandBone) _rightHandInitLocal = rightHandBone.localRotation;
        if (leftUpLegBone) _leftUpLegInitLocal = leftUpLegBone.localRotation;
        if (leftLegBone) _leftLegInitLocal = leftLegBone.localRotation;
        if (leftFootBone) _leftFootInitLocal = leftFootBone.localRotation;
        if (rightUpLegBone) _rightUpLegInitLocal = rightUpLegBone.localRotation;
        if (rightLegBone) _rightLegInitLocal = rightLegBone.localRotation;
        if (rightFootBone) _rightFootInitLocal = rightFootBone.localRotation;
    }

    /// <summary>
    /// Restores all bones to their cached bind-pose local rotations.
    /// Called at the start of each LateUpdate to prevent accumulated IK errors.
    /// </summary>
    private void RestoreBindPose()
    {
        if (hipsBone) hipsBone.localRotation = _hipsInitLocal;
        if (spineBone) spineBone.localRotation = _spineInitLocal;
        if (spine1Bone) spine1Bone.localRotation = _spine1InitLocal;
        if (spine2Bone) spine2Bone.localRotation = _spine2InitLocal;
        if (neckBone) neckBone.localRotation = _neckInitLocal;
        if (headBone) headBone.localRotation = _headInitLocal;
        if (leftShoulderBone) leftShoulderBone.localRotation = _leftShoulderInitLocal;
        if (leftUpperArmBone) leftUpperArmBone.localRotation = _leftUpperArmInitLocal;
        if (leftForeArmBone) leftForeArmBone.localRotation = _leftForeArmInitLocal;
        if (leftHandBone) leftHandBone.localRotation = _leftHandInitLocal;
        if (rightShoulderBone) rightShoulderBone.localRotation = _rightShoulderInitLocal;
        if (rightUpperArmBone) rightUpperArmBone.localRotation = _rightUpperArmInitLocal;
        if (rightForeArmBone) rightForeArmBone.localRotation = _rightForeArmInitLocal;
        if (rightHandBone) rightHandBone.localRotation = _rightHandInitLocal;
        if (leftUpLegBone) leftUpLegBone.localRotation = _leftUpLegInitLocal;
        if (leftLegBone) leftLegBone.localRotation = _leftLegInitLocal;
        if (leftFootBone) leftFootBone.localRotation = _leftFootInitLocal;
        if (rightUpLegBone) rightUpLegBone.localRotation = _rightUpLegInitLocal;
        if (rightLegBone) rightLegBone.localRotation = _rightLegInitLocal;
        if (rightFootBone) rightFootBone.localRotation = _rightFootInitLocal;
    }

    private static void SnapTarget(Transform target, Transform source)
    {
        if (target == null || source == null) return;
        target.SetPositionAndRotation(source.position, source.rotation);
    }

    private Transform[] GetSpineChain()
    {
        // Return available spine bones in order
        int count = 0;
        if (spineBone) count++;
        if (spine1Bone) count++;
        if (spine2Bone) count++;

        Transform[] chain = new Transform[count];
        int idx = 0;
        if (spineBone) chain[idx++] = spineBone;
        if (spine1Bone) chain[idx++] = spine1Bone;
        if (spine2Bone) chain[idx++] = spine2Bone;
        return chain;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_calibrated) return;

        // Draw IK targets
        Gizmos.color = Color.green;
        if (headTarget) Gizmos.DrawWireSphere(headTarget.position, 0.05f);
        if (leftHandTarget) Gizmos.DrawWireSphere(leftHandTarget.position, 0.03f);
        if (rightHandTarget) Gizmos.DrawWireSphere(rightHandTarget.position, 0.03f);

        Gizmos.color = Color.blue;
        if (pelvisTarget) Gizmos.DrawWireSphere(pelvisTarget.position, 0.05f);

        Gizmos.color = Color.red;
        if (leftFootTarget) Gizmos.DrawWireSphere(leftFootTarget.position, 0.03f);
        if (rightFootTarget) Gizmos.DrawWireSphere(rightFootTarget.position, 0.03f);

        // Shin-mounted: show derived ankle positions
        if (shinMountedTrackers)
        {
            Gizmos.color = Color.magenta;
            if (leftFootTarget)
            {
                Vector3 ankleL = leftFootTarget.TransformPoint(_leftShinToAnkleLocal);
                Gizmos.DrawWireSphere(ankleL, 0.02f);
                Gizmos.DrawLine(leftFootTarget.position, ankleL);
            }
            if (rightFootTarget)
            {
                Vector3 ankleR = rightFootTarget.TransformPoint(_rightShinToAnkleLocal);
                Gizmos.DrawWireSphere(ankleR, 0.02f);
                Gizmos.DrawLine(rightFootTarget.position, ankleR);
            }
        }

        Gizmos.color = Color.cyan;
        if (leftThighTracker) Gizmos.DrawWireSphere(leftThighTracker.position, 0.03f);
        if (rightThighTracker) Gizmos.DrawWireSphere(rightThighTracker.position, 0.03f);

        Gizmos.color = Color.yellow;
        if (leftKneeHintTarget) Gizmos.DrawWireSphere(leftKneeHintTarget.position, 0.03f);
        if (rightKneeHintTarget) Gizmos.DrawWireSphere(rightKneeHintTarget.position, 0.03f);

        // Draw bone chain
        if (hipsBone && headBone)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(hipsBone.position, headBone.position);
        }
    }
#endif
}
