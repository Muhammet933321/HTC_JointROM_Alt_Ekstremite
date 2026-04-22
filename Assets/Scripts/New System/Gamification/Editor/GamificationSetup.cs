#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-click Editor setup tool for the Denge Kahramanı gamification system.
///
/// Opens via:  Tools → Gamification → Sahne Kurulumu
///
/// What it creates:
///   1. TaskDefinition ScriptableObject assets for all 8 tasks (PDF §9)
///   2. [Gamification] root GameObject with all manager scripts
///   3. Ghost avatar (duplicate of main avatar, semi-transparent)
///   4. Platform feedback object
///   5. World-space Canvas with UI panels
///   6. Wires all component references automatically
/// </summary>
public class GamificationSetup : EditorWindow
{
    // ───────────────────────── Window State ─────────────────────────

    private Transform _mainAvatarRoot;
    private string _taskAssetFolder = "Assets/Resources/Tasks";

    private bool _createGhostAvatar   = true;
    private bool _createPlatform      = true;
    private bool _createUI            = true;
    private bool _createTaskAssets    = true;
    private bool _autoStartOnAwake    = false;

    private Vector2 _scroll;

    // ───────────────────────── Menu Entry ─────────────────────────

    [MenuItem("Tools/Gamification/Sahne Kurulumu", priority = 100)]
    private static void OpenWindow()
    {
        var win = GetWindow<GamificationSetup>("Denge Kahramanı Setup");
        win.minSize = new Vector2(420, 560);
    }

    // ───────────────────────── GUI ─────────────────────────

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("🦵  Denge Kahramanı — Gamification Kurulumu", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── References ──
        GUILayout.Label("Referanslar", EditorStyles.miniBoldLabel);
        _mainAvatarRoot = (Transform)EditorGUILayout.ObjectField(
            "Ana Avatar Root",
            _mainAvatarRoot, typeof(Transform), true);

        _taskAssetFolder = EditorGUILayout.TextField("Task Asset Klasörü", _taskAssetFolder);

        EditorGUILayout.Space(8);

        // ── Options ──
        GUILayout.Label("Oluşturulacaklar", EditorStyles.miniBoldLabel);
        _createTaskAssets  = EditorGUILayout.Toggle("Task Definition Asset'leri (8 adet)", _createTaskAssets);
        _createGhostAvatar = EditorGUILayout.Toggle("Hayalet Avatar (Ghost)",              _createGhostAvatar);
        _createPlatform    = EditorGUILayout.Toggle("Platform (Feedback Zemin)",           _createPlatform);
        _createUI          = EditorGUILayout.Toggle("World-Space Canvas (UI)",             _createUI);
        _autoStartOnAwake  = EditorGUILayout.Toggle("Oturum Otomatik Başlasın",            _autoStartOnAwake);

        EditorGUILayout.Space(8);

        // ── Info ──
        EditorGUILayout.HelpBox(
            "Bu araç şunları oluşturur:\n" +
            "• 8 adet TaskDefinition ScriptableObject (PDF §9 görevleri)\n" +
            "• [Gamification] manager objesi (tüm script'ler)\n" +
            "• Ghost Avatar: yarı saydam demo karakter\n" +
            "• Feedback Platform: valgus'a göre renk değiştiren zemin\n" +
            "• World-Space Canvas: görev/canlı/özet panelleri\n\n" +
            "Ana Avatar Root: sahnedeki Mixamo model kökü (opsiyonel — olmadan da çalışır).",
            MessageType.Info);

        EditorGUILayout.Space(8);

        bool canSetup = !Application.isPlaying;
        GUI.enabled = canSetup;

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("  Sistemi Kur  ", GUILayout.Height(44)))
            RunSetup();
        GUI.backgroundColor = Color.white;

        GUI.enabled = true;

        if (!canSetup)
            EditorGUILayout.HelpBox("Play modunda kurulum yapılamaz.", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    // ───────────────────────── Main Setup ─────────────────────────

    private void RunSetup()
    {
        List<TaskDefinition> tasks = null;

        if (_createTaskAssets)
            tasks = CreateTaskAssets();

        // Root object
        GameObject root = new GameObject("[Gamification]");
        Undo.RegisterCreatedObjectUndo(root, "Create Gamification System");

        // Core managers
        var sequencer  = root.AddComponent<TaskSequencer>();
        var evaluator  = root.AddComponent<TaskEvaluator>();
        var scorer     = root.AddComponent<GameScoreManager>();
        var reporter   = root.AddComponent<SessionReportWriter>();
        var biometrics = root.AddComponent<LowerLimbBiometrics>();

        // Wire cross-references
        Set(evaluator, "sequencer",  sequencer);
        Set(evaluator, "biometrics", biometrics);
        Set(scorer,    "evaluator",  evaluator);
        Set(reporter,  "sequencer",  sequencer);

        // Auto-start
        if (_autoStartOnAwake)
            Set(sequencer, "autoStartOnAwake", true);

        // Populate task sequence
        if (tasks != null && tasks.Count > 0)
        {
            sequencer.taskSequence = new List<TaskDefinition>(tasks);
            EditorUtility.SetDirty(sequencer);
        }

        // Enable simulated input by default so editor testing works without trackers
        biometrics.useSimulatedInput = true;

        // Feedback platform
        if (_createPlatform)
            CreatePlatform(root.transform, root, scorer);

        // Ghost avatar
        if (_createGhostAvatar && _mainAvatarRoot != null)
            CreateGhostAvatar(root.transform, root, sequencer);

        // World-space Canvas + GameFlowController
        GameFlowController flowCtrl = null;
        if (_createUI)
            flowCtrl = CreateCanvas(root.transform, root, sequencer, evaluator, scorer, biometrics);

        // If canvas was skipped, still add GameFlowController
        if (flowCtrl == null)
        {
            flowCtrl = root.AddComponent<GameFlowController>();
            flowCtrl.sequencer   = sequencer;
            flowCtrl.biometrics  = biometrics;
            flowCtrl.simulatedMode = true;
        }

        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("Kurulum Tamamlandı ✓",
            "[Gamification] objesi oluşturuldu.\n\n" +
            "Sonraki adımlar:\n" +
            "1. GameFlowController > 'calibrator' ve 'trackingManager' referanslarını atayın\n" +
            "   (sahnedeki mevcut [FullBodyTracking] objesinden)\n" +
            "2. Editör testi: simulatedMode=true bırakın, Space=oturum başlat\n" +
            "3. Gerçek hardware: simulatedMode=false, tracker'ları bağlayın\n" +
            "4. LowerLimbBiometrics sliderları ile simüle veri ayarlayabilirsiniz",
            "Tamam");

        Debug.Log("[GamificationSetup] Kurulum tamamlandı. Klasör: " + _taskAssetFolder);
    }

    // ───────────────────────── Task Assets ─────────────────────────

    private List<TaskDefinition> CreateTaskAssets()
    {
        if (!AssetDatabase.IsValidFolder(_taskAssetFolder))
        {
            string[] parts = _taskAssetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Task data: (type, nameTR, instructions, duration, valgusThresh, swayThresh, hipEuler, kneeEuler)
        var defs = new (TaskType type, string name, string instr, float dur, float rest, Vector3 hip, Vector3 knee)[]
        {
            (
                TaskType.Standing,
                "Dik Duruş",
                "Ayaklarınızı omuz genişliğinde açarak dik durun.\nKollarınızı yanlara serbest bırakın ve doğal bir duruş alın.",
                10f, 3f,
                Vector3.zero, Vector3.zero
            ),
            (
                TaskType.LeanRight,
                "Sağa Eğilme",
                "Vücudunuzu sağa doğru yavaşça eğin.\nAyaklarınız yerden kalkmadan gövdenizi yana doğru uzatın.",
                8f, 3f,
                new Vector3(0f, 0f, -15f), Vector3.zero
            ),
            (
                TaskType.LeanLeft,
                "Sola Eğilme",
                "Vücudunuzu sola doğru yavaşça eğin.\nAyaklarınız yerden kalkmadan gövdenizi yana doğru uzatın.",
                8f, 3f,
                new Vector3(0f, 0f, 15f), Vector3.zero
            ),
            (
                TaskType.LeanForward,
                "Öne Eğilme",
                "Belkemiğinizi düz tutarak öne doğru yavaşça eğilin.\nDizlerinizi hafifçe bükebilirsiniz.",
                8f, 3f,
                new Vector3(20f, 0f, 0f), new Vector3(10f, 0f, 0f)
            ),
            (
                TaskType.SingleLegBalance_R,
                "Sağ Ayak Dengesi",
                "Sol ayağınızı yerden kaldırın ve sadece sağ ayağınız üzerinde dengede durun.\nGözlerinizi karşıya sabitleyin.",
                10f, 4f,
                new Vector3(0f, 0f, 5f), Vector3.zero
            ),
            (
                TaskType.SingleLegBalance_L,
                "Sol Ayak Dengesi",
                "Sağ ayağınızı yerden kaldırın ve sadece sol ayağınız üzerinde dengede durun.\nGözlerinizi karşıya sabitleyin.",
                10f, 4f,
                new Vector3(0f, 0f, -5f), Vector3.zero
            ),
            (
                TaskType.MiniSquat,
                "Mini Squat",
                "Ayaklarınız omuz genişliğinde açık, ayak uçları hafif dışarı dönük.\nDizlerinizi ayak uçlarınızın hizasında tutarak yavaşça çömelin ve kalkın.",
                15f, 4f,
                new Vector3(25f, 0f, 0f), new Vector3(50f, 0f, 0f)
            ),
            (
                TaskType.WalkSimulation,
                "Yürüme Simülasyonu",
                "Yerinde yavaşça adım alın — önce sağ, sonra sol ayak.\nHer adımda dizinizi kaldırın ve yavaş bir tempoda devam edin.",
                12f, 3f,
                Vector3.zero, new Vector3(15f, 0f, 0f)
            ),
        };

        var list = new List<TaskDefinition>();

        foreach (var d in defs)
        {
            string assetPath = $"{_taskAssetFolder}/Task_{d.type}.asset";

            // Don't overwrite existing
            var existing = AssetDatabase.LoadAssetAtPath<TaskDefinition>(assetPath);
            if (existing != null)
            {
                list.Add(existing);
                continue;
            }

            var td = ScriptableObject.CreateInstance<TaskDefinition>();
            td.taskType        = d.type;
            td.taskNameTR      = d.name;
            td.instructionsTR  = d.instr;
            td.durationSeconds = d.dur;
            td.restAfterSeconds = d.rest;
            td.countdownSeconds = 3f;
            td.valgusThresholdDeg    = 8f;
            td.asymmetryThresholdPct = 10f;
            td.minFlexionDeg         = 45f;
            td.swayRmsThreshold      = 0.015f;
            td.demoHipEuler          = d.hip;
            td.demoKneeEuler         = d.knee;
            td.demoPauseDuration     = 1.5f;
            td.demoTransitionSpeed   = 2f;
            td.loopDemo              = true;

            AssetDatabase.CreateAsset(td, assetPath);
            list.Add(td);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GamificationSetup] {list.Count} TaskDefinition asset oluşturuldu: {_taskAssetFolder}");
        return list;
    }

    // ───────────────────────── Platform ─────────────────────────

    private GameObject CreatePlatform(Transform parent, GameObject managerObj, GameScoreManager scorer)
    {
        // Create a flat quad as the feedback platform
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Plane);
        platform.name = "FeedbackPlatform";
        platform.transform.SetParent(parent);
        platform.transform.localPosition = new Vector3(0f, 0f, 0f);
        platform.transform.localScale    = new Vector3(0.5f, 1f, 0.5f);
        Undo.RegisterCreatedObjectUndo(platform, "Create Feedback Platform");

        // Add PostureFeedbackController to manager
        var feedback = managerObj.AddComponent<PostureFeedbackController>();
        var biometrics = managerObj.GetComponent<LowerLimbBiometrics>();
        Set(feedback, "biometrics",       biometrics);
        Set(feedback, "platformRenderer", platform.GetComponent<Renderer>());

        return platform;
    }

    // ───────────────────────── Ghost Avatar ─────────────────────────

    private void CreateGhostAvatar(Transform parent, GameObject managerObj, TaskSequencer sequencer)
    {
        // Duplicate the main avatar
        GameObject ghost = Instantiate(_mainAvatarRoot.gameObject);
        ghost.name = "GhostAvatar_Demo";
        Undo.RegisterCreatedObjectUndo(ghost, "Create Ghost Avatar");
        ghost.transform.SetParent(parent);
        ghost.transform.localPosition = new Vector3(1.4f, 0f, 0f); // 1.4m to the right

        // Remove IK / tracking scripts from ghost — order matters:
        // MixamoBoneAutoFinder depends on FullBodyIKSolver, so remove it first.
        foreach (var type in new System.Type[]
        {
            typeof(MixamoBoneAutoFinder),   // must come before FullBodyIKSolver
            typeof(FullBodyIKSolver),
            typeof(FullBodyTrackingManager),
            typeof(FullBodyCalibrator),
            typeof(FullBodyDebugVisualizer),
        })
        {
            var comp = ghost.GetComponent(type);
            if (comp != null)
                DestroyImmediate(comp);
        }

        // Make all renderers semi-transparent
        MakeSemiTransparent(ghost, 0.35f);

        // Add PoseDemoController
        var demo = managerObj.AddComponent<PoseDemoController>();
        Set(demo, "sequencer", sequencer);
        demo.playDuringCountdown   = true;
        demo.playDuringMeasurement = false;

        // Try to auto-find bones via Mixamo naming convention
        Transform hipBone       = FindBoneByKeyword(ghost.transform, "hips", "pelvis");
        // Spine1 preferred for lean counter-rotation (mid-lumbar); fall back to Spine
        Transform spineBone     = FindBoneByKeyword(ghost.transform, "spine1") ??
                                   FindBoneByKeyword(ghost.transform, "spine");
        // Thigh = UpLeg; Shin = Leg (different keywords — "leftleg" does NOT match "leftupleg")
        Transform leftThighBone  = FindBoneByKeyword(ghost.transform, "leftupleg",  "l_upleg");
        Transform rightThighBone = FindBoneByKeyword(ghost.transform, "rightupleg", "r_upleg");
        Transform leftShinBone   = FindBoneByKeyword(ghost.transform, "leftleg",    "l_leg");
        Transform rightShinBone  = FindBoneByKeyword(ghost.transform, "rightleg",   "r_leg");
        Transform leftAnkleBone  = FindBoneByKeyword(ghost.transform, "leftfoot",   "l_foot");
        Transform rightAnkleBone = FindBoneByKeyword(ghost.transform, "rightfoot",  "r_foot");

        // Assign whichever bones were found
        if (hipBone)        demo.hipBone        = hipBone;
        if (spineBone)      demo.spineBone      = spineBone;
        if (leftThighBone)  demo.leftThighBone  = leftThighBone;
        if (rightThighBone) demo.rightThighBone = rightThighBone;
        if (leftShinBone)   demo.leftShinBone   = leftShinBone;
        if (rightShinBone)  demo.rightShinBone  = rightShinBone;
        if (leftAnkleBone)  demo.leftAnkleBone  = leftAnkleBone;
        if (rightAnkleBone) demo.rightAnkleBone = rightAnkleBone;

        EditorUtility.SetDirty(demo);
        Debug.Log("[GamificationSetup] Ghost avatar oluşturuldu. PoseDemoController kemiklerini Inspector'dan kontrol edin.");
    }

    // ───────────────────────── Canvas ─────────────────────────

    private GameFlowController CreateCanvas(Transform parent, GameObject managerObj,
        TaskSequencer sequencer, TaskEvaluator evaluator,
        GameScoreManager scorer, LowerLimbBiometrics biometrics)
    {
        // ── Root Canvas ──
        GameObject canvasObj = new GameObject("GameCanvas");
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Game Canvas");
        canvasObj.transform.SetParent(parent);
        canvasObj.transform.localPosition = new Vector3(0f, 1.6f, 1.8f);
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale    = Vector3.one * 0.002f;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // ── Background ──
        AddImage(canvasObj.transform, "Background",
            new Rect(-400, -300, 800, 600),
            new Color(0.05f, 0.05f, 0.05f, 0.85f));

        // ══════════════════════════════════════════════
        // ── CALIBRATION PANEL (shown first on launch) ──
        // ══════════════════════════════════════════════
        GameObject calibPanel = CreatePanel(canvasObj.transform, "CalibrationPanel",
            new Rect(-380, -280, 760, 560));
        calibPanel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.12f, 0.95f);

        TMP_Text trackerStatusText = AddTMPText(calibPanel.transform, "TrackerStatus",
            new Rect(-350, 230, 700, 42), "VR tracker'ları bekleniyor...", 22, FontStyle.Bold, TextAlignmentOptions.Center);
        trackerStatusText.color = new Color(1f, 0.84f, 0f);

        TMP_Text calibInstructText = AddTMPText(calibPanel.transform, "CalibInstruct",
            new Rect(-330, 40, 660, 200),
            "T-POZU KALIBRASYONU\n\nAyaklarınızı omuz genişliğinde açın.\nKollarınızı yanlara yatay tutun.\nDik durun.\n\nSağ kontrolcüde A/B'ye 2 sn basılı tutun\n(Editör: C tuşu)",
            20, FontStyle.Normal, TextAlignmentOptions.Center);

        TMP_Text calibStatusText = AddTMPText(calibPanel.transform, "CalibStatus",
            new Rect(-330, -120, 660, 40), "Bekleniyor...", 20, FontStyle.Normal, TextAlignmentOptions.Center);
        calibStatusText.color = Color.white;

        Image holdBar = AddBarImage(calibPanel.transform, "HoldProgressBar",
            new Rect(-320, -165, 640, 28), new Color(0.3f, 0.7f, 1f));

        TMP_Text startPromptText = AddTMPText(calibPanel.transform, "StartPrompt",
            new Rect(-330, -220, 660, 60),
            "OTURUMU BAŞLATMAK İÇİN\nA BUTONUNA BASIN  (Editör: Space)",
            24, FontStyle.Bold, TextAlignmentOptions.Center);
        startPromptText.color = new Color(0.4f, 1f, 0.4f);
        startPromptText.gameObject.SetActive(false);

        // ── Task Panel ──
        GameObject taskPanel = CreatePanel(canvasObj.transform, "TaskPanel",
            new Rect(-380, 100, 760, 180));

        TMP_Text taskNameText     = AddTMPText(taskPanel.transform, "TaskName",
            new Rect(-350, 60, 700, 60), "", 32, FontStyle.Bold, TextAlignmentOptions.Center);

        TMP_Text taskInstructText = AddTMPText(taskPanel.transform, "Instructions",
            new Rect(-350, -10, 700, 80), "", 22, FontStyle.Normal, TextAlignmentOptions.Center);

        TMP_Text taskProgressText = AddTMPText(taskPanel.transform, "Progress",
            new Rect(-350, -70, 700, 35), "", 20, FontStyle.Normal, TextAlignmentOptions.Center);

        // ── Countdown (centred, large) ──
        TMP_Text countdownText = AddTMPText(canvasObj.transform, "CountdownText",
            new Rect(-150, -10, 300, 120), "", 96, FontStyle.Bold, TextAlignmentOptions.Center);
        countdownText.color = new Color(1f, 0.84f, 0f);

        // ── Live Panel ──
        GameObject livePanel = CreatePanel(canvasObj.transform, "LivePanel",
            new Rect(-380, -100, 760, 200));

        TMP_Text leftValgusText  = AddTMPText(livePanel.transform, "LeftValgus",
            new Rect(-350, 60, 340, 36), "Sol Valgus: --", 22);
        TMP_Text rightValgusText = AddTMPText(livePanel.transform, "RightValgus",
            new Rect(10,   60, 340, 36), "Sağ Valgus: --", 22);

        Image leftValgusBar  = AddBarImage(livePanel.transform, "BarLeft",
            new Rect(-350, 20, 340, 20), Color.green);
        Image rightValgusBar = AddBarImage(livePanel.transform, "BarRight",
            new Rect(10,   20, 340, 20), Color.green);

        TMP_Text swayText      = AddTMPText(livePanel.transform, "Sway",
            new Rect(-350, -20, 340, 36), "Sway: --", 22);
        TMP_Text symmetryText  = AddTMPText(livePanel.transform, "Symmetry",
            new Rect(10,   -20, 340, 36), "Simetri: --", 22);
        Image swayBar          = AddBarImage(livePanel.transform, "BarSway",
            new Rect(-350, -60, 340, 20), Color.yellow);

        TMP_Text liveScoreText = AddTMPText(livePanel.transform, "LiveScore",
            new Rect(-350, -90, 700, 40), "", 26, FontStyle.Bold, TextAlignmentOptions.Center);
        liveScoreText.color = Color.white;

        // ── Rest Panel ──
        GameObject restPanel = CreatePanel(canvasObj.transform, "RestPanel",
            new Rect(-300, -50, 600, 100));
        restPanel.SetActive(false);
        TMP_Text restTimerText = AddTMPText(restPanel.transform, "RestTimer",
            new Rect(-280, -30, 560, 60), "Dinleniyor...", 28, FontStyle.Bold, TextAlignmentOptions.Center);

        // ── Summary Panel ──
        GameObject summaryPanel = CreatePanel(canvasObj.transform, "SummaryPanel",
            new Rect(-380, -280, 760, 240));
        summaryPanel.SetActive(false);
        TMP_Text sessionScoreText = AddTMPText(summaryPanel.transform, "SessionScore",
            new Rect(-350, 90, 700, 50), "Oturum Skoru: --", 30, FontStyle.Bold, TextAlignmentOptions.Center);
        TMP_Text resultListText   = AddTMPText(summaryPanel.transform, "ResultList",
            new Rect(-350, -60, 700, 140), "", 20);

        // ── GameUIController ──
        var ui = managerObj.AddComponent<GameUIController>();
        ui.sequencer   = sequencer;
        ui.evaluator   = evaluator;
        ui.scoreManager = scorer;
        ui.biometrics  = biometrics;

        ui.taskPanel         = taskPanel;
        ui.taskNameText      = taskNameText;
        ui.instructionText   = taskInstructText;
        ui.countdownText     = countdownText;
        ui.taskProgressText  = taskProgressText;
        ui.livePanel         = livePanel;
        ui.leftValgusText    = leftValgusText;
        ui.rightValgusText   = rightValgusText;
        ui.swayText          = swayText;
        ui.symmetryText      = symmetryText;
        ui.liveScoreText     = liveScoreText;
        ui.leftValgusBar     = leftValgusBar;
        ui.rightValgusBar    = rightValgusBar;
        ui.swayBar           = swayBar;
        ui.restPanel         = restPanel;
        ui.restTimerText     = restTimerText;
        ui.summaryPanel      = summaryPanel;
        ui.sessionScoreText  = sessionScoreText;
        ui.taskResultsListText = resultListText;

        EditorUtility.SetDirty(ui);

        // ── GameFlowController ──
        var flow = managerObj.AddComponent<GameFlowController>();
        flow.sequencer               = sequencer;
        flow.biometrics              = biometrics;
        flow.gameUI                  = ui;
        flow.calibrationPanel        = calibPanel;
        flow.trackerStatusText       = trackerStatusText;
        flow.calibrationInstructText = calibInstructText;
        flow.calibrationStatusText   = calibStatusText;
        flow.holdProgressBar         = holdBar;
        flow.startPromptText         = startPromptText;
        flow.simulatedMode           = true; // safe default — disable for real hardware
        EditorUtility.SetDirty(flow);

        return flow;
    }

    // ───────────────────────── UI Helpers ─────────────────────────

    private static GameObject CreatePanel(Transform parent, string name, Rect rect)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        rt.sizeDelta = new Vector2(rect.width, rect.height);
        return go;
    }

    private static void AddImage(Transform parent, string name, Rect rect, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        rt.sizeDelta = new Vector2(rect.width, rect.height);
    }

    private static TMP_Text AddTMPText(Transform parent, string name, Rect rect,
        string defaultText, int fontSize = 24,
        FontStyle style = FontStyle.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = defaultText;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = alignment;
        tmp.color     = Color.white;

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        rt.sizeDelta = new Vector2(rect.width, rect.height);
        return tmp;
    }

    private static Image AddBarImage(Transform parent, string name, Rect rect, Color color)
    {
        // Background bar
        var bg = new GameObject(name + "_BG");
        bg.transform.SetParent(parent, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchoredPosition = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        bgRt.sizeDelta = new Vector2(rect.width, rect.height);

        // Fill bar (child)
        var fill = new GameObject(name + "_Fill");
        fill.transform.SetParent(bg.transform, false);
        var img = fill.AddComponent<Image>();
        img.color = color;
        img.type  = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 0.5f;
        var rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    // ───────────────────────── Bone Search Helper ─────────────────────────

    private static Transform FindBoneByKeyword(Transform root, params string[] keywords)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace("-", "").Replace(" ", "");
            foreach (var kw in keywords)
            {
                string kwClean = kw.ToLowerInvariant().Replace("_", "").Replace("-", "");
                if (lower.Contains(kwClean))
                    return t;
            }
        }
        return null;
    }

    // ───────────────────────── Reflection Helper ─────────────────────────

    private static void Set(object target, string fieldName, object value)
    {
        if (target == null) return;

        // Try field first
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        // Then property
        var prop = target.GetType().GetProperty(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return;
        }

        Debug.LogWarning($"[GamificationSetup] Saha/özellik bulunamadı: {target.GetType().Name}.{fieldName}");
    }

    // ───────────────────────── Ghost Material Helper ─────────────────────────

    private static void MakeSemiTransparent(GameObject go, float alpha)
    {
        foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = rend.sharedMaterials;
            var newMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) { newMats[i] = null; continue; }

                var mat = new Material(mats[i]);

                // Try URP Lit
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f); // Transparent

                // Standard / URP base color alpha
                if (mat.HasProperty("_BaseColor"))
                {
                    var c = mat.GetColor("_BaseColor");
                    mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
                }
                else if (mat.HasProperty("_Color"))
                {
                    var c = mat.GetColor("_Color");
                    mat.SetColor("_Color", new Color(c.r, c.g, c.b, alpha));
                }

                mat.renderQueue = 3000;
                newMats[i] = mat;
            }
            rend.sharedMaterials = newMats;
        }
    }
}
#endif
