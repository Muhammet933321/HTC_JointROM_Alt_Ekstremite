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

    [Header("Opsiyonel Üst Kol Trackerları (5-tracker kol kurulumu)")]
    [Tooltip("Sol üst kol (humerus) tracker. Atandığında kol doğrudan FK ile sürülür.")]
    [SerializeField] private Transform leftUpperArmTracker;
    [Tooltip("Sağ üst kol (humerus) tracker. Atandığında kol doğrudan FK ile sürülür.")]
    [SerializeField] private Transform rightUpperArmTracker;

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

    [Tooltip("Shin tracker rotasyonunun bacak kemiğine uygulanma ağırlığı (shin-mounted modda).\n" +
             "1 = tracker açısı doğrudan uygulanır, 0 = sadece IK.")]
    [SerializeField, Range(0f, 1f)] private float shinRotationBlend = 0.7f;

    [Tooltip("Tracker'lar el bileği yerine ön kola monte edilmişse aktif edin.\n" +
             "Aktifken: el bileği pozisyonu ve dirsek hint'i tracker'dan otomatik hesaplanır.")]
    [SerializeField] private bool forearmMountedTrackers = false;

    [Tooltip("Ön kol tracker rotasyonunun kol kemiğine uygulanma ağırlığı (forearm-mounted modda).\n" +
             "1 = tracker açısı doğrudan uygulanır, 0 = sadece IK.")]
    [SerializeField, Range(0f, 1f)] private float forearmRotationBlend = 0.7f;

    [Tooltip("Kontrolcünün rotasyonunu dirsek yönünü hesaplamak için kullan.\n" +
             "Aktifken: kontrolcünün açısı dirsek hint pozisyonunu belirler — dirsek artık doğru tarafa bükülen.\n" +
             "Forearm Mounted Trackers kapalıyken (klasik kontrolcü modu) geçerlidir.")]
    [SerializeField] private bool useControllerRotationForElbow = true;

    // ───────────────────────── Calibration Data ─────────────────────────
    private bool _calibrated;

    // Body proportion scaling: avatarHeight / playerHeight
    private float _bodyScale = 1f;
    // Calibration-time pelvis positions (for delta-based scaling)
    private Vector3 _calibPelvisTrackerPos;
    private Vector3 _calibPelvisBonePos;
    // Calibration-time head target Y (for scale reference)
    private Vector3 _calibHeadTrackerPos;

    // Per-limb calibration positions for delta-based scaling.
    // At runtime, only the CHANGE from calibration is scaled, so the result is
    // exact at calibration and proportionally correct during movement.
    private Vector3 _calibLeftHandTrackerPos, _calibLeftHandBonePos;
    private Vector3 _calibRightHandTrackerPos, _calibRightHandBonePos;
    private Vector3 _calibLeftFootTrackerPos, _calibLeftFootBonePos;
    private Vector3 _calibRightFootTrackerPos, _calibRightFootBonePos;
    // Shin-mounted mode: derived ankle/knee world positions at calibration
    private Vector3 _calibLeftAnkleDerivedPos, _calibLeftKneeDerivedPos;
    private Vector3 _calibRightAnkleDerivedPos, _calibRightKneeDerivedPos;
    private Vector3 _calibLeftKneeBonePos, _calibRightKneeBonePos;

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

    // Controller rotation → elbow direction (controller-local space at calibration).
    // Captures which axis of the controller points toward the elbow so that at runtime
    // we can reconstruct elbow world position purely from controller rotation + hand pos.
    private Vector3 _leftControllerToElbowDir  = Vector3.up;
    private Vector3 _rightControllerToElbowDir = Vector3.up;
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

    // Upper arm tracker → bone rotation offsets (5-tracker arm mode)
    private Quaternion _leftUpperArmToArmRot  = Quaternion.identity;
    private Quaternion _rightUpperArmToArmRot = Quaternion.identity;
    // Forearm-mounted tracker: wrist/elbow positions in tracker-local space
    private Vector3 _leftForeArmToWristLocal;
    private Vector3 _rightForeArmToWristLocal;
    private Vector3 _leftForeArmToElbowLocal;
    private Vector3 _rightForeArmToElbowLocal;
    // Forearm tracker → forearm bone rotation offset
    private Quaternion _leftForeArmToForeArmRot  = Quaternion.identity;
    private Quaternion _rightForeArmToForeArmRot = Quaternion.identity;
    // Hand rotation relative to forearm bone at calibration (like foot-shin offset)
    private Quaternion _leftHandForeArmOffset  = Quaternion.identity;
    private Quaternion _rightHandForeArmOffset = Quaternion.identity;
    // Derived wrist/elbow world positions at calibration (forearm-mounted mode)
    private Vector3 _calibLeftWristDerivedPos,  _calibLeftElbowDerivedPos;
    private Vector3 _calibRightWristDerivedPos, _calibRightElbowDerivedPos;
    private Vector3 _calibLeftElbowBonePos,     _calibRightElbowBonePos;

    // Cached spine chain (avoids per-frame allocation)
    private readonly Transform[] _spineChainCache = new Transform[3];
    private int _spineChainLen;

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
        RebuildSpineChainCache();
    }

    /// <summary>
    /// Edit Mode'da IK'yı test etmek için
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

        // --- Body proportion scale ---
        // Compute height ratio between avatar and player so tracker positions
        // can be scaled to match avatar bone lengths. This fixes issues where
        // small/large avatars produce wrong pelvis/foot heights.
        // Use foot BONE Y as floor reference for both player and avatar height.
        // In shin-mounted mode, foot targets are shin trackers (above ankles),
        // so using tracker Y would underestimate player height and inflate _bodyScale.
        {
            float floorY = Mathf.Min(
                leftFootBone  ? leftFootBone.position.y  : headTarget.position.y,
                rightFootBone ? rightFootBone.position.y : headTarget.position.y);
            float playerHeight = headTarget.position.y - floorY;
            float avatarHeight = headBone.position.y - floorY;
            _bodyScale = (playerHeight > 0.1f) ? (avatarHeight / playerHeight) : 1f;
            _calibPelvisTrackerPos = pelvisTarget.position;
            _calibPelvisBonePos    = hipsBone.position;
            _calibHeadTrackerPos   = headTarget.position;
            Debug.Log($"[FullBodyIKSolver] Body scale = {_bodyScale:F3}  (avatar={avatarHeight:F3}m, player={playerHeight:F3}m)");
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

        // --- Controller → elbow direction (controller-rotation-based elbow hint) ---
        // At calibration (T-pose), record which direction in controller-local space
        // points from the hand toward the elbow. At runtime, rotating this direction
        // by the controller rotation gives the world-space elbow direction.
        CalibrateControllerElbowDir(leftHandTarget,  leftForeArmBone,  leftHandBone,  ref _leftControllerToElbowDir);
        CalibrateControllerElbowDir(rightHandTarget, rightForeArmBone, rightHandBone, ref _rightControllerToElbowDir);

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

        // --- Upper arm tracker offsets (5-tracker arm FK mode) ---
        if (leftUpperArmTracker && leftUpperArmBone)
            _leftUpperArmToArmRot = Quaternion.Inverse(leftUpperArmTracker.rotation) * leftUpperArmBone.rotation;
        if (rightUpperArmTracker && rightUpperArmBone)
            _rightUpperArmToArmRot = Quaternion.Inverse(rightUpperArmTracker.rotation) * rightUpperArmBone.rotation;

        // --- Forearm tracker offsets (forearm-mounted mode) ---
        if (forearmMountedTrackers)
        {
            CalibrateForeArmTracker(leftHandTarget, leftForeArmBone, leftHandBone,
                ref _leftForeArmToWristLocal, ref _leftForeArmToElbowLocal, ref _leftForeArmToForeArmRot);
            CalibrateForeArmTracker(rightHandTarget, rightForeArmBone, rightHandBone,
                ref _rightForeArmToWristLocal, ref _rightForeArmToElbowLocal, ref _rightForeArmToForeArmRot);
        }
        // Hand-forearm offset: stored even in non-forearm-mounted mode so
        // SolveArmDirectFK (5-tracker) can orient the hand correctly.
        CalibrateHandForeArmOffset(leftForeArmBone, leftHandBone, ref _leftHandForeArmOffset);
        CalibrateHandForeArmOffset(rightForeArmBone, rightHandBone, ref _rightHandForeArmOffset);

        if (leftForeArmBone)  _calibLeftElbowBonePos  = leftForeArmBone.position;
        if (rightForeArmBone) _calibRightElbowBonePos = rightForeArmBone.position;

        if (forearmMountedTrackers)
        {
            if (leftHandTarget)
            {
                _calibLeftWristDerivedPos = leftHandTarget.TransformPoint(_leftForeArmToWristLocal);
                _calibLeftElbowDerivedPos = leftHandTarget.TransformPoint(_leftForeArmToElbowLocal);
            }
            if (rightHandTarget)
            {
                _calibRightWristDerivedPos = rightHandTarget.TransformPoint(_rightForeArmToWristLocal);
                _calibRightElbowDerivedPos = rightHandTarget.TransformPoint(_rightForeArmToElbowLocal);
            }
        }

        // --- Per-limb delta-based scaling calibration ---
        // Store tracker and bone world positions so ScaleTrackerPosition can compute
        // changes from calibration rather than absolute pelvis-relative vectors.
        // This ensures each limb target is exact at calibration and scales only movement.
        if (leftHandTarget && leftHandBone)
        {
            _calibLeftHandTrackerPos = leftHandTarget.position;
            _calibLeftHandBonePos = leftHandBone.position;
        }
        if (rightHandTarget && rightHandBone)
        {
            _calibRightHandTrackerPos = rightHandTarget.position;
            _calibRightHandBonePos = rightHandBone.position;
        }
        if (leftFootTarget && leftFootBone)
        {
            _calibLeftFootTrackerPos = leftFootTarget.position;
            _calibLeftFootBonePos = leftFootBone.position;
        }
        if (rightFootTarget && rightFootBone)
        {
            _calibRightFootTrackerPos = rightFootTarget.position;
            _calibRightFootBonePos = rightFootBone.position;
        }
        if (leftLegBone) _calibLeftKneeBonePos = leftLegBone.position;
        if (rightLegBone) _calibRightKneeBonePos = rightLegBone.position;

        // Shin-mounted: store derived ankle/knee world positions at calibration
        if (shinMountedTrackers)
        {
            if (leftFootTarget)
            {
                _calibLeftAnkleDerivedPos = leftFootTarget.TransformPoint(_leftShinToAnkleLocal);
                _calibLeftKneeDerivedPos = leftFootTarget.TransformPoint(_leftShinToKneeLocal);
            }
            if (rightFootTarget)
            {
                _calibRightAnkleDerivedPos = rightFootTarget.TransformPoint(_rightShinToAnkleLocal);
                _calibRightKneeDerivedPos = rightFootTarget.TransformPoint(_rightShinToKneeLocal);
            }
        }

        RebuildSpineChainCache();

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

        // Forearm-mounted mode: snap hand targets to forearm midpoint; otherwise snap to hand bone
        if (forearmMountedTrackers)
        {
            if (leftHandTarget != null && leftForeArmBone != null && leftHandBone != null)
            {
                Vector3 foreArmMid = (leftForeArmBone.position + leftHandBone.position) * 0.5f;
                leftHandTarget.SetPositionAndRotation(foreArmMid, leftForeArmBone.rotation);
            }
            if (rightHandTarget != null && rightForeArmBone != null && rightHandBone != null)
            {
                Vector3 foreArmMid = (rightForeArmBone.position + rightHandBone.position) * 0.5f;
                rightHandTarget.SetPositionAndRotation(foreArmMid, rightForeArmBone.rotation);
            }
        }
        else
        {
            SnapTarget(leftHandTarget, leftHandBone);
            SnapTarget(rightHandTarget, rightHandBone);
        }

        // Upper arm trackers (5-tracker arm mode): snap to upper arm midpoint
        if (leftUpperArmTracker != null && leftUpperArmBone != null && leftForeArmBone != null)
        {
            Vector3 upperArmMid = (leftUpperArmBone.position + leftForeArmBone.position) * 0.5f;
            leftUpperArmTracker.SetPositionAndRotation(upperArmMid, leftUpperArmBone.rotation);
        }
        if (rightUpperArmTracker != null && rightUpperArmBone != null && rightForeArmBone != null)
        {
            Vector3 upperArmMid = (rightUpperArmBone.position + rightForeArmBone.position) * 0.5f;
            rightUpperArmTracker.SetPositionAndRotation(upperArmMid, rightUpperArmBone.rotation);
        }

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

        // Delta-based position scaling: take the CHANGE from calibration tracker
        // position, scale it by body proportion ratio, and add to calibration bone
        // position. This ensures the avatar pelvis moves the correct amount
        // regardless of avatar vs player size difference.
        Vector3 pelvisDelta = pelvisTarget.position - _calibPelvisTrackerPos;
        Vector3 targetPos = _calibPelvisBonePos + pelvisDelta * _bodyScale;

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

        // Scaled head target position (accounts for avatar/player size difference)
        Vector3 headDelta = headTarget.position - _calibHeadTrackerPos;
        Vector3 scaledHeadPos = (_calibPelvisBonePos + (_calibHeadTrackerPos - _calibPelvisTrackerPos) * _bodyScale)
                                + headDelta * _bodyScale;

        // --- Iterative CCD-like spine solver ---
        // Process each spine bone from bottom to top. After rotating each bone,
        // re-read the current head bone position so subsequent bones correct
        // the remaining error. This produces a natural distributed bend instead
        // of a single-rotation "stiff" look.
        int chainLen = _spineChainLen;
        if (chainLen == 0) return;

        // Per-bone weight: upper spine bones contribute more than lower ones.
        // Weights sum to spineStiffness.
        for (int i = 0; i < chainLen; i++)
        {
            if (_spineChainCache[i] == null) continue;

            // Re-read current head position after each bone rotation
            Vector3 currentHeadPos = headBone != null ? headBone.position : _spineChainCache[chainLen - 1].position + _spineChainCache[chainLen - 1].up * 0.2f;
            Vector3 bonePos = _spineChainCache[i].position;

            Vector3 boneToCurrentHead = (currentHeadPos - bonePos);
            Vector3 boneToTarget      = (scaledHeadPos  - bonePos);
            if (boneToCurrentHead.sqrMagnitude < 0.0001f || boneToTarget.sqrMagnitude < 0.0001f)
                continue;

            Quaternion correction = Quaternion.FromToRotation(
                boneToCurrentHead.normalized, boneToTarget.normalized);

            // Bottom bones get less weight, top bones get more — natural curve
            float t = (float)(i + 1) / chainLen;  // 0.33, 0.66, 1.0 for 3 bones
            float boneWeight = Mathf.Lerp(0.3f, 1.0f, t) * spineStiffness;
            boneWeight = Mathf.Clamp01(boneWeight);

            _spineChainCache[i].rotation = Quaternion.Slerp(Quaternion.identity, correction, boneWeight)
                                     * _spineChainCache[i].rotation;
        }

        // --- Twist distribution ---
        // The bend pass above handles forward/back/side tilt. Now distribute
        // the axial twist (body turning left/right) from pelvis→head across
        // the spine chain. Without this, turning the torso only moves the head.
        if (headBone != null)
        {
            // Desired head forward from HMD
            Vector3 desiredForward = headTarget.rotation * (_headOffset * Vector3.forward);
            // Current head forward (after bend pass)
            Vector3 currentForward = headBone.rotation * Vector3.forward;

            // Compute spine axis (up direction along the spine)
            Vector3 spineAxis = (headBone.position - hipsBone.position).normalized;
            if (spineAxis.sqrMagnitude < 0.001f) spineAxis = hipsBone.up;

            // Project both forwards onto the plane perpendicular to spine axis
            Vector3 desiredFlat = Vector3.ProjectOnPlane(desiredForward, spineAxis);
            Vector3 currentFlat = Vector3.ProjectOnPlane(currentForward, spineAxis);

            if (desiredFlat.sqrMagnitude > 0.001f && currentFlat.sqrMagnitude > 0.001f)
            {
                float twistAngle = Vector3.SignedAngle(currentFlat, desiredFlat, spineAxis);

                for (int i = 0; i < chainLen; i++)
                {
                    if (_spineChainCache[i] == null) continue;
                    float t = (float)(i + 1) / chainLen;
                    float boneTwist = (twistAngle / chainLen) * t * spineStiffness;
                    _spineChainCache[i].rotation = Quaternion.AngleAxis(boneTwist, spineAxis)
                                             * _spineChainCache[i].rotation;
                }
            }
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

        // ── 5-tracker Direct FK Mode (upper arm tracker + forearm tracker) ──
        Transform upperArmTracker = isLeft ? leftUpperArmTracker : rightUpperArmTracker;
        if (forearmMountedTrackers && upperArmTracker != null)
        {
            SolveArmDirectFK(upperArm, foreArm, hand, target, upperArmTracker, isLeft);
            return;
        }

        // ── Forearm-Mounted Tracker Mode (tracker on forearm, no upper arm tracker) ──
        if (forearmMountedTrackers)
        {
            float upperLen = (foreArm.position - upperArm.position).magnitude;
            float lowerLen = (hand.position - foreArm.position).magnitude;
            float limbLen = Mathf.Max(upperLen + lowerLen, 0.1f);

            // Derive wrist and elbow hint positions from the forearm tracker
            Vector3 wristRaw   = target.TransformPoint(isLeft ? _leftForeArmToWristLocal  : _rightForeArmToWristLocal);
            Vector3 elbowRaw   = target.TransformPoint(isLeft ? _leftForeArmToElbowLocal  : _rightForeArmToElbowLocal);

            Vector3 wristPos = ScaleTrackerPosition(wristRaw,
                isLeft ? _calibLeftWristDerivedPos  : _calibRightWristDerivedPos,
                isLeft ? _calibLeftHandBonePos      : _calibRightHandBonePos);
            Vector3 elbowHintPos = ScaleTrackerPosition(elbowRaw,
                isLeft ? _calibLeftElbowDerivedPos  : _calibRightElbowDerivedPos,
                isLeft ? _calibLeftElbowBonePos     : _calibRightElbowBonePos);

            // Collinearity safety: if arm is nearly straight, the derived elbow hint
            // may lie on the shoulder→wrist axis. Fall back to calibrated bend direction.
            Vector3 shoulderToWrist = wristPos - upperArm.position;
            float swDist = shoulderToWrist.magnitude;
            Vector3 swDir = swDist > 0.001f ? shoulderToWrist / swDist : (isLeft ? -hipsBone.right : hipsBone.right);
            Vector3 perpComp = Vector3.ProjectOnPlane(elbowHintPos - upperArm.position, swDir);
            if (perpComp.magnitude < limbLen * 0.05f)
            {
                Vector3 calibDir = isLeft ? _leftElbowHintDirLocal : _rightElbowHintDirLocal;
                Vector3 hintDir = Vector3.ProjectOnPlane(hipsBone.TransformDirection(calibDir), swDir);
                if (hintDir.sqrMagnitude < 0.001f)
                    hintDir = Vector3.ProjectOnPlane(Vector3.back, swDir);
                elbowHintPos = upperArm.position + swDir * (swDist * 0.5f)
                               + hintDir.normalized * elbowHintDistance;
            }

            TwoBoneIKSolver.Solve(
                upperArm, foreArm, hand,
                wristPos, hand.rotation,
                elbowHintPos,
                armIKWeight, 0f, 1f);

            // Blend forearm bone rotation toward tracker rotation (same as shinRotationBlend for legs)
            if (forearmRotationBlend > 0f)
            {
                Quaternion foreArmOffset = isLeft ? _leftForeArmToForeArmRot : _rightForeArmToForeArmRot;
                Quaternion trackerForeArmRot = target.rotation * foreArmOffset;
                foreArm.rotation = Quaternion.Slerp(foreArm.rotation, trackerForeArmRot,
                    forearmRotationBlend * armIKWeight);
            }

            // Orient hand relative to forearm using calibration offset
            AlignHandToForeArm(foreArm, hand, isLeft ? _leftHandForeArmOffset : _rightHandForeArmOffset);
            return;
        }

        // ── Legacy IK Mode (controller as hand target) ──
        // Scale hand target position for body proportion matching (delta-based)
        Vector3 calibTrackerPos = isLeft ? _calibLeftHandTrackerPos : _calibRightHandTrackerPos;
        Vector3 calibBonePos = isLeft ? _calibLeftHandBonePos : _calibRightHandBonePos;
        Vector3 scaledHandPos = ScaleTrackerPosition(target.position, calibTrackerPos, calibBonePos);

        Vector3 hintPos;
        if (elbowHint != null)
        {
            hintPos = elbowHint.position;
        }
        else if (useControllerRotationForElbow)
        {
            // ── Controller-rotation-based elbow hint ──
            // Derive elbow world position from controller rotation + calibrated
            // controller→elbow direction. This mirrors how the shin tracker derives
            // the knee hint for legs: the tracker knows its own orientation, so we
            // can trust it to tell us where the next joint is.
            float foreArmLen = (hand.position - foreArm.position).magnitude;
            if (foreArmLen < 0.01f) foreArmLen = 0.25f; // fallback if bones are at origin

            Vector3 ctrlElbowDirLocal = isLeft ? _leftControllerToElbowDir : _rightControllerToElbowDir;
            // Rotate calibrated direction by current controller rotation → world-space elbow direction
            Vector3 elbowWorldDir = target.rotation * ctrlElbowDirLocal;

            // Elbow hint = hand position + elbow direction * forearm length
            // (elbow is behind and above the hand, exactly as the controller orientation indicates)
            hintPos = scaledHandPos + elbowWorldDir * foreArmLen;

            // Collinearity safety: if hint ends up nearly on the shoulder→hand axis,
            // fall back to calibrated pelvis-relative direction so IK doesn't flip.
            Vector3 shoulderToHand = (scaledHandPos - upperArm.position);
            float swLen = shoulderToHand.magnitude;
            Vector3 swDir = swLen > 0.001f ? shoulderToHand / swLen : (isLeft ? -hipsBone.right : hipsBone.right);
            Vector3 hintPerp = Vector3.ProjectOnPlane(hintPos - upperArm.position, swDir);
            float totalLimbLen = (foreArm.position - upperArm.position).magnitude + foreArmLen;
            if (hintPerp.magnitude < totalLimbLen * 0.05f)
            {
                Vector3 fallbackDir = hipsBone.TransformDirection(isLeft ? _leftElbowHintDirLocal : _rightElbowHintDirLocal);
                Vector3 fd = Vector3.ProjectOnPlane(fallbackDir, swDir);
                if (fd.sqrMagnitude < 0.001f) fd = Vector3.ProjectOnPlane(Vector3.back, swDir);
                hintPos = upperArm.position + swDir * (swLen * 0.5f) + fd.normalized * elbowHintDistance;
            }
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

            Vector3 rootToHandDir = (scaledHandPos - upperArm.position).normalized;
            if (rootToHandDir.sqrMagnitude < 0.001f) rootToHandDir = isLeft ? -hipsBone.right : hipsBone.right;
            Vector3 midPoint = upperArm.position + rootToHandDir * (limbLen * 0.5f);
            float hintDistance = Mathf.Max(elbowHintDistance, limbLen * 0.6f);
            hintPos = midPoint + hintDir * hintDistance;
        }

        // Target rotation: IK only — hand orientation is derived from forearm direction
        // via AlignHandToForeArm (same as AlignFootToShin for legs). This prevents the
        // controller rotation from fighting the IK result and keeps the wrist naturally
        // aligned with wherever the forearm is pointing.
        TwoBoneIKSolver.Solve(
            upperArm, foreArm, hand,
            scaledHandPos, hand.rotation,
            hintPos,
            armIKWeight, 0f, 1f);

        // Orient hand relative to forearm using calibration offset — same as foot/shin.
        AlignHandToForeArm(foreArm, hand, isLeft ? _leftHandForeArmOffset : _rightHandForeArmOffset);
    }

    /// <summary>
    /// Direct FK mode for 5-tracker arm setup (upper arm tracker + forearm tracker).
    /// Sets bone rotations directly from tracker measurements — no IK needed.
    /// This is the arm equivalent of SolveLegDirectFK.
    /// </summary>
    private void SolveArmDirectFK(Transform upperArm, Transform foreArm, Transform hand,
                                   Transform foreArmTracker, Transform upperArmTrackerTf, bool isLeft)
    {
        // Upper arm rotation directly from upper arm tracker
        Quaternion upperArmOffset = isLeft ? _leftUpperArmToArmRot : _rightUpperArmToArmRot;
        Quaternion targetUpperArmRot = upperArmTrackerTf.rotation * upperArmOffset;
        upperArm.rotation = Quaternion.Slerp(upperArm.rotation, targetUpperArmRot, armIKWeight);

        // Forearm rotation directly from forearm tracker
        Quaternion foreArmOffset = isLeft ? _leftForeArmToForeArmRot : _rightForeArmToForeArmRot;
        Quaternion targetForeArmRot = foreArmTracker.rotation * foreArmOffset;
        foreArm.rotation = Quaternion.Slerp(foreArm.rotation, targetForeArmRot, armIKWeight);

        // Hand follows forearm with calibrated offset
        AlignHandToForeArm(foreArm, hand, isLeft ? _leftHandForeArmOffset : _rightHandForeArmOffset);
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
            // Raw derived positions from tracker
            Vector3 ankleTargetRaw = footTarget.TransformPoint(
                isLeft ? _leftShinToAnkleLocal : _rightShinToAnkleLocal);
            Vector3 kneeHintRaw = footTarget.TransformPoint(
                isLeft ? _leftShinToKneeLocal : _rightShinToKneeLocal);

            // Scale derived positions using delta-based approach for body proportion matching.
            // Each limb target is anchored to its exact calibration bone position;
            // only the movement from calibration is scaled by _bodyScale.
            Vector3 ankleTargetPos = ScaleTrackerPosition(ankleTargetRaw,
                isLeft ? _calibLeftAnkleDerivedPos : _calibRightAnkleDerivedPos,
                isLeft ? _calibLeftFootBonePos : _calibRightFootBonePos);
            Vector3 kneeHintPos = ScaleTrackerPosition(kneeHintRaw,
                isLeft ? _calibLeftKneeDerivedPos : _calibRightKneeDerivedPos,
                isLeft ? _calibLeftKneeBonePos : _calibRightKneeBonePos);

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

            // Blend shin bone rotation toward tracker rotation so the knee
            // angle directly follows the real shin tracker regardless of IK result.
            // This fixes squatting looking like kneeling — the tracker knows the
            // exact shin angle, so we trust it.
            if (shinRotationBlend > 0f)
            {
                Quaternion shinLegOffset = isLeft ? _leftShinToLegRot : _rightShinToLegRot;
                Quaternion trackerLegRot = footTarget.rotation * shinLegOffset;
                leg.rotation = Quaternion.Slerp(leg.rotation, trackerLegRot, shinRotationBlend * legIKWeight);
            }

            AlignFootToShin(leg, foot, isLeft ? _leftFootShinOffset : _rightFootShinOffset);
            return;
        }

        // ── Legacy Ankle-Mounted Mode ──
        // Scale foot target position using delta-based approach for body proportion matching.
        Vector3 calibFootTrackerPos = isLeft ? _calibLeftFootTrackerPos : _calibRightFootTrackerPos;
        Vector3 calibFootBonePos = isLeft ? _calibLeftFootBonePos : _calibRightFootBonePos;
        Vector3 scaledFootPos = ScaleTrackerPosition(footTarget.position, calibFootTrackerPos, calibFootBonePos);

        // Compute hip→foot direction (used for hint stability checks)
        Vector3 hipToFoot = scaledFootPos - upLeg.position;
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
            scaledFootPos, foot.rotation,
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
    /// Delta-based position scaling. Computes the CHANGE from calibration tracker
    /// position, scales it by body proportion ratio, and adds to calibration bone
    /// position. This ensures the result equals calibBonePos exactly at calibration
    /// and correctly handles all tracker mount positions (shin, ankle, wrist, etc.).
    /// </summary>
    private Vector3 ScaleTrackerPosition(Vector3 currentTrackerPos,
                                          Vector3 calibTrackerPos, Vector3 calibBonePos)
    {
        Vector3 delta = currentTrackerPos - calibTrackerPos;
        return calibBonePos + delta * _bodyScale;
    }

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

    /// <summary>
    /// Calibrates forearm-mounted tracker offsets: stores wrist and elbow positions
    /// in tracker-local space and the tracker→forearm-bone rotation offset.
    /// Mirrors CalibrateShinTracker for the arm chain.
    /// </summary>
    private static void CalibrateForeArmTracker(Transform tracker, Transform foreArmBone, Transform handBone,
                                                 ref Vector3 toWristLocal, ref Vector3 toElbowLocal,
                                                 ref Quaternion toForeArmRot)
    {
        if (tracker == null) return;
        if (handBone != null)
            toWristLocal = tracker.InverseTransformPoint(handBone.position);
        if (foreArmBone != null)
        {
            toElbowLocal  = tracker.InverseTransformPoint(foreArmBone.position);
            toForeArmRot  = Quaternion.Inverse(tracker.rotation) * foreArmBone.rotation;
        }
    }

    /// <summary>
    /// Records hand rotation relative to forearm bone at calibration.
    /// At runtime, AlignHandToForeArm reproduces the exact anatomical wrist angle.
    /// Mirrors CalibrateFootShinOffset for the arm chain.
    /// </summary>
    private static void CalibrateHandForeArmOffset(Transform foreArm, Transform hand,
                                                    ref Quaternion offset)
    {
        if (foreArm == null || hand == null) return;
        offset = Quaternion.Inverse(foreArm.rotation) * hand.rotation;
    }

    /// <summary>
    /// Records which direction in controller-local space points from the hand toward
    /// the elbow at calibration time (T-pose). At runtime, rotating this direction by
    /// the controller's current rotation gives the world-space elbow direction.
    ///
    /// This is the arm equivalent of how CalibrateShinTracker records the knee direction
    /// in shin-tracker-local space. The controller "knows" where the forearm is pointing,
    /// so it implicitly tells us where the elbow must be.
    /// </summary>
    private static void CalibrateControllerElbowDir(Transform controller, Transform foreArmBone,
                                                     Transform handBone, ref Vector3 controllerLocalDir)
    {
        if (controller == null || foreArmBone == null || handBone == null) return;

        // Elbow-to-wrist world direction at calibration
        Vector3 elbowToWristWorld = (handBone.position - foreArmBone.position);
        if (elbowToWristWorld.sqrMagnitude < 0.0001f) return;

        // We want: hand → elbow direction = opposite of elbow → wrist
        Vector3 handToElbowWorld = -elbowToWristWorld.normalized;

        // Store in controller-local space so at runtime:
        // elbow_world_dir = controller.rotation * controllerLocalDir
        controllerLocalDir = Quaternion.Inverse(controller.rotation) * handToElbowWorld;
    }

    /// <summary>
    /// Orients the hand bone so it always follows the forearm bone's rotation with
    /// the relative offset recorded at calibration.
    /// Mirrors AlignFootToShin for the arm chain.
    /// </summary>
    private static void AlignHandToForeArm(Transform foreArm, Transform hand, Quaternion foreArmOffset)
    {
        if (foreArm == null || hand == null) return;
        hand.rotation = foreArm.rotation * foreArmOffset;
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

    private void RebuildSpineChainCache()
    {
        _spineChainLen = 0;
        if (spineBone)  _spineChainCache[_spineChainLen++] = spineBone;
        if (spine1Bone) _spineChainCache[_spineChainLen++] = spine1Bone;
        if (spine2Bone) _spineChainCache[_spineChainLen++] = spine2Bone;
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

        // Arm trackers (forearm-mounted / 5-tracker arm mode)
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // translucent cyan for arm trackers
        if (leftUpperArmTracker) Gizmos.DrawWireSphere(leftUpperArmTracker.position, 0.03f);
        if (rightUpperArmTracker) Gizmos.DrawWireSphere(rightUpperArmTracker.position, 0.03f);

        if (forearmMountedTrackers)
        {
            Gizmos.color = Color.magenta;
            if (leftHandTarget)
            {
                Vector3 wristL = leftHandTarget.TransformPoint(_leftForeArmToWristLocal);
                Gizmos.DrawWireSphere(wristL, 0.02f);
                Gizmos.DrawLine(leftHandTarget.position, wristL);
            }
            if (rightHandTarget)
            {
                Vector3 wristR = rightHandTarget.TransformPoint(_rightForeArmToWristLocal);
                Gizmos.DrawWireSphere(wristR, 0.02f);
                Gizmos.DrawLine(rightHandTarget.position, wristR);
            }
        }

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
