using UnityEngine;

public class LegTrackerSolver : MonoBehaviour
{
    [Header("Tracker References")]
    public Transform pelvisTracker;
    public Transform thighTracker;
    public Transform shinTracker;

    [Header("Bone References")]
    public Transform pelvisBone;      // Ana pelvis kemiği
    public Transform hipJoint;        // pelvisBone'un alt objesi (child) olmalı
    public Transform kneeJoint;       // hipJoint'un alt objesi (child) olmalı
    public Transform ankleJoint;      // kneeJoint'un alt objesi (child) olmalı

    // Kemiklerin kalibrasyon anındaki offset'leri
    private Quaternion pelvisOffset;
    private Quaternion thighOffset;
    private Quaternion shinOffset;

    // Kemiklerin modeldeki orijinal (initial) yerel rotasyonları
    private Quaternion hipInitialLocalRot;
    private Quaternion kneeInitialLocalRot;
    private Quaternion ankleInitialLocalRot;

    // Dışarıdan okunabilen, anlık hesaplanan yerel rotasyonlar
    public Quaternion CurrentHipLocalRotation { get; private set; }
    public Quaternion CurrentKneeLocalRotation { get; private set; }

    // Bu, LegCalibrationManager'ın kontrol edebilmesi için public olmalı
    [HideInInspector]
    public bool calibrated = false;

    // Calibration change detector for consumers
    public int CalibrationVersion { get; private set; } = 0;

    // Kalibrasyon anındaki "sıfır" noktası rotasyonları (Açı hesabı için)
    private Quaternion pelvisCalibRot;
    private Quaternion thighCalibRot;
    private Quaternion shinCalibRot;
    private Quaternion hipCalibLocalRot;   // Inverse(pelvisCalibRot) * thighCalibRot
    private Quaternion kneeCalibLocalRot;  // Inverse(thighCalibRot) * shinCalibRot

    void Start()
    {
        // Script başladığında kemiklerin orijinal pozlarını kaydet
        if (hipJoint) hipInitialLocalRot = hipJoint.localRotation;
        if (kneeJoint) kneeInitialLocalRot = kneeJoint.localRotation;
        if (ankleJoint) ankleInitialLocalRot = ankleJoint.localRotation;

        // Başlangıçta rotasyonları identity olarak ayarla
        CurrentHipLocalRotation = Quaternion.identity;
        CurrentKneeLocalRotation = Quaternion.identity;
    }

    void Update()
    {
        if (!calibrated) return;

        // 1. Her bir tracker'ın "gerçek dünya" rotasyonunu hesapla (offset'i uygula)
        Quaternion pelvisRot = pelvisTracker.rotation * pelvisOffset;
        Quaternion thighRot = thighTracker.rotation * thighOffset;
        Quaternion shinRot = shinTracker.rotation * shinOffset;

        // 2. Ana pelvis kemiğini sür
        pelvisBone.rotation = pelvisRot;

        // 3. HİYERARŞİK (YEREL) ROTASYONLARI HESAPLA
        // Kalçanın rotasyonu = Pelvisin rotasyonuna göre üst bacağın rotasyonu
        CurrentHipLocalRotation = Quaternion.Inverse(pelvisRot) * thighRot;

        // Diz rotasyonu = Üst bacağın rotasyonuna göre alt bacağın rotasyonu
        CurrentKneeLocalRotation = Quaternion.Inverse(thighRot) * shinRot;

        // 4. HESAPLANAN YEREL ROTASYONLARI MODELE UYGULA
        // Kalibrasyon pozuna göre delta rotasyon uygula ki kalibrasyon anında sıçrama olmasın
        Quaternion hipDelta = Quaternion.Inverse(hipCalibLocalRot) * CurrentHipLocalRotation;
        Quaternion kneeDelta = Quaternion.Inverse(kneeCalibLocalRot) * CurrentKneeLocalRotation;

        hipJoint.localRotation = hipInitialLocalRot * hipDelta;
        kneeJoint.localRotation = kneeInitialLocalRot * kneeDelta;

        // ankleJoint için bir tracker'mız yok, o yüzden sadece dizi takip etsin.
    }

    public void Calibrate()
    {
        // 1. Tracker'ların kemiklere göre duruş offset'ini kaydet
        pelvisOffset = Quaternion.Inverse(pelvisTracker.rotation) * pelvisBone.rotation;
        thighOffset = Quaternion.Inverse(thighTracker.rotation) * hipJoint.rotation;
        shinOffset = Quaternion.Inverse(shinTracker.rotation) * kneeJoint.rotation;

        // 2. Açı hesabı için "sıfır" noktalarını kaydet
        pelvisCalibRot = pelvisTracker.rotation * pelvisOffset;
        thighCalibRot = thighTracker.rotation * thighOffset;
        shinCalibRot = shinTracker.rotation * shinOffset;

        // 2.1 Kalibrasyonda yerel (parent'a göre) rotasyonları da kaydet
        hipCalibLocalRot = Quaternion.Inverse(pelvisCalibRot) * thighCalibRot;
        kneeCalibLocalRot = Quaternion.Inverse(thighCalibRot) * shinCalibRot;

        // 3. Mevcut hesaplanan rotasyonları "sıfırla"
        CurrentHipLocalRotation = Quaternion.identity;
        CurrentKneeLocalRotation = Quaternion.identity;

        calibrated = true;
        CalibrationVersion++;
        Debug.Log("[LegTrackerSolver] Kalibrasyon tamamlandı.");
    }
}
