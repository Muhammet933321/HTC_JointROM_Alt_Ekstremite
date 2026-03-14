using UnityEngine;

/// <summary>
/// Editor utility that automatically finds and assigns Mixamo bone references
/// to a FullBodyIKSolver component. Searches the transform hierarchy for
/// standard Mixamo bone names (mixamorig1:*).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FullBodyIKSolver))]
public class MixamoBoneAutoFinder : MonoBehaviour
{
    [Header("Avatar Root")]
    [Tooltip("Mixamo modelin kök transform'u (mixamorig1:Hips'in ebeveyni)")]
    [SerializeField] private Transform avatarRoot;

    /// <summary>
    /// Finds all Mixamo bones and assigns them to the linked FullBodyIKSolver.
    /// Call from the editor context menu or at runtime.
    /// </summary>
    [ContextMenu("Kemikleri Otomatik Bul ve Ata")]
    public void FindAndAssignBones()
    {
        if (!avatarRoot)
        {
            Debug.LogError("[MixamoBoneAutoFinder] Avatar root atanmamış!");
            return;
        }

        FullBodyIKSolver solver = GetComponent<FullBodyIKSolver>();
        if (!solver)
        {
            Debug.LogError("[MixamoBoneAutoFinder] FullBodyIKSolver bulunamadı!");
            return;
        }

        // Use SerializedObject for assignment in both Editor and Runtime
        var fields = new System.Collections.Generic.Dictionary<string, string>
        {
            // Field name → Bone name suffix (after "mixamorig1:")
            { "hipsBone",           "Hips" },
            { "spineBone",          "Spine" },
            { "spine1Bone",         "Spine1" },
            { "spine2Bone",         "Spine2" },
            { "neckBone",           "Neck" },
            { "headBone",           "Head" },
            { "leftShoulderBone",   "LeftShoulder" },
            { "leftUpperArmBone",   "LeftArm" },
            { "leftForeArmBone",    "LeftForeArm" },
            { "leftHandBone",       "LeftHand" },
            { "rightShoulderBone",  "RightShoulder" },
            { "rightUpperArmBone",  "RightArm" },
            { "rightForeArmBone",   "RightForeArm" },
            { "rightHandBone",      "RightHand" },
            { "leftUpLegBone",      "LeftUpLeg" },
            { "leftLegBone",        "LeftLeg" },
            { "leftFootBone",       "LeftFoot" },
            { "rightUpLegBone",     "RightUpLeg" },
            { "rightLegBone",       "RightLeg" },
            { "rightFootBone",      "RightFoot" },
        };

        int found = 0;
        int total = fields.Count;

        foreach (var kvp in fields)
        {
            string boneName = kvp.Value;
            Transform bone = FindBoneRecursive(avatarRoot, boneName);
            if (bone != null)
            {
                // Use reflection to set the private serialized field
                var field = typeof(FullBodyIKSolver).GetField(kvp.Key,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(solver, bone);
                    found++;
                    Debug.Log($"[MixamoBoneAutoFinder] {kvp.Key} → {bone.name}");
                }
                else
                {
                    Debug.LogWarning($"[MixamoBoneAutoFinder] Alan bulunamadı: {kvp.Key}");
                }
            }
            else
            {
                Debug.LogWarning($"[MixamoBoneAutoFinder] Kemik bulunamadı: {boneName}");
            }
        }

        Debug.Log($"[MixamoBoneAutoFinder] {found}/{total} kemik başarıyla atandı.");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(solver);
#endif
    }

    /// <summary>
    /// Recursively search for a bone containing the given name.
    /// Supports both "mixamorig1:BoneName" and "mixamorig:BoneName" formats.
    /// </summary>
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        // Check exact matches for common prefixes
        string[] prefixes = { "mixamorig1:", "mixamorig:", "" };

        foreach (Transform child in parent)
        {
            foreach (string prefix in prefixes)
            {
                if (child.name == prefix + boneName)
                    return child;
            }

            // Check if the bone name is contained (for non-standard naming)
            if (child.name.EndsWith(boneName))
                return child;

            // Recurse
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }

        return null;
    }
}
