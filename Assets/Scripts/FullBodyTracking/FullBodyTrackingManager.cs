using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// Manages full-body tracking device detection and assignment.
/// Detects HMD, controllers, and trackers via OpenXR, then assigns
/// them to the FullBodyIKSolver targets based on spatial analysis.
///
/// Auto-assignment logic:
/// - Highest tracker → Pelvis
/// - Lower-left tracker → Left foot
/// - Lower-right tracker → Right foot
/// - (Optional) Mid-height trackers → Knees
/// </summary>
[DisallowMultipleComponent]
public class FullBodyTrackingManager : MonoBehaviour
{
    private enum TrackerAssignmentMode
    {
        AutoBySpatialAnalysis,
        ExplicitIndices
    }

    private enum TorsoTrackerMount
    {
        Waist,
        Chest
    }

    [Header("=== Tracker Transform'ları ===")]
    [Tooltip("Sahnedeki tüm olası tracker Transform'ları (sıralı: 0, 1, 2, ...)")]
    [SerializeField] private Transform[] allTrackerTransforms;

    [Header("=== Sabit Cihaz Referansları ===")]
    [Tooltip("HMD (XR Camera) Transform")]
    [SerializeField] private Transform hmdTransform;
    [Tooltip("Sol kontrolcü Transform")]
    [SerializeField] private Transform leftControllerTransform;
    [Tooltip("Sağ kontrolcü Transform")]
    [SerializeField] private Transform rightControllerTransform;

    [Header("=== Hedef IK Solver ===")]
    [SerializeField] private FullBodyIKSolver ikSolver;

    [Header("=== IK Target Objeleri ===")]
    [Tooltip("IK solver'a bağlanacak hedef objeler. Bunlar tracker/controller pozisyonlarını takip eder.")]
    [SerializeField] private Transform headIKTarget;
    [SerializeField] private Transform leftHandIKTarget;
    [SerializeField] private Transform rightHandIKTarget;
    [SerializeField] private Transform pelvisIKTarget;
    [SerializeField] private Transform leftFootIKTarget;
    [SerializeField] private Transform rightFootIKTarget;
    [Tooltip("Opsiyonel diz IK hedefleri")]
    [SerializeField] private Transform leftKneeIKTarget;
    [SerializeField] private Transform rightKneeIKTarget;

    [Header("=== UI ===")]
    [SerializeField] private TMP_Text statusText;

    [Header("=== Ayarlar ===")]
    [Tooltip("Tracker tarama sıklığı (saniye)")]
    [SerializeField] private float scanInterval = 1f;
    [Tooltip("Minimum gerekli tracker sayısı (3 = pelvis + 2 ayak)")]
    [SerializeField] private int requiredTrackerCount = 3;
    [Tooltip("5 tracker varsa 2'si diz olarak atansın mı?")]
    [SerializeField] private bool enableKneeTrackers = false;

    [Header("=== Tracker Atama Modu ===")]
    [Tooltip("Auto: aktif tracker'ları uzaysal analizle otomatik atar. Explicit: index'leri elle sabitlersiniz.")]
    [SerializeField] private TrackerAssignmentMode assignmentMode = TrackerAssignmentMode.ExplicitIndices;

    [Header("=== Explicit Tracker Index Atamaları ===")]
    [Tooltip("Torso tracker index'i (bel veya gogus).")]
    [SerializeField] private int torsoTrackerIndex = 0;
    [SerializeField] private int leftFootTrackerIndex = 1;
    [SerializeField] private int rightFootTrackerIndex = 2;
    [SerializeField] private int leftKneeTrackerIndex = 3;
    [SerializeField] private int rightKneeTrackerIndex = 4;

    [Header("=== Torso Tracker Donusumu ===")]
    [Tooltip("Torso tracker beldeyse Waist, gogusteyse Chest secin.")]
    [SerializeField] private TorsoTrackerMount torsoTrackerMount = TorsoTrackerMount.Waist;
    [Tooltip("Chest modunda tracker'dan pelvise lokal ofset (metre).")]
    [SerializeField] private Vector3 chestToPelvisOffset = new(0f, -0.27f, 0.03f);
    [Tooltip("Pelvis rotasyonunda sadece yaw (Y ekseni) kullanilsin.")]
    [SerializeField] private bool pelvisYawOnly = true;

    [Header("=== Explicit Atama Dayanimi ===")]
    [Tooltip("Explicit indexlerden biri aktif degilse, otomatik uzaysal atamaya duser.")]
    [SerializeField] private bool fallbackToAutoWhenExplicitInvalid = true;

    [Header("=== Debug ===")]
    [SerializeField] private bool verboseRuntimeLogs = false;

    // Internal state
    private readonly List<InputDevice> _devices = new();
    private readonly List<int> _activeTrackerIndices = new();
    private float _scanTimer;
    private bool _assigned;

    // Assignment indices
    private int _pelvisIdx = -1;
    private int _leftFootIdx = -1;
    private int _rightFootIdx = -1;
    private int _leftKneeIdx = -1;
    private int _rightKneeIdx = -1;

    // ───────────────────────── Delta Mapping ─────────────────────────

    private struct CalibrationPair
    {
        public Vector3 devicePos;
        public Quaternion deviceRot;
        public Vector3 targetPos;
        public Quaternion targetRot;
    }

    private bool _mappingCalibrated;
    private CalibrationPair _calibHead;
    private CalibrationPair _calibLeftHand;
    private CalibrationPair _calibRightHand;
    private CalibrationPair _calibPelvis;
    private CalibrationPair _calibLeftFoot;
    private CalibrationPair _calibRightFoot;
    private CalibrationPair _calibLeftKnee;
    private CalibrationPair _calibRightKnee;

    // ───────────────────────── Unity Lifecycle ─────────────────────────

    private void Start()
    {
        ScanAndAssign();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            ScanAndAssign();
        }

        // Drive IK targets to follow tracked devices
        if (_assigned)
        {
            DriveIKTargets();
        }
    }

    // ───────────────────────── Scanning ─────────────────────────

    /// <summary>
    /// Scans for active trackers and assigns them based on spatial position.
    /// </summary>
    public void ScanAndAssign()
    {
        if (allTrackerTransforms == null || allTrackerTransforms.Length == 0)
        {
            _assigned = false;
            _pelvisIdx = _leftFootIdx = _rightFootIdx = _leftKneeIdx = _rightKneeIdx = -1;
            UpdateStatusUI();
            return;
        }

        _devices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.HardwareTracker, _devices);

        _activeTrackerIndices.Clear();

        for (int i = 0; i < _devices.Count; i++)
        {
            _devices[i].TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked);
            if (isTracked && i < allTrackerTransforms.Length && allTrackerTransforms[i] != null)
            {
                _activeTrackerIndices.Add(i);
            }
        }

        if (_activeTrackerIndices.Count >= requiredTrackerCount)
        {
            if (assignmentMode == TrackerAssignmentMode.ExplicitIndices)
            {
                _assigned = AssignTrackersByExplicitIndices();

                if (!_assigned && fallbackToAutoWhenExplicitInvalid)
                {
                    AssignTrackersBySpatialAnalysis();
                    _assigned = _pelvisIdx >= 0 && _leftFootIdx >= 0 && _rightFootIdx >= 0;
                }
            }
            else
            {
                AssignTrackersBySpatialAnalysis();
                _assigned = _pelvisIdx >= 0 && _leftFootIdx >= 0 && _rightFootIdx >= 0;
            }

            if (!_assigned)
            {
                _pelvisIdx = _leftFootIdx = _rightFootIdx = _leftKneeIdx = _rightKneeIdx = -1;
            }
        }
        else
        {
            _assigned = false;
            _pelvisIdx = _leftFootIdx = _rightFootIdx = _leftKneeIdx = _rightKneeIdx = -1;
        }

        UpdateStatusUI();
    }

    // ───────────────────────── Spatial Assignment ─────────────────────────

    private bool AssignTrackersByExplicitIndices()
    {
        if (!IsTrackerIndexActiveAndValid(torsoTrackerIndex) ||
            !IsTrackerIndexActiveAndValid(leftFootTrackerIndex) ||
            !IsTrackerIndexActiveAndValid(rightFootTrackerIndex))
        {
            return false;
        }

        _pelvisIdx = torsoTrackerIndex;
        _leftFootIdx = leftFootTrackerIndex;
        _rightFootIdx = rightFootTrackerIndex;

        if (enableKneeTrackers)
        {
            bool hasLeftKnee = IsTrackerIndexActiveAndValid(leftKneeTrackerIndex);
            bool hasRightKnee = IsTrackerIndexActiveAndValid(rightKneeTrackerIndex);

            _leftKneeIdx = hasLeftKnee ? leftKneeTrackerIndex : -1;
            _rightKneeIdx = hasRightKnee ? rightKneeTrackerIndex : -1;
        }
        else
        {
            _leftKneeIdx = -1;
            _rightKneeIdx = -1;
        }

        return true;
    }

    private void AssignTrackersBySpatialAnalysis()
    {
        // Gather active tracker positions
        List<TrackerInfo> trackers = new();
        foreach (int idx in _activeTrackerIndices)
        {
            trackers.Add(new TrackerInfo
            {
                index = idx,
                position = allTrackerTransforms[idx].position
            });
        }

        // Sort by height (Y) descending
        trackers.Sort((a, b) => b.position.y.CompareTo(a.position.y));

        // Highest = Pelvis
        _pelvisIdx = trackers[0].index;

        if (enableKneeTrackers && trackers.Count >= 5)
        {
            // 5+ trackers: [0]=pelvis, [1-2]=knees, [3-4]=feet
            AssignLeftRight(trackers[1], trackers[2], out _leftKneeIdx, out _rightKneeIdx);
            AssignLeftRight(trackers[3], trackers[4], out _leftFootIdx, out _rightFootIdx);
        }
        else if (trackers.Count >= 3)
        {
            // 3 trackers: [0]=pelvis, [1-2]=feet
            // (or more trackers but knee tracking disabled — just use lowest 2)
            int footA = trackers.Count - 2;
            int footB = trackers.Count - 1;
            AssignLeftRight(trackers[footA], trackers[footB], out _leftFootIdx, out _rightFootIdx);
            _leftKneeIdx = -1;
            _rightKneeIdx = -1;
        }

        if (verboseRuntimeLogs)
        {
            Debug.Log($"[FullBodyTrackingManager] Atama: Pelvis=T{_pelvisIdx}, " +
                      $"SolAyak=T{_leftFootIdx}, SağAyak=T{_rightFootIdx}, " +
                      $"SolDiz=T{_leftKneeIdx}, SağDiz=T{_rightKneeIdx}");
        }
    }

    /// <summary>
    /// Assigns left/right based on X position relative to the camera's forward direction.
    /// Left = negative local X, Right = positive local X.
    /// </summary>
    private void AssignLeftRight(TrackerInfo a, TrackerInfo b, out int leftIdx, out int rightIdx)
    {
        // Use HMD as reference for left/right determination
        Vector3 reference = hmdTransform != null ? hmdTransform.position : Vector3.zero;
        Vector3 rightRef = hmdTransform != null ? hmdTransform.right : Vector3.right;

        // Project both trackers onto the left-right axis
        float aLR = Vector3.Dot(a.position - reference, rightRef);
        float bLR = Vector3.Dot(b.position - reference, rightRef);

        if (aLR < bLR)
        {
            // a is more to the left
            leftIdx = a.index;
            rightIdx = b.index;
        }
        else
        {
            leftIdx = b.index;
            rightIdx = a.index;
        }
    }

    private bool IsTrackerIndexActiveAndValid(int index)
    {
        if (index < 0 || index >= allTrackerTransforms.Length) return false;
        if (allTrackerTransforms[index] == null) return false;
        return _activeTrackerIndices.Contains(index);
    }

    // ───────────────────────── Drive IK Targets ─────────────────────────

    private void DriveIKTargets()
    {
        // HMD → Head target
        if (hmdTransform && headIKTarget)
        {
            ApplyMapping(hmdTransform, headIKTarget, ref _calibHead);
        }

        // Controllers → Hand targets
        if (leftControllerTransform && leftHandIKTarget)
        {
            ApplyMapping(leftControllerTransform, leftHandIKTarget, ref _calibLeftHand);
        }
        if (rightControllerTransform && rightHandIKTarget)
        {
            ApplyMapping(rightControllerTransform, rightHandIKTarget, ref _calibRightHand);
        }

        // Trackers → Body targets
        if (_pelvisIdx >= 0 && pelvisIKTarget)
        {
            Transform t = allTrackerTransforms[_pelvisIdx];
            Vector3 devicePos = t.position;
            Quaternion deviceRot = t.rotation;

            if (torsoTrackerMount == TorsoTrackerMount.Chest)
            {
                devicePos += t.rotation * chestToPelvisOffset;
            }

            if (_mappingCalibrated)
            {
                Vector3 posDelta = devicePos - _calibPelvis.devicePos;
                Quaternion rotDelta = deviceRot * Quaternion.Inverse(_calibPelvis.deviceRot);

                if (pelvisYawOnly)
                {
                    Vector3 euler = rotDelta.eulerAngles;
                    rotDelta = Quaternion.Euler(0f, euler.y, 0f);
                }

                pelvisIKTarget.SetPositionAndRotation(
                    _calibPelvis.targetPos + posDelta,
                    rotDelta * _calibPelvis.targetRot);
            }
            else
            {
                if (pelvisYawOnly)
                {
                    Vector3 euler = deviceRot.eulerAngles;
                    deviceRot = Quaternion.Euler(0f, euler.y, 0f);
                }
                pelvisIKTarget.SetPositionAndRotation(devicePos, deviceRot);
            }
        }

        if (_leftFootIdx >= 0 && leftFootIKTarget)
        {
            ApplyMapping(allTrackerTransforms[_leftFootIdx], leftFootIKTarget, ref _calibLeftFoot);
        }

        if (_rightFootIdx >= 0 && rightFootIKTarget)
        {
            ApplyMapping(allTrackerTransforms[_rightFootIdx], rightFootIKTarget, ref _calibRightFoot);
        }

        // Optional knee trackers
        if (_leftKneeIdx >= 0 && leftKneeIKTarget)
        {
            ApplyMapping(allTrackerTransforms[_leftKneeIdx], leftKneeIKTarget, ref _calibLeftKnee);
        }

        if (_rightKneeIdx >= 0 && rightKneeIKTarget)
        {
            ApplyMapping(allTrackerTransforms[_rightKneeIdx], rightKneeIKTarget, ref _calibRightKnee);
        }
    }

    private void ApplyMapping(Transform device, Transform ikTarget, ref CalibrationPair pair)
    {
        if (_mappingCalibrated)
        {
            Vector3 posDelta = device.position - pair.devicePos;
            Quaternion rotDelta = device.rotation * Quaternion.Inverse(pair.deviceRot);
            ikTarget.SetPositionAndRotation(
                pair.targetPos + posDelta,
                rotDelta * pair.targetRot);
        }
        else
        {
            ikTarget.SetPositionAndRotation(device.position, device.rotation);
        }
    }

    // ───────────────────────── UI ─────────────────────────

    private void UpdateStatusUI()
    {
        if (!statusText) return;

        string active = _activeTrackerIndices.Count > 0
            ? string.Join(", ", _activeTrackerIndices)
            : "Yok";

        string assignment;
        if (_assigned)
        {
            assignment = $"Pelvis: Tracker {_pelvisIdx}\n" +
                         $"Sol Ayak: Tracker {_leftFootIdx}\n" +
                         $"Sağ Ayak: Tracker {_rightFootIdx}\n" +
                         $"Mod: {(assignmentMode == TrackerAssignmentMode.ExplicitIndices ? "Explicit" : "Auto")}";
            if (_leftKneeIdx >= 0)
                assignment += $"\nSol Diz: Tracker {_leftKneeIdx}";
            if (_rightKneeIdx >= 0)
                assignment += $"\nSağ Diz: Tracker {_rightKneeIdx}";

            assignment += $"\nTorso Mount: {(torsoTrackerMount == TorsoTrackerMount.Chest ? "Chest" : "Waist")}";
        }
        else
        {
            assignment = $"Atama yapılamadı (en az {requiredTrackerCount} aktif tracker gerekli)";
        }

        statusText.text = $"Cihaz sayısı: {_devices.Count}\n" +
                          $"Aktif tracker: [{active}]\n" +
                          $"HMD: {(hmdTransform ? "Var" : "Yok")}\n" +
                          $"Kontrolcüler: {(leftControllerTransform ? "L" : "-")}/{(rightControllerTransform ? "R" : "-")}\n" +
                          $"Hand Source: {(leftControllerTransform && rightControllerTransform ? "Controllers" : "Controller Missing")}\n" +
                          $"{assignment}";
    }

    // ───────────────────────── Helper Struct ─────────────────────────

    private struct TrackerInfo
    {
        public int index;
        public Vector3 position;
    }

    // ───────────────────────── Public Accessors ─────────────────────────

    public bool IsAssigned => _assigned;
    public bool IsMappingCalibrated => _mappingCalibrated;

    /// <summary>
    /// Forces an immediate scan and target update in the same frame.
    /// Useful before calibration so offsets are computed from up-to-date targets.
    /// </summary>
    public void RefreshAndDriveTargetsNow()
    {
        ScanAndAssign();
        if (_assigned)
        {
            DriveIKTargets();
        }
    }

    /// <summary>
    /// Captures current device↔IK-target pairs for delta-based mapping.
    /// Call AFTER IK targets have been snapped to avatar bones (T-pose).
    /// </summary>
    public void CalibrateMapping()
    {
        if (!_assigned)
        {
            Debug.LogWarning("[FullBodyTrackingManager] CalibrateMapping failed: trackers not assigned.");
            return;
        }

        // HMD
        if (hmdTransform && headIKTarget)
            _calibHead = CaptureCalibrationPair(hmdTransform.position, hmdTransform.rotation, headIKTarget);

        // Controllers
        if (leftControllerTransform && leftHandIKTarget)
            _calibLeftHand = CaptureCalibrationPair(leftControllerTransform.position, leftControllerTransform.rotation, leftHandIKTarget);
        if (rightControllerTransform && rightHandIKTarget)
            _calibRightHand = CaptureCalibrationPair(rightControllerTransform.position, rightControllerTransform.rotation, rightHandIKTarget);

        // Pelvis (apply chest offset if needed)
        if (_pelvisIdx >= 0 && pelvisIKTarget)
        {
            Transform t = allTrackerTransforms[_pelvisIdx];
            Vector3 dPos = t.position;
            if (torsoTrackerMount == TorsoTrackerMount.Chest)
                dPos += t.rotation * chestToPelvisOffset;
            _calibPelvis = CaptureCalibrationPair(dPos, t.rotation, pelvisIKTarget);
        }

        // Feet
        if (_leftFootIdx >= 0 && leftFootIKTarget)
            _calibLeftFoot = CaptureCalibrationPair(allTrackerTransforms[_leftFootIdx], leftFootIKTarget);
        if (_rightFootIdx >= 0 && rightFootIKTarget)
            _calibRightFoot = CaptureCalibrationPair(allTrackerTransforms[_rightFootIdx], rightFootIKTarget);

        // Knees
        if (_leftKneeIdx >= 0 && leftKneeIKTarget)
            _calibLeftKnee = CaptureCalibrationPair(allTrackerTransforms[_leftKneeIdx], leftKneeIKTarget);
        if (_rightKneeIdx >= 0 && rightKneeIKTarget)
            _calibRightKnee = CaptureCalibrationPair(allTrackerTransforms[_rightKneeIdx], rightKneeIKTarget);

        _mappingCalibrated = true;
        Debug.Log("[FullBodyTrackingManager] Delta mapping calibrated.");
    }

    private static CalibrationPair CaptureCalibrationPair(Transform device, Transform target)
    {
        return new CalibrationPair
        {
            devicePos = device.position,
            deviceRot = device.rotation,
            targetPos = target.position,
            targetRot = target.rotation
        };
    }

    private static CalibrationPair CaptureCalibrationPair(Vector3 devicePos, Quaternion deviceRot, Transform target)
    {
        return new CalibrationPair
        {
            devicePos = devicePos,
            deviceRot = deviceRot,
            targetPos = target.position,
            targetRot = target.rotation
        };
    }

    public bool GetAssignment(out int pelvis, out int leftFoot, out int rightFoot,
                               out int leftKnee, out int rightKnee)
    {
        pelvis = _pelvisIdx;
        leftFoot = _leftFootIdx;
        rightFoot = _rightFootIdx;
        leftKnee = _leftKneeIdx;
        rightKnee = _rightKneeIdx;
        return _assigned;
    }
}
