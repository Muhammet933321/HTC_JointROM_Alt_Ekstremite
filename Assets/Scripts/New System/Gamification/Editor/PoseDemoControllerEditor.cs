#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoseDemoController))]
public class PoseDemoControllerEditor : Editor
{
    private TaskType _selectedTask = TaskType.Standing;
    private float    _holdSeconds  = 1.5f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var demo = (PoseDemoController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("=== Pose Capture (edit mod) ===", EditorStyles.boldLabel);

        _selectedTask = (TaskType)EditorGUILayout.EnumPopup("Görev", _selectedTask);
        _holdSeconds  = EditorGUILayout.FloatField("Hold Süresi (sn)", _holdSeconds);

        var seq   = demo.GetCapturedSequence(_selectedTask);
        int count = seq?.keyframes.Count ?? 0;
        EditorGUILayout.LabelField($"Kayıtlı keyframe sayısı: {count}", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        // ── Capture ──────────────────────────────────────────────
        if (GUILayout.Button("► Keyframe Ekle  (şu anki poz)"))
        {
            Undo.RecordObject(target, "Add Keyframe");
            var snap = demo.CaptureCurrentPose(_holdSeconds);
            demo.GetOrCreateSequence(_selectedTask).keyframes.Add(snap);
            EditorUtility.SetDirty(target);
            Debug.Log($"[PoseCapture] {_selectedTask} — keyframe {count + 1} eklendi.");
        }

        // ── Delete last ──────────────────────────────────────────
        GUI.enabled = count > 0;
        if (GUILayout.Button("◄ Son Keyframe Sil"))
        {
            Undo.RecordObject(target, "Remove Last Keyframe");
            seq.keyframes.RemoveAt(seq.keyframes.Count - 1);
            EditorUtility.SetDirty(target);
        }
        GUI.enabled = true;

        // ── Clear sequence ───────────────────────────────────────
        if (count > 0 && GUILayout.Button("✕  Tüm Sequenceı Temizle"))
        {
            if (EditorUtility.DisplayDialog("Emin misin?",
                    $"{_selectedTask} için {count} keyframe silinecek.",
                    "Evet, sil", "İptal"))
            {
                Undo.RecordObject(target, "Clear Sequence");
                seq.keyframes.Clear();
                EditorUtility.SetDirty(target);
            }
        }

        // ── Preview keyframes ────────────────────────────────────
        if (count > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Önizleme (kemikleri doğrudan uygular):", EditorStyles.miniBoldLabel);
            for (int i = 0; i < count; i++)
            {
                if (GUILayout.Button($"  Keyframe {i + 1} Önizle"))
                {
                    RecordBones(demo, $"Preview Keyframe {i + 1}");
                    demo.ApplySnapshot(seq.keyframes[i]);
                    SceneView.RepaintAll();
                }
            }
        }

        // ── Reset to neutral ─────────────────────────────────────
        EditorGUILayout.Space(4);
        if (!demo.neutralCaptured)
            EditorGUILayout.HelpBox("⚠ Nötr poz henüz kaydedilmedi. Önce aşağıdaki '⊙ Nötr Kaydet' butonuna bas.", MessageType.Warning);

        GUI.enabled = demo.neutralCaptured;
        if (GUILayout.Button("↺  Nötr Poza Döndür"))
        {
            RecordBones(demo, "Reset to Neutral");
            demo.ApplyNeutral();
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        // ── Capture neutral ───────────────────────────────────────
        if (GUILayout.Button("⊙  Şu Anki Pozu Nötr Olarak Kaydet"))
        {
            if (EditorUtility.DisplayDialog("Nötr Pozu Güncelle",
                    "Kemiklerin şu anki konumu ve rotasyonu nötr poz olarak kaydedilecek.\n"
                    + "Keyframe önizlemeden sonra bunu kullanarak orijinal duruşa dönebilirsin.",
                    "Evet", "İptal"))
            {
                Undo.RecordObject(target, "Capture Neutral Pose");
                demo.neutralSnapshot = demo.CaptureCurrentPose(0f);
                demo.neutralCaptured = true;
                EditorUtility.SetDirty(target);
                Debug.Log("[PoseCapture] Nötr poz kaydedildi.");
            }
        }
    }

    // Undo-records every bone Transform before a preview operation
    private static void RecordBones(PoseDemoController demo, string label)
    {
        var bones = new Object[]
        {
            demo.hipBone, demo.spineBone,
            demo.leftThighBone, demo.rightThighBone,
            demo.leftShinBone, demo.rightShinBone,
            demo.leftAnkleBone, demo.rightAnkleBone
        };
        foreach (var b in bones)
            if (b != null) Undo.RecordObject(b, label);
    }
}
#endif
