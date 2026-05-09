using System.Collections.Generic;
using UnityEngine;

public class ReplayAvatarDriver : MonoBehaviour
{
    [SerializeField] private Animator humanoidAnimator;
    [SerializeField] private Transform avatarRoot;
    [SerializeField] private bool cacheOnAwake = true;
    [SerializeField] private bool applyLocalPositions = true;
    [SerializeField] private bool applyHipsWorldPose;

    private readonly Dictionary<string, Transform> _targetsById = new();
    private readonly Dictionary<string, Transform> _targetsByName = new();

    public int CachedIdTargetCount => _targetsById.Count;
    public int CachedNameTargetCount => _targetsByName.Count;
    public int LastAppliedPoseCount { get; private set; }
    public int LastMissingPoseCount { get; private set; }

    private void Awake()
    {
        if (cacheOnAwake)
            RebuildCache();
    }

    public void ConfigureSkeleton(Animator animator, Transform root, bool rebuildNow = true)
    {
        humanoidAnimator = animator;
        avatarRoot = root != null ? root : humanoidAnimator != null ? humanoidAnimator.transform : transform;

        if (rebuildNow)
            RebuildCache();
    }

    public void RebuildCache()
    {
        _targetsById.Clear();
        _targetsByName.Clear();

        if (humanoidAnimator == null) humanoidAnimator = GetComponentInChildren<Animator>();
        if (avatarRoot == null)
            avatarRoot = humanoidAnimator != null ? humanoidAnimator.transform : transform;

        MapHumanoid("avatar/hips", HumanBodyBones.Hips);
        MapHumanoid("avatar/spine", HumanBodyBones.Spine);
        MapHumanoid("avatar/spine1", HumanBodyBones.Chest);
        MapHumanoid("avatar/spine2", HumanBodyBones.UpperChest);
        MapHumanoid("avatar/neck", HumanBodyBones.Neck);
        MapHumanoid("avatar/head", HumanBodyBones.Head);
        MapHumanoid("avatar/leftShoulder", HumanBodyBones.LeftShoulder);
        MapHumanoid("avatar/leftUpperArm", HumanBodyBones.LeftUpperArm);
        MapHumanoid("avatar/leftForeArm", HumanBodyBones.LeftLowerArm);
        MapHumanoid("avatar/leftHand", HumanBodyBones.LeftHand);
        MapHumanoid("avatar/rightShoulder", HumanBodyBones.RightShoulder);
        MapHumanoid("avatar/rightUpperArm", HumanBodyBones.RightUpperArm);
        MapHumanoid("avatar/rightForeArm", HumanBodyBones.RightLowerArm);
        MapHumanoid("avatar/rightHand", HumanBodyBones.RightHand);
        MapHumanoid("avatar/leftUpLeg", HumanBodyBones.LeftUpperLeg);
        MapHumanoid("avatar/leftLeg", HumanBodyBones.LeftLowerLeg);
        MapHumanoid("avatar/leftFoot", HumanBodyBones.LeftFoot);
        MapHumanoid("avatar/rightUpLeg", HumanBodyBones.RightUpperLeg);
        MapHumanoid("avatar/rightLeg", HumanBodyBones.RightLowerLeg);
        MapHumanoid("avatar/rightFoot", HumanBodyBones.RightFoot);

        if (avatarRoot != null)
            CacheNamesRecursive(avatarRoot);
    }

    public void ApplyFrame(ReplayFrame frame)
    {
        if (frame == null || frame.avatarBonePoses == null) return;
        if (_targetsById.Count == 0 && _targetsByName.Count == 0) RebuildCache();

        LastAppliedPoseCount = 0;
        LastMissingPoseCount = 0;

        for (int index = 0; index < frame.avatarBonePoses.Count; index++)
        {
            if (ApplyPose(frame.avatarBonePoses[index])) LastAppliedPoseCount++;
            else LastMissingPoseCount++;
        }
    }

    private bool ApplyPose(ReplayPose pose)
    {
        if (pose == null || !pose.valid) return false;
        Transform target = ResolveTarget(pose);
        if (target == null) return false;

        if (applyHipsWorldPose && pose.id == "avatar/hips")
        {
            target.SetPositionAndRotation(pose.position, pose.rotation);
            return true;
        }

        if (applyLocalPositions)
            target.localPosition = pose.localPosition;
        target.localRotation = pose.localRotation;
        return true;
    }

    private Transform ResolveTarget(ReplayPose pose)
    {
        if (!string.IsNullOrEmpty(pose.id) && _targetsById.TryGetValue(pose.id, out Transform byId))
            return byId;

        string normalizedName = Normalize(pose.objectName);
        if (!string.IsNullOrEmpty(normalizedName) && _targetsByName.TryGetValue(normalizedName, out Transform byName))
            return byName;

        return null;
    }

    private void MapHumanoid(string id, HumanBodyBones bone)
    {
        if (humanoidAnimator == null || !humanoidAnimator.isHuman) return;
        Transform boneTransform = humanoidAnimator.GetBoneTransform(bone);
        if (boneTransform != null)
            _targetsById[id] = boneTransform;
    }

    private void CacheNamesRecursive(Transform current)
    {
        string normalized = Normalize(current.name);
        if (!string.IsNullOrEmpty(normalized) && !_targetsByName.ContainsKey(normalized))
            _targetsByName.Add(normalized, current);

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            CacheNamesRecursive(current.GetChild(childIndex));
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace(" ", "").Replace("_", "").Replace(":", "").ToLowerInvariant();
    }
}