using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject — bir görev için sıralı PoseSnapshotSO keyframe listesi.
/// Ghost avatar bu sekansı döngüde oynatır.
///
/// Oluşturma: Assets → Create → Gamification → Pose Sequence
///
/// Workflow:
///   1. Her keyframe için bir PoseSnapshotSO asset'i oluştur
///      (PoseDemoController Inspector'ındaki "► Keyframe Ekle" butonu bunu otomatik yapar).
///   2. PoseSnapshotSO'ları keyframes listesine sürükle-bırak.
///   3. Bu PoseSequenceSO'yu ilgili TaskDefinition.demoSequence alanına ata.
/// </summary>
[CreateAssetMenu(fileName = "PoseSeq_New", menuName = "Gamification/Pose Sequence", order = 11)]
public class PoseSequenceSO : ScriptableObject
{
    [Header("=== Identity ===")]
    [Tooltip("Sekans adı (ör: 'MiniSquat_Demo', 'LeanForward_Demo').")]
    public string sequenceName = "Yeni Sekans";

    [Tooltip("Sekans açıklaması (klinisyen notu, opsiyonel).")]
    [TextArea(1, 3)]
    public string descriptionTR = "";

    [Header("=== Playback ===")]
    [Tooltip("Sekans bitince başa dönsün mü? Görev countdown/ölçüm süresince döngü yapılır.")]
    public bool loop = true;

    [Tooltip("Keyframeler arası geçiş hızı (Slerp rate). -1 = PoseDemoController.transitionSpeed kullan.")]
    public float transitionSpeedOverride = -1f;

    [Header("=== Keyframes ===")]
    [Tooltip("Sırayla oynatılacak poz keyframeleri. Boş referanslar atlanır.")]
    public List<PoseSnapshotSO> keyframes = new List<PoseSnapshotSO>();

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Null olmayan ilk keyframe'i döndürür. Yoksa null.</summary>
    public PoseSnapshotSO FirstValidKeyframe()
    {
        foreach (var kf in keyframes)
            if (kf != null) return kf;
        return null;
    }

    /// <summary>Null olmayan keyframe sayısını döndürür.</summary>
    public int ValidCount()
    {
        int c = 0;
        foreach (var kf in keyframes) if (kf != null) c++;
        return c;
    }
}
