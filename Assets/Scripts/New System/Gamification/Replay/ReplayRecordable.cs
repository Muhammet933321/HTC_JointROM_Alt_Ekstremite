using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ReplayRecordable : MonoBehaviour
{
    [SerializeField] private string stableId;
    [SerializeField] private Transform[] trackedTransforms;

    public string StableId => string.IsNullOrWhiteSpace(stableId) ? gameObject.name : stableId;

    public void AppendPoses(List<ReplayPose> poses)
    {
        if (poses == null) return;

        if (trackedTransforms == null || trackedTransforms.Length == 0)
        {
            poses.Add(ReplayPose.FromTransform($"recordable/{StableId}", transform));
            return;
        }

        for (int index = 0; index < trackedTransforms.Length; index++)
        {
            Transform tracked = trackedTransforms[index];
            string id = tracked != null
                ? $"recordable/{StableId}/{tracked.name}"
                : $"recordable/{StableId}/missing_{index}";
            poses.Add(ReplayPose.FromTransform(id, tracked));
        }
    }

    public void AppendTargets(Dictionary<string, Transform> targets)
    {
        if (targets == null) return;

        if (trackedTransforms == null || trackedTransforms.Length == 0)
        {
            targets[$"recordable/{StableId}"] = transform;
            return;
        }

        for (int index = 0; index < trackedTransforms.Length; index++)
        {
            Transform tracked = trackedTransforms[index];
            if (tracked == null) continue;
            targets[$"recordable/{StableId}/{tracked.name}"] = tracked;
        }
    }
}