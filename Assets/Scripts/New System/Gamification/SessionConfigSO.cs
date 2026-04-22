using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject — bir terapi/değerlendirme oturumunun tam yapılandırması.
///
/// Oluşturma: Assets → Create → Gamification → Session Config
///
/// Workflow:
///   1. Her hasta/protokol için ayrı bir SessionConfigSO asset'i oluştur
///      (ör: "Standart_ACL_Protokolu", "Haftalk_Takip", "Kısa_Tarama").
///   2. tasks listesine istediğin TaskDefinition SO'ları ekle.
///   3. TaskSequencer.sessionConfig alanına bu SO'yu ata.
///   4. Play'de TaskSequencer, oturumu bu yapılandırmadan otomatik yükler.
/// </summary>
[CreateAssetMenu(fileName = "Session_Standart", menuName = "Gamification/Session Config", order = 12)]
public class SessionConfigSO : ScriptableObject
{
    // ═══════════════════════════════════════════════════════════════
    // IDENTITY
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Identity ===")]
    [Tooltip("Oturum / protokol adı (klinisyen için, ör: 'Standart ACL Değerlendirmesi').")]
    public string sessionName = "Standart Protokol";

    [Tooltip("Oturum açıklaması (Türkçe, klinisyen notu).")]
    [TextArea(2, 5)]
    public string descriptionTR = "Alt ekstremite değerlendirme protokolü.";

    // ═══════════════════════════════════════════════════════════════
    // TASK LIST
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Görev Listesi ===")]
    [Tooltip("Sırayla yürütülecek görevler. Her görev bir TaskDefinition ScriptableObject'idir.")]
    public List<TaskDefinition> tasks = new List<TaskDefinition>();

    // ═══════════════════════════════════════════════════════════════
    // OPTIONS
    // ═══════════════════════════════════════════════════════════════

    [Header("=== Oturum Seçenekleri ===")]

    [Tooltip("Her görevi kaç kez tekrarla. 1 = yalnızca bir kez.\n" +
             "Örn: 3 → her görev arka arkaya 3 kez yapılır.")]
    [Min(1)]
    public int repetitionsPerTask = 1;

    [Tooltip("Tüm görevler için global dinlenme süresi (sn). -1 = her görevin kendi restAfterSeconds ayarı kullanılır.")]
    public float globalRestOverrideSeconds = -1f;

    [Tooltip("Görev sırasını her oturumda rastgele karıştır.")]
    public bool randomizeOrder = false;

    [Tooltip("Görevleri klinik tarama için önerilen sıraya göre otomatik diz. Manual listeden bağımsız olarak BuildExecutionList() öncesi uygulanır.")]
    public bool autoSortByClinicalRecommendation = true;

    [Tooltip("Oturum başında kalibrasyon ekranını zorla (false = kalibrasyon zaten yapılmışsa atla).")]
    public bool forceRecalibrationOnStart = false;

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME HELPER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tekrar sayısı, sıralama ve global rest override uygulanmış
    /// çalışma listesini (flat execution list) döndürür.
    /// TaskSequencer.StartSession() tarafından çağrılır.
    /// </summary>
    public List<TaskDefinition> BuildExecutionList()
    {
        var sourceTasks = autoSortByClinicalRecommendation
            ? SortTasksByClinicalRecommendation(tasks)
            : new List<TaskDefinition>(tasks);

        var list = new List<TaskDefinition>();

        foreach (var task in sourceTasks)
        {
            if (task == null) continue;

            for (int r = 0; r < Mathf.Max(1, repetitionsPerTask); r++)
                list.Add(PrepareExecutionTask(task));
        }

        if (randomizeOrder)
        {
            // Fisher–Yates shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        return list;
    }

    private TaskDefinition PrepareExecutionTask(TaskDefinition source)
    {
        if (source == null || globalRestOverrideSeconds < 0f)
            return source;

        var runtimeTask = Instantiate(source);
        runtimeTask.name = source.name;
        runtimeTask.hideFlags = HideFlags.DontSave;
        runtimeTask.restAfterSeconds = globalRestOverrideSeconds;
        return runtimeTask;
    }

    public static List<TaskDefinition> SortTasksByClinicalRecommendation(List<TaskDefinition> source)
    {
        var ordered = source != null ? new List<TaskDefinition>(source) : new List<TaskDefinition>();
        ordered.Sort((a, b) => GetClinicalRecommendationOrder(a).CompareTo(GetClinicalRecommendationOrder(b)));
        return ordered;
    }

    public static int GetClinicalRecommendationOrder(TaskDefinition task)
    {
        if (task == null) return int.MaxValue;
        return GetClinicalRecommendationOrder(task.taskType);
    }

    public static int GetClinicalRecommendationOrder(TaskType taskType)
    {
        switch (taskType)
        {
            case TaskType.Standing:                   return 10;
            case TaskType.LandingScreen:              return 20;
            case TaskType.MiniSquat:                  return 30;
            case TaskType.SingleLegSquat_R:           return 40;
            case TaskType.SingleLegSquat_L:           return 50;
            case TaskType.ModifiedYBalanceAnterior_R: return 60;
            case TaskType.ModifiedYBalanceAnterior_L: return 70;
            case TaskType.LeanRight:                  return 80;
            case TaskType.LeanLeft:                   return 90;
            case TaskType.LeanForward:                return 100;
            case TaskType.SingleLegBalance_R:         return 110;
            case TaskType.SingleLegBalance_L:         return 120;
            case TaskType.WalkSimulation:             return 130;
            default:                                  return 999;
        }
    }
}
