#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Custom Inspector for PoseDemoController.
/// ScriptableObject tabanlı poz kayıt arayüzü.
///
/// Kullanım:
///   1. Hedef PoseSequenceSO'yu "Hedef Sekans" alanına ata veya yeni oluştur.
///   2. Ghost avatar kemiklerini Scene view'de pozisyona getir.
///   3. "► Keyframe Ekle" → PoseSnapshotSO asset'i oluşturulur ve sekansa eklenir.
///      Kaydedilen snapshot; omurga, clavicle, üst kol, ön kol ve el kemiklerini de içerir.
///   4. "↺ Nötr Poza Döndür" → daha önce "⊙ Nötr Kaydet" ile kaydedilen poza döner.
/// </summary>
[CustomEditor(typeof(PoseDemoController))]
public class PoseDemoControllerEditor : Editor
{
    // Seçili PoseSequenceSO (keyframe ekleme hedefi)
    private PoseSequenceSO _targetSequence;
    private float          _holdSeconds  = 1.5f;
    private string         _snapName     = "";

    private const string SnapshotDir = "Assets/Gamification/Poses/Snapshots";
    private const string SequenceDir = "Assets/Gamification/Poses/Sequences";
    private const string NeutralDir  = "Assets/Gamification/Poses/Neutral";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var demo = (PoseDemoController)target;

        // ═══════════════════════════════════════════════════════════
        // POZ KAYIT BÖLÜMÜ
        // ═══════════════════════════════════════════════════════════
        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("=== Poz Kayıt (edit mod) ===", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Kaydedilen keyframe; üst omurga, boyun, baş ve iki kol zincirini de içerir. " +
            "Bu sayede yana eğilmelerde kolları ve omurgayı ayrıca düzeltebilirsin.",
            MessageType.Info);

        // ── Hedef PoseSequenceSO seçimi ───────────────────────────
        EditorGUILayout.LabelField("Keyframe eklenecek sekans:", EditorStyles.miniBoldLabel);

        _targetSequence = (PoseSequenceSO)EditorGUILayout.ObjectField(
            "Hedef PoseSequenceSO", _targetSequence, typeof(PoseSequenceSO), false);

        if (GUILayout.Button("+ Yeni PoseSequenceSO Oluştur"))
        {
            EnsureDirectory(SequenceDir);
            var path = AssetDatabase.GenerateUniqueAssetPath(SequenceDir + "/PoseSeq_New.asset");
            var newSeq = CreateInstance<PoseSequenceSO>();
            newSeq.sequenceName = "Yeni Sekans";
            AssetDatabase.CreateAsset(newSeq, path);
            AssetDatabase.SaveAssets();
            _targetSequence = newSeq;
            EditorGUIUtility.PingObject(newSeq);
            Debug.Log($"[PoseCapture] Yeni PoseSequenceSO oluşturuldu: {path}");
        }

        if (_targetSequence == null)
        {
            EditorGUILayout.HelpBox(
                "Keyframe eklemek için bir PoseSequenceSO seçin veya yukarıdan yeni oluşturun.",
                MessageType.Info);
        }
        else
        {
            int count = _targetSequence.ValidCount();
            EditorGUILayout.LabelField(
                $"Sekans: \"{_targetSequence.sequenceName}\"  |  Geçerli keyframe: {count}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            _holdSeconds = EditorGUILayout.FloatField("Hold Süresi (sn)", _holdSeconds);
            _snapName    = EditorGUILayout.TextField("Poz Adı (opsiyonel)", _snapName);

            EditorGUILayout.Space(4);

            // ── Keyframe Ekle ─────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("► Keyframe Ekle  (şu anki poz)"))
                {
                    EnsureDirectory(SnapshotDir);

                    string baseName = string.IsNullOrWhiteSpace(_snapName)
                        ? $"{_targetSequence.sequenceName}_KF{count + 1}"
                        : _snapName.Trim();

                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                        SnapshotDir + "/" + baseName + ".asset");

                    var snap = CreateInstance<PoseSnapshotSO>();
                    snap.poseName    = baseName;
                    snap.holdSeconds = _holdSeconds;
                    demo.CaptureIntoPoseSnapshotSO(snap);          // kemiklerden yakala

                    AssetDatabase.CreateAsset(snap, assetPath);

                    Undo.RecordObject(_targetSequence, "Add Pose Keyframe");
                    _targetSequence.keyframes.Add(snap);
                    EditorUtility.SetDirty(_targetSequence);

                    AssetDatabase.SaveAssets();
                    _snapName = "";

                    Debug.Log($"[PoseCapture] '{baseName}' oluşturuldu ve '{_targetSequence.sequenceName}' sekansına eklendi.");
                    EditorGUIUtility.PingObject(snap);
                }
                GUI.backgroundColor = Color.white;
            }

            // ── Son Keyframe Sil ──────────────────────────────────
            GUI.enabled = _targetSequence.keyframes.Count > 0;
            if (GUILayout.Button("◄ Son Keyframe'i Sekandan Çıkar (SO silinmez)"))
            {
                Undo.RecordObject(_targetSequence, "Remove Last Keyframe");
                _targetSequence.keyframes.RemoveAt(_targetSequence.keyframes.Count - 1);
                EditorUtility.SetDirty(_targetSequence);
            }
            GUI.enabled = true;

            // ── Keyframe Önizle ───────────────────────────────────
            if (count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Önizleme (kemikleri anında uygular, Undo destekli):",
                    EditorStyles.miniBoldLabel);

                for (int i = 0; i < _targetSequence.keyframes.Count; i++)
                {
                    var kf = _targetSequence.keyframes[i];
                    if (kf == null)
                    {
                        EditorGUILayout.LabelField($"  [{i}] — (boş referans)", EditorStyles.miniLabel);
                        continue;
                    }
                    string label = string.IsNullOrWhiteSpace(kf.poseName) ? $"Keyframe {i}" : kf.poseName;
                    if (GUILayout.Button($"  [{i}] {label}  Önizle"))
                    {
                        RecordBones(demo, $"Preview {label}");
                        demo.ApplySnapshotSO(kf);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // NÖTR POZ BÖLÜMÜ
        // ═══════════════════════════════════════════════════════════
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("=== Nötr Poz ===", EditorStyles.boldLabel);

        if (demo.neutralPose == null)
            EditorGUILayout.HelpBox(
                "⚠ Nötr poz atanmamış.\nKemikleri dik duruşa al ve aşağıdaki '⊙ Kaydet' butonuna bas.",
                MessageType.Warning);
        else
            EditorGUILayout.LabelField($"Atanmış: {demo.neutralPose.poseName}", EditorStyles.miniLabel);

        // ── Nötr Kaydet ───────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            if (GUILayout.Button("⊙  Şu Anki Pozu Nötr Olarak Kaydet"))
            {
                if (EditorUtility.DisplayDialog("Nötr Poz Kaydet",
                    "Kemiklerin şu anki konum ve rotasyonları nötr poz olarak kaydedilecek.\n" +
                    "Üst omurga ve kollar da bu nötr poza dahil edilir.",
                        "Evet", "İptal"))
                {
                    EnsureDirectory(NeutralDir);

                    if (demo.neutralPose != null)
                    {
                        // Mevcut SO'yu güncelle
                        Undo.RecordObject(demo.neutralPose, "Update Neutral Pose");
                        demo.CaptureIntoPoseSnapshotSO(demo.neutralPose);
                        EditorUtility.SetDirty(demo.neutralPose);
                    }
                    else
                    {
                        // Yeni SO oluştur
                        string path = AssetDatabase.GenerateUniqueAssetPath(NeutralDir + "/Pose_Neutral.asset");
                        var neutral = CreateInstance<PoseSnapshotSO>();
                        neutral.poseName    = "Nötr";
                        neutral.holdSeconds = 0f;
                        demo.CaptureIntoPoseSnapshotSO(neutral);
                        AssetDatabase.CreateAsset(neutral, path);

                        Undo.RecordObject(target, "Set Neutral Pose");
                        demo.neutralPose = neutral;
                        EditorUtility.SetDirty(target);
                    }

                    AssetDatabase.SaveAssets();
                    Debug.Log("[PoseCapture] Nötr poz kaydedildi.");
                }
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Nötr Poza Döndür ─────────────────────────────────────
        GUI.enabled = demo.neutralPose != null;
        if (GUILayout.Button("↺  Nötr Poza Döndür"))
        {
            RecordBones(demo, "Reset to Neutral");
            demo.ApplyNeutral();
            SceneView.RepaintAll();
        }
        GUI.enabled = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>AssetDatabase klasör ağacını oluşturur (yoksa).</summary>
    private static void EnsureDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts   = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>Önizleme öncesinde tüm kemikleri Undo sistemine kaydeder.</summary>
    private static void RecordBones(PoseDemoController demo, string label)
    {
        Object[] bones =
        {
            demo.hipBone, demo.lowerSpineBone, demo.spineBone, demo.chestBone, demo.neckBone, demo.headBone,
            demo.leftShoulderBone, demo.rightShoulderBone,
            demo.leftUpperArmBone, demo.rightUpperArmBone,
            demo.leftForearmBone, demo.rightForearmBone,
            demo.leftHandBone, demo.rightHandBone,
            demo.leftThighBone, demo.rightThighBone,
            demo.leftShinBone,  demo.rightShinBone,
            demo.leftAnkleBone, demo.rightAnkleBone
        };
        foreach (var b in bones)
            if (b != null) Undo.RecordObject(b, label);
    }
}
#endif
