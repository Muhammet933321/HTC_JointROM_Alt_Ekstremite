using UnityEngine;

/// <summary>
/// ScriptableObject that defines a single gamification task.
/// Create via Assets → Create → Gamification → Task Definition.
/// </summary>
[CreateAssetMenu(fileName = "Task_New", menuName = "Gamification/Task Definition", order = 1)]
public class TaskDefinition : ScriptableObject
{
    [Header("=== Identity ===")]
    public TaskType taskType = TaskType.Standing;

    [Tooltip("Türkçe görev adı (kullanıcıya gösterilir).")]
    public string taskNameTR = "Yeni Görev";

    [Tooltip("Kullanıcıya verilecek talimat metni.")]
    [TextArea(2, 4)]
    public string instructionsTR = "Lütfen dik durun.";

    [Header("=== Timing ===")]
    [Tooltip("Ölçüm süresi (saniye). 0 = sonsuz (manuel sonlandırma).")]
    public float durationSeconds = 10f;

    [Tooltip("Görev bitmeden önce kullanıcıya gösterilen geri sayım süresi (saniye).")]
    public float countdownSeconds = 3f;

    [Tooltip("Bir sonraki göreve geçmeden önce dinlenme süresi (saniye).")]
    public float restAfterSeconds = 3f;

    [Header("=== Risk Thresholds (PDF §7) ===")]
    [Tooltip("Dinamik diz valgusu için risk eşiği (derece). PDF §7: >8° = ACL risk göstergesi.")]
    public float valgusThresholdDeg = 8f;

    [Tooltip("Simetri indeksi eşiği (%). PDF §7: >%10 = anlamlı asimetri.")]
    public float asymmetryThresholdPct = 10f;

    [Tooltip("Minimum diz fleksiyonu (derece). PDF §7: <45° = hareket kısıtlılığı.")]
    public float minFlexionDeg = 45f;

    [Tooltip("Sway RMS eşiği (metre). Varsayılan 0.015 m = 15 mm.")]
    public float swayRmsThreshold = 0.015f;

    [Header("=== Demo Animation (ScriptableObject) ===")]
    [Tooltip("Ghost avatar demo animasyon sekansı.\n" +
             "PoseSequenceSO asset'ini buraya sürükle-bırak.\n" +
             "Boş bırakılırsa dahili Euler fallback library kullanılır.")]
    public PoseSequenceSO demoSequence;

    [Tooltip("Demo geçiş hızı geçersiz kılması (yüksek = hızlı Slerp). " +
             "-1 = PoseDemoController.transitionSpeed kullan. " +
             "PoseSequenceSO.transitionSpeedOverride > 0 ise o önceliklidir.")]
    public float demoTransitionSpeedOverride = -1f;
}
