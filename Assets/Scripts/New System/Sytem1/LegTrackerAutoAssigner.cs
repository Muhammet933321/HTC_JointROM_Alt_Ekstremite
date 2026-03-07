using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// Aktif HTC VIVE Ultimate Tracker'ları tespit eder ve sırasıyla
/// LegTrackerSolver'a (pelvis, thigh, shin) otomatik atar.
///
/// Kullanım:
/// 1) Sahnedeki tüm tracker Transform'larını (0, 1, 2, 3, 4) sırasıyla allTrackers dizisine ekleyin.
/// 2) legSolver alanına LegTrackerSolver bileşenini atayın.
/// 3) Çalışma zamanında script aktif tracker'ları tespit eder ve ilk 3'ünü otomatik atar.
///
/// Örnek: Tracker 0, 2, 3, 4 aktifse → Pelvis=0, Thigh=2, Shin=3
/// </summary>
public class LegTrackerAutoAssigner : MonoBehaviour
{
    [Header("Tüm Tracker Transform'ları (sıralı: 0, 1, 2, ...)")]
    [Tooltip("Sahnedeki tracker objeleri sırası ile. Index 0 = Ultimate Tracker 0, Index 1 = Ultimate Tracker 1, vs.")]
    public Transform[] allTrackers;

    [Header("Hedef Solver")]
    public LegTrackerSolver legSolver;

    [Header("UI")]
    public TextMeshProUGUI statusText;

    [Header("Ayarlar")]
    [Tooltip("Aktif tracker taraması yapma sıklığı (saniye)")]
    public float scanInterval = 1f;

    [Tooltip("Atama için gereken minimum aktif tracker sayısı")]
    public int requiredCount = 3;

    // Atanan tracker indeksleri (-1 = atanmadı)
    private int _pelvisIndex = -1;
    private int _thighIndex = -1;
    private int _shinIndex = -1;

    private float _scanTimer;
    private readonly List<InputDevice> _devices = new List<InputDevice>();
    private readonly List<int> _activeIndices = new List<int>();
    private bool _assigned = false;

    void Start()
    {
        ScanAndAssign();
    }

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            ScanAndAssign();
        }
    }

    /// <summary>
    /// Aktif tracker'ları tarar ve ilk 3'ünü solver'a atar.
    /// Manuel olarak da çağrılabilir.
    /// </summary>
    public void ScanAndAssign()
    {
        _devices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.HardwareTracker, _devices);

        _activeIndices.Clear();

        // Her bir cihaz için isTracked kontrolü yap
        for (int i = 0; i < _devices.Count; i++)
        {
            bool isTracked = false;
            _devices[i].TryGetFeatureValue(CommonUsages.isTracked, out isTracked);

            if (isTracked && i < allTrackers.Length && allTrackers[i] != null)
            {
                _activeIndices.Add(i);
            }
        }

        // Sıralı tutulması zaten garanti (0'dan itibaren taranıyor)
        // İlk 3 aktif tracker'ı ata
        if (_activeIndices.Count >= requiredCount && legSolver != null)
        {
            _pelvisIndex = _activeIndices[0];
            _thighIndex = _activeIndices[1];
            _shinIndex = _activeIndices[2];

            legSolver.pelvisTracker = allTrackers[_pelvisIndex];
            legSolver.thighTracker = allTrackers[_thighIndex];
            legSolver.shinTracker = allTrackers[_shinIndex];

            _assigned = true;

            Debug.Log($"[LegTrackerAutoAssigner] Atama yapıldı → Pelvis: Tracker {_pelvisIndex}, " +
                      $"Üst Bacak: Tracker {_thighIndex}, Alt Bacak: Tracker {_shinIndex}");
        }
        else
        {
            _assigned = false;
        }

        UpdateStatusUI();
    }

    private void UpdateStatusUI()
    {
        if (!statusText) return;

        // Aktif tracker listesi
        string activeList = _activeIndices.Count > 0
            ? string.Join(", ", _activeIndices)
            : "Yok";

        // Toplam tespit edilen cihaz sayısı
        string deviceInfo = $"Tespit edilen cihaz: {_devices.Count}";

        // Atama durumu
        string assignmentInfo;
        if (_assigned)
        {
            assignmentInfo = $"Pelvis: Tracker {_pelvisIndex}\n" +
                           $"Üst Bacak: Tracker {_thighIndex}\n" +
                           $"Alt Bacak: Tracker {_shinIndex}";
        }
        else
        {
            assignmentInfo = $"Atama yapılamadı (en az {requiredCount} aktif tracker gerekli)";
        }

        statusText.text = $"{deviceInfo}\nAktif: [{activeList}]\n{assignmentInfo}";
    }

    /// <summary>
    /// Mevcut atama bilgisini döndürür. Atama yapılmadıysa false döner.
    /// </summary>
    public bool GetAssignment(out int pelvis, out int thigh, out int shin)
    {
        pelvis = _pelvisIndex;
        thigh = _thighIndex;
        shin = _shinIndex;
        return _assigned;
    }
}
