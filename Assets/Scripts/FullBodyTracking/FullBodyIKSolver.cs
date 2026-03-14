using UnityEngine;

/// <summary>
/// Full-body IK solver that drives a Mixamo humanoid avatar using:
/// - 1 HMD (head)
/// - 2 Controllers (hands)
/// - 1 Pelvis tracker (waist)
/// - 2 Ankle trackers (feet)
/// - (Optional) 2 Knee trackers (future)
///
/// Uses custom TwoBoneIKSolver for arms and legs.
/// Drives Hips (NOT Spine) as the pelvis bone to fix the known hierarchy bug.
/// </summary>
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

    [Tooltip("Sol ayak bileği tracker")]
    [SerializeField] private Transform leftFootTarget;

    [Tooltip("Sağ ayak bileği tracker")]
    [SerializeField] private Transform rightFootTarget;

    [Header("Opsiyonel Diz Tracker'ları (İleride Eklenebilir)")]
    [Tooltip("Sol diz tracker — atanırsa diz hint pozisyonu olarak kullanılır")]
    [SerializeField] private Transform leftKneeTracker;
    [Tooltip("Sağ diz tracker — atanırsa diz hint pozisyonu olarak kullanılır")]
    [SerializeField] private Transform rightKneeTracker;

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
    private Vector3 _pelvisPosOffset = Vector3.zero;
    private Quaternion _leftFootOffset = Quaternion.identity;
    private Quaternion _rightFootOffset = Quaternion.identity;
    private Quaternion _headOffset = Quaternion.identity;
    private Quaternion _leftHandOffset = Quaternion.identity;
    private Quaternion _rightHandOffset = Quaternion.identity;

    // Initial bone local rotations (model bind pose)
    private Quaternion _hipsInitLocal;
    private Quaternion _spineInitLocal;
    private Quaternion _spine1InitLocal;
    private Quaternion _spine2InitLocal;
    private Quaternion _neckInitLocal;
    private Quaternion _headInitLocal;

    // Avatar height info
    private float _avatarHeight = 1.7f;
    private float _userHeight = 1.7f;
    private float _heightScale = 1f;

    // Calibration version
    public int CalibrationVersion { get; private set; }

    // ───────────────────────── Unity Lifecycle ─────────────────────────

    private void Start()
    {
        CacheInitialBoneRotations();
    }

    private void LateUpdate()
    {
        if (!_calibrated) return;
        if (!hipsBone) return;

        // 1. Pelvis (Hips) — DOĞRU kemik, Spine DEĞİL!
        SolvePelvis();

        // 2. Omurga — HMD ve pelvis arası interpolasyon
        SolveSpine();

        // 3. Baş — HMD takibi
        SolveHead();

        // 4. Kollar — Two-Bone IK
        SolveArm(leftUpperArmBone, leftForeArmBone, leftHandBone,
                 leftHandTarget, isLeft: true);
        SolveArm(rightUpperArmBone, rightForeArmBone, rightHandBone,
                 rightHandTarget, isLeft: false);

        // 5. Bacaklar — Two-Bone IK
        SolveLeg(leftUpLegBone, leftLegBone, leftFootBone,
                 leftFootTarget, leftKneeTracker, _leftFootOffset, isLeft: true);
        SolveLeg(rightUpLegBone, rightLegBone, rightFootBone,
                 rightFootTarget, rightKneeTracker, _rightFootOffset, isLeft: false);
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
            _pelvisPosOffset = hipsBone.position - pelvisTarget.position;
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

        // Height estimation
        if (headTarget)
        {
            _userHeight = headTarget.position.y;
        }
        if (headBone)
        {
            _avatarHeight = headBone.position.y - hipsBone.position.y + (hipsBone.position.y * 0.5f);
            if (_avatarHeight < 0.5f) _avatarHeight = 1.7f; // fallback
        }
        _heightScale = _userHeight / Mathf.Max(_avatarHeight, 0.5f);

        _calibrated = true;
        CalibrationVersion++;
        Debug.Log($"[FullBodyIKSolver] Kalibrasyon tamamlandı. Kullanıcı yüksekliği: {_userHeight:F2}m, " +
                  $"Avatar yüksekliği: {_avatarHeight:F2}m, Ölçek: {_heightScale:F2}");
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

    // ───────────────────────── Solver Methods ─────────────────────────

    private void SolvePelvis()
    {
        if (!pelvisTarget || pelvisWeight <= 0f) return;

        // CRITICAL FIX: Drive Hips (mixamorig1:Hips), NOT Spine!
        // Hips is the parent of both Spine and legs, so rotating Hips
        // correctly rotates the entire body including legs.
        Quaternion targetRot = pelvisTarget.rotation * _pelvisOffset;
        Vector3 targetPos = pelvisTarget.position + pelvisTarget.rotation * _pelvisPosOffset;

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
                          Transform target, bool isLeft)
    {
        if (!upperArm || !foreArm || !hand || !target) return;
        if (armIKWeight <= 0f) return;

        // Calculate elbow hint position
        // Elbows bend backwards (negative forward direction relative to body facing)
        Vector3 bodyForward = hipsBone.forward;
        Vector3 bodySide = isLeft ? -hipsBone.right : hipsBone.right;
        Vector3 elbowBendDir = -bodyForward + (isLeft ? -bodySide : bodySide) * 0.3f - Vector3.up * 0.5f;
        elbowBendDir = elbowBendDir.normalized;

        Vector3 hintPos = TwoBoneIKSolver.CalculateHintPosition(
            upperArm.position, foreArm.position, hand.position,
            elbowBendDir, elbowHintDistance);

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
                          Transform footTarget, Transform kneeTracker,
                          Quaternion footOffset, bool isLeft)
    {
        if (!upLeg || !leg || !foot || !footTarget) return;
        if (legIKWeight <= 0f) return;

        // Knee hint position
        Vector3 hintPos;
        if (kneeTracker != null)
        {
            // If knee tracker is available, use it directly as the hint
            hintPos = kneeTracker.position;
        }
        else
        {
            // Default: knees bend forward
            Vector3 kneeBendDir = hipsBone.forward + Vector3.up * 0.1f;
            hintPos = TwoBoneIKSolver.CalculateHintPosition(
                upLeg.position, leg.position, foot.position,
                kneeBendDir, kneeHintDistance);
        }

        // Target rotation
        Quaternion targetRot = applyTargetRotation ? footTarget.rotation * footOffset : foot.rotation;

        TwoBoneIKSolver.Solve(
            upLeg, leg, foot,
            footTarget.position, targetRot,
            hintPos,
            legIKWeight, applyTargetRotation ? legIKWeight : 0f, 1f);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void CacheInitialBoneRotations()
    {
        if (hipsBone) _hipsInitLocal = hipsBone.localRotation;
        if (spineBone) _spineInitLocal = spineBone.localRotation;
        if (spine1Bone) _spine1InitLocal = spine1Bone.localRotation;
        if (spine2Bone) _spine2InitLocal = spine2Bone.localRotation;
        if (neckBone) _neckInitLocal = neckBone.localRotation;
        if (headBone) _headInitLocal = headBone.localRotation;
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

        Gizmos.color = Color.yellow;
        if (leftKneeTracker) Gizmos.DrawWireSphere(leftKneeTracker.position, 0.03f);
        if (rightKneeTracker) Gizmos.DrawWireSphere(rightKneeTracker.position, 0.03f);

        // Draw bone chain
        if (hipsBone && headBone)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(hipsBone.position, headBone.position);
        }
    }
#endif
}
