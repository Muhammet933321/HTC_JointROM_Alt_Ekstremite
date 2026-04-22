using UnityEngine;

/// <summary>
/// ScriptableObject — tek bir tam-vucut keyframe.
///
/// Oluşturma: Assets → Create → Gamification → Pose Snapshot
///
/// Workflow:
///   1. Ghost avatar kemiği Scene view'de istediğin pozisyona getir.
///   2. PoseDemoController Inspector'ındaki "► Keyframe Ekle" butonu
///      otomatik olarak bu SO'yu oluşturur ve doldurur.
///   3. SO asset'ini bir PoseSequenceSO içine sürükle-bırak.
/// </summary>
[CreateAssetMenu(fileName = "Pose_New", menuName = "Gamification/Pose Snapshot", order = 10)]
public class PoseSnapshotSO : ScriptableObject
{
    private static readonly Vector4 IdentityVector = new Vector4(0f, 0f, 0f, 1f);

    // ═══════════════════════════════════════════════════════════════
    // IDENTITY
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Identity ===")]
    [Tooltip("Bu poz için açıklayıcı ad (ör: 'MiniSquat_Çömel', 'Dik_Duruş').")]
    public string poseName = "Yeni Poz";

    [Tooltip("Klinisyen notları / açıklama (Türkçe, opsiyonel).")]
    [TextArea(1, 3)]
    public string descriptionTR = "";

    // ═══════════════════════════════════════════════════════════════
    // TIMING
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Timing ===")]
    [Tooltip("Bu pozu uyguladıktan sonra beklenecek süre (saniye). Nötr poz için 0 bırak.")]
    public float holdSeconds = 1.5f;

    // ═══════════════════════════════════════════════════════════════
    // BONE DATA  (Quaternion → Vector4 olarak saklanır: Unity Vector4'ü serialize eder)
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Hip Transform ===")]
    [Tooltip("Hips kemiğinin localPosition (aşağı/yukarı offset — çömelme gibi).")]
    public Vector3 hipPosition;

    [Tooltip("Hips kemiğinin localRotation (x,y,z,w).")]
    public Vector4 hip = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Spine Chain ===")]
    [Tooltip("mixamorig:Spine kemiği yakalandiysa aktif.")]
    public bool hasLowerSpine;
    public Vector4 lowerSpine = new Vector4(0f, 0f, 0f, 1f);

    [Tooltip("mixamorig:Spine1 kemiğinin localRotation.")]
    public Vector4 spine = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Upper Torso (optional) ===")]
    [Tooltip("mixamorig:Spine2 / chest kemigi yakalandiysa aktif.")]
    public bool hasChest;
    public Vector4 chest = new Vector4(0f, 0f, 0f, 1f);

    [Tooltip("Neck kemigi yakalandiysa aktif.")]
    public bool hasNeck;
    public Vector4 neck = new Vector4(0f, 0f, 0f, 1f);

    [Tooltip("Head kemigi yakalandiysa aktif.")]
    public bool hasHead;
    public Vector4 head = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Left Leg ===")]
    public Vector4 thighLeft = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 shinLeft = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 ankleLeft = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Right Leg ===")]
    public Vector4 thighRight = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 shinRight = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 ankleRight = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Left Arm (optional) ===")]
    public bool hasLeftShoulder;
    public Vector4 shoulderLeft = new Vector4(0f, 0f, 0f, 1f);

    public bool hasLeftUpperArm;
    public Vector4 upperArmLeft = new Vector4(0f, 0f, 0f, 1f);

    public bool hasLeftForearm;
    public Vector4 forearmLeft = new Vector4(0f, 0f, 0f, 1f);

    public bool hasLeftHand;
    public Vector4 handLeft = new Vector4(0f, 0f, 0f, 1f);

    [Header("=== Right Arm (optional) ===")]
    public bool hasRightShoulder;
    public Vector4 shoulderRight = new Vector4(0f, 0f, 0f, 1f);

    public bool hasRightUpperArm;
    public Vector4 upperArmRight = new Vector4(0f, 0f, 0f, 1f);

    public bool hasRightForearm;
    public Vector4 forearmRight = new Vector4(0f, 0f, 0f, 1f);

    public bool hasRightHand;
    public Vector4 handRight = new Vector4(0f, 0f, 0f, 1f);

    // ═══════════════════════════════════════════════════════════════
    // QUATERNION ACCESSORS
    // ═══════════════════════════════════════════════════════════════

    public Quaternion HipQ        => ToQ(hip);
    public Quaternion LowerSpineQ => ToQ(lowerSpine);
    public Quaternion SpineQ      => ToQ(spine);
    public Quaternion ChestQ      => ToQ(chest);
    public Quaternion NeckQ       => ToQ(neck);
    public Quaternion HeadQ       => ToQ(head);
    public Quaternion ThighLeftQ  => ToQ(thighLeft);
    public Quaternion ThighRightQ => ToQ(thighRight);
    public Quaternion ShinLeftQ   => ToQ(shinLeft);
    public Quaternion ShinRightQ  => ToQ(shinRight);
    public Quaternion AnkleLeftQ  => ToQ(ankleLeft);
    public Quaternion AnkleRightQ => ToQ(ankleRight);
    public Quaternion ShoulderLeftQ  => ToQ(shoulderLeft);
    public Quaternion ShoulderRightQ => ToQ(shoulderRight);
    public Quaternion UpperArmLeftQ  => ToQ(upperArmLeft);
    public Quaternion UpperArmRightQ => ToQ(upperArmRight);
    public Quaternion ForearmLeftQ   => ToQ(forearmLeft);
    public Quaternion ForearmRightQ  => ToQ(forearmRight);
    public Quaternion HandLeftQ      => ToQ(handLeft);
    public Quaternion HandRightQ     => ToQ(handRight);

    public static Quaternion ToQ(Vector4 v) => new Quaternion(v.x, v.y, v.z, v.w);
    public static Vector4    ToV(Quaternion q) => new Vector4(q.x, q.y, q.z, q.w);

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME HELPERS  (Editor ve runtime her ikisinde çalışır)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verilen kemiklerin mevcut localRotation + hipPosition değerlerini bu SO'ya yazar.
    /// Editor'dan çağrıldıktan sonra <c>EditorUtility.SetDirty(this)</c> çağır.
    /// </summary>
    public void CaptureFrom(
        Transform hipBone, Transform lowerSpineBone, Transform spineBone,
        Transform chestBone, Transform neckBone, Transform headBone,
        Transform lShoulder, Transform rShoulder,
        Transform lUpperArm, Transform rUpperArm,
        Transform lForearm, Transform rForearm,
        Transform lHand, Transform rHand,
        Transform lThigh, Transform rThigh,
        Transform lShin, Transform rShin,
        Transform lAnkle, Transform rAnkle)
    {
        hipPosition = hipBone ? hipBone.localPosition : Vector3.zero;
        hip = hipBone ? ToV(hipBone.localRotation) : IdentityVector;
        hasLowerSpine = lowerSpineBone != null;
        lowerSpine = hasLowerSpine ? ToV(lowerSpineBone.localRotation) : IdentityVector;
        spine = spineBone ? ToV(spineBone.localRotation) : IdentityVector;

        hasChest = chestBone != null;
        chest = hasChest ? ToV(chestBone.localRotation) : IdentityVector;

        hasNeck = neckBone != null;
        neck = hasNeck ? ToV(neckBone.localRotation) : IdentityVector;

        hasHead = headBone != null;
        head = hasHead ? ToV(headBone.localRotation) : IdentityVector;

        hasLeftShoulder = lShoulder != null;
        shoulderLeft = hasLeftShoulder ? ToV(lShoulder.localRotation) : IdentityVector;

        hasRightShoulder = rShoulder != null;
        shoulderRight = hasRightShoulder ? ToV(rShoulder.localRotation) : IdentityVector;

        hasLeftUpperArm = lUpperArm != null;
        upperArmLeft = hasLeftUpperArm ? ToV(lUpperArm.localRotation) : IdentityVector;

        hasRightUpperArm = rUpperArm != null;
        upperArmRight = hasRightUpperArm ? ToV(rUpperArm.localRotation) : IdentityVector;

        hasLeftForearm = lForearm != null;
        forearmLeft = hasLeftForearm ? ToV(lForearm.localRotation) : IdentityVector;

        hasRightForearm = rForearm != null;
        forearmRight = hasRightForearm ? ToV(rForearm.localRotation) : IdentityVector;

        hasLeftHand = lHand != null;
        handLeft = hasLeftHand ? ToV(lHand.localRotation) : IdentityVector;

        hasRightHand = rHand != null;
        handRight = hasRightHand ? ToV(rHand.localRotation) : IdentityVector;

        thighLeft = lThigh ? ToV(lThigh.localRotation) : IdentityVector;
        thighRight = rThigh ? ToV(rThigh.localRotation) : IdentityVector;
        shinLeft = lShin ? ToV(lShin.localRotation) : IdentityVector;
        shinRight = rShin ? ToV(rShin.localRotation) : IdentityVector;
        ankleLeft = lAnkle ? ToV(lAnkle.localRotation) : IdentityVector;
        ankleRight = rAnkle ? ToV(rAnkle.localRotation) : IdentityVector;
    }

    /// <summary>
    /// Bu SO'nun verilerini doğrudan kemik transform'larına uygular (anlık, Lerp yok).
    /// </summary>
    public void ApplyTo(
        Transform hipBone, Transform lowerSpineBone, Transform spineBone,
        Transform chestBone, Transform neckBone, Transform headBone,
        Transform lShoulder, Transform rShoulder,
        Transform lUpperArm, Transform rUpperArm,
        Transform lForearm, Transform rForearm,
        Transform lHand, Transform rHand,
        Transform lThigh, Transform rThigh,
        Transform lShin, Transform rShin,
        Transform lAnkle, Transform rAnkle)
    {
        if (hipBone) { hipBone.localRotation = HipQ; hipBone.localPosition = hipPosition; }
        ApplyOptional(lowerSpineBone, hasLowerSpine, LowerSpineQ);
        if (spineBone) spineBone.localRotation = SpineQ;

        ApplyOptional(chestBone, hasChest, ChestQ);
        ApplyOptional(neckBone, hasNeck, NeckQ);
        ApplyOptional(headBone, hasHead, HeadQ);
        ApplyOptional(lShoulder, hasLeftShoulder, ShoulderLeftQ);
        ApplyOptional(rShoulder, hasRightShoulder, ShoulderRightQ);
        ApplyOptional(lUpperArm, hasLeftUpperArm, UpperArmLeftQ);
        ApplyOptional(rUpperArm, hasRightUpperArm, UpperArmRightQ);
        ApplyOptional(lForearm, hasLeftForearm, ForearmLeftQ);
        ApplyOptional(rForearm, hasRightForearm, ForearmRightQ);
        ApplyOptional(lHand, hasLeftHand, HandLeftQ);
        ApplyOptional(rHand, hasRightHand, HandRightQ);

        if (lThigh) lThigh.localRotation = ThighLeftQ;
        if (rThigh) rThigh.localRotation = ThighRightQ;
        if (lShin) lShin.localRotation = ShinLeftQ;
        if (rShin) rShin.localRotation = ShinRightQ;
        if (lAnkle) lAnkle.localRotation = AnkleLeftQ;
        if (rAnkle) rAnkle.localRotation = AnkleRightQ;
    }

    private static void ApplyOptional(Transform bone, bool hasRotation, Quaternion rotation)
    {
        if (bone != null && hasRotation)
            bone.localRotation = rotation;
    }
}
