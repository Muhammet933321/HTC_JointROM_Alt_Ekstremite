#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to quickly set up a full-body tracking scene.
/// Creates all required IK target GameObjects and wires up components.
/// </summary>
public class FullBodyTrackingSetup : EditorWindow
{
    private Transform avatarRoot;
    private Transform xrOrigin;

    [MenuItem("Tools/Full Body Tracking/Sahne Kurulumu")]
    private static void OpenWindow()
    {
        GetWindow<FullBodyTrackingSetup>("FB Tracking Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Full Body Tracking Sahne Kurulumu", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        avatarRoot = (Transform)EditorGUILayout.ObjectField("Avatar Root (Mixamo)", avatarRoot, typeof(Transform), true);
        xrOrigin = (Transform)EditorGUILayout.ObjectField("XR Origin", xrOrigin, typeof(Transform), true);

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Bu araç aşağıdakileri oluşturur:\n" +
            "1. IK Target objeleri (Head, Hands, Pelvis, Feet, Knees)\n" +
            "2. FullBodyIKSolver component'i\n" +
            "3. FullBodyTrackingManager component'i\n" +
            "4. FullBodyCalibrator component'i\n" +
            "5. MixamoBoneAutoFinder ile otomatik kemik atama\n\n" +
            "Kullanım:\n" +
            "- Avatar Root: Mixamo modelin root'u (mixamorig1:Hips'in ebeveyni)\n" +
            "- XR Origin: XR Interaction Toolkit'in XR Origin objesi",
            MessageType.Info);

        EditorGUILayout.Space();

        GUI.enabled = avatarRoot != null;

        if (GUILayout.Button("Sistemi Kur", GUILayout.Height(40)))
        {
            SetupFullBodyTracking();
        }

        GUI.enabled = true;
    }

    private void SetupFullBodyTracking()
    {
        // Create the manager GameObject
        GameObject managerObj = new GameObject("[FullBodyTracking]");
        Undo.RegisterCreatedObjectUndo(managerObj, "Create Full Body Tracking System");

        // Create IK Targets parent
        GameObject targetsParent = new GameObject("IK_Targets");
        targetsParent.transform.SetParent(managerObj.transform);

        // Create individual targets
        Transform headTarget = CreateTarget(targetsParent.transform, "HeadTarget");
        Transform leftHandTarget = CreateTarget(targetsParent.transform, "LeftHandTarget");
        Transform rightHandTarget = CreateTarget(targetsParent.transform, "RightHandTarget");
        Transform pelvisTarget = CreateTarget(targetsParent.transform, "PelvisTarget");
        Transform leftFootTarget = CreateTarget(targetsParent.transform, "LeftFootTarget");
        Transform rightFootTarget = CreateTarget(targetsParent.transform, "RightFootTarget");
        Transform leftKneeTarget = CreateTarget(targetsParent.transform, "LeftKneeTarget");
        Transform rightKneeTarget = CreateTarget(targetsParent.transform, "RightKneeTarget");

        // Add FullBodyIKSolver to avatar
        FullBodyIKSolver solver = avatarRoot.gameObject.GetComponent<FullBodyIKSolver>();
        if (!solver)
            solver = avatarRoot.gameObject.AddComponent<FullBodyIKSolver>();

        // Add MixamoBoneAutoFinder
        MixamoBoneAutoFinder finder = avatarRoot.gameObject.GetComponent<MixamoBoneAutoFinder>();
        if (!finder)
            finder = avatarRoot.gameObject.AddComponent<MixamoBoneAutoFinder>();

        // Set avatar root on the finder via reflection
        var avatarRootField = typeof(MixamoBoneAutoFinder).GetField("avatarRoot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (avatarRootField != null)
            avatarRootField.SetValue(finder, avatarRoot);

        // Auto-find bones
        finder.FindAndAssignBones();

        // Assign IK targets to solver via reflection
        SetField(solver, "headTarget", headTarget);
        SetField(solver, "leftHandTarget", leftHandTarget);
        SetField(solver, "rightHandTarget", rightHandTarget);
        SetField(solver, "pelvisTarget", pelvisTarget);
        SetField(solver, "leftFootTarget", leftFootTarget);
        SetField(solver, "rightFootTarget", rightFootTarget);
        SetField(solver, "leftKneeTracker", leftKneeTarget);
        SetField(solver, "rightKneeTracker", rightKneeTarget);

        // Add FullBodyTrackingManager
        FullBodyTrackingManager manager = managerObj.AddComponent<FullBodyTrackingManager>();
        SetField(manager, "ikSolver", solver);
        SetField(manager, "headIKTarget", headTarget);
        SetField(manager, "leftHandIKTarget", leftHandTarget);
        SetField(manager, "rightHandIKTarget", rightHandTarget);
        SetField(manager, "pelvisIKTarget", pelvisTarget);
        SetField(manager, "leftFootIKTarget", leftFootTarget);
        SetField(manager, "rightFootIKTarget", rightFootTarget);
        SetField(manager, "leftKneeIKTarget", leftKneeTarget);
        SetField(manager, "rightKneeIKTarget", rightKneeTarget);

        // Set XR references if available
        if (xrOrigin)
        {
            Camera cam = xrOrigin.GetComponentInChildren<Camera>();
            if (cam)
                SetField(manager, "hmdTransform", cam.transform);
        }

        // Add Calibrator
        FullBodyCalibrator calibrator = managerObj.AddComponent<FullBodyCalibrator>();
        SetField(calibrator, "ikSolver", solver);
        SetField(calibrator, "trackingManager", manager);

        // Add Debug Visualizer
        FullBodyDebugVisualizer visualizer = managerObj.AddComponent<FullBodyDebugVisualizer>();
        SetField(visualizer, "ikSolver", solver);
        SetField(visualizer, "trackingManager", manager);

        EditorUtility.SetDirty(managerObj);
        EditorUtility.SetDirty(avatarRoot.gameObject);

        Selection.activeGameObject = managerObj;

        Debug.Log("[FullBodyTrackingSetup] Sistem başarıyla kuruldu! " +
                  "Tracker Transform'larını FullBodyTrackingManager'a atamayı unutmayın.");

        EditorUtility.DisplayDialog("Kurulum Tamamlandı",
            "Full-body tracking sistemi başarıyla kuruldu.\n\n" +
            "Sonraki adımlar:\n" +
            "1. FullBodyTrackingManager'da tracker Transform'larını atayın\n" +
            "2. HMD ve controller Transform'larını atayın\n" +
            "3. Play modda kalibrasyon yapın (C tuşu veya A/B butonu, 2sn basılı tutun)",
            "Tamam");
    }

    private Transform CreateTarget(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        return obj.transform;
    }

    private void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[FullBodyTrackingSetup] Alan bulunamadı: {target.GetType().Name}.{fieldName}");
        }
    }
}
#endif
