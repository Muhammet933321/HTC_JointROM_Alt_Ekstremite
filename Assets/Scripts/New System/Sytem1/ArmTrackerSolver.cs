using UnityEngine;

public class ArmTrackerSolver : MonoBehaviour
{
    [Header("Tracker References")]
    public Transform chestTracker;
    public Transform upperArmTracker;
    public Transform forearmTracker;

    [Header("Bone References")]
    public Transform chestBone;       // Ana g�vde kemi�i
    public Transform shoulderJoint;   // chestBone'un alt objesi (child) olmal�
    public Transform elbowJoint;      // shoulderJoint'un alt objesi (child) olmal�
    public Transform wristJoint;      // elbowJoint'un alt objesi (child) olmal�

    // Kemiklerin kalibrasyon an�ndaki offset'leri
    private Quaternion chestOffset;
    private Quaternion upperArmOffset;
    private Quaternion forearmOffset;

    // Kemiklerin modeldeki orijinal (initial) yerel rotasyonlar�
    private Quaternion shoulderInitialLocalRot;
    private Quaternion elbowInitialLocalRot;
    private Quaternion wristInitialLocalRot;

    // D��ar�dan okunabilen, anl�k hesaplanan yerel rotasyonlar
    public Quaternion CurrentShoulderLocalRotation { get; private set; }
    public Quaternion CurrentElbowLocalRotation { get; private set; }

    // Bu, ArmCalibrationManager'�n kontrol edebilmesi i�in public olmal�
    [HideInInspector]
    public bool calibrated = false;

    // Calibration change detector for consumers
    public int CalibrationVersion { get; private set; } = 0;

    // Kalibrasyon an�ndaki "s�f�r" noktas� rotasyonlar� (A�� hesab� i�in)
    private Quaternion chestCalibRot;
    private Quaternion upperArmCalibRot;
    private Quaternion forearmCalibRot;
    private Quaternion shoulderCalibLocalRot; // Inverse(chestCalibRot) * upperArmCalibRot
    private Quaternion elbowCalibLocalRot;    // Inverse(upperArmCalibRot) * forearmCalibRot

    void Start()
    {
        // Script ba�lad���nda kemiklerin orijinal pozlar�n� kaydet
        // Kalibrasyondan �nce bu de�erlere sahibiz
        if (shoulderJoint) shoulderInitialLocalRot = shoulderJoint.localRotation;
        if (elbowJoint) elbowInitialLocalRot = elbowJoint.localRotation;
        if (wristJoint) wristInitialLocalRot = wristJoint.localRotation;

        // Ba�lang��ta rotasyonlar� identity (bo�) olarak ayarla
        CurrentShoulderLocalRotation = Quaternion.identity;
        CurrentElbowLocalRotation = Quaternion.identity;
    }

    void Update()
    {
        if (!calibrated) return;

        // 1. Her bir tracker'�n "ger�ek d�nya" rotasyonunu hesapla (offset'i uygula)
        // Bu, tracker'�n kemi�e g�re nas�l durdu�unu hesaba katar
        Quaternion chestRot = chestTracker.rotation * chestOffset;
        Quaternion upperArmRot = upperArmTracker.rotation * upperArmOffset;
        Quaternion forearmRot = forearmTracker.rotation * forearmOffset;

        // 2. Ana g�vde kemi�ini s�r
        chestBone.rotation = chestRot;

        // 3. H�YERAR��K (YEREL) ROTASYONLARI HESAPLA
        // Omuzun rotasyonu = G��s�n rotasyonuna g�re �st kolun rotasyonu
        CurrentShoulderLocalRotation = Quaternion.Inverse(chestRot) * upperArmRot;

        // Dirse�in rotasyonu = �st kolun rotasyonuna g�re �n kolun rotasyonu
        CurrentElbowLocalRotation = Quaternion.Inverse(upperArmRot) * forearmRot;

    // 4. HESAPLANAN YEREL ROTASYONLARI MODELE UYGULA
    // Kalibrasyon pozuna g�re delta rotasyon uygula ki kalibrasyon an�nda s��rama olmas�n
    Quaternion shoulderDelta = Quaternion.Inverse(shoulderCalibLocalRot) * CurrentShoulderLocalRotation;
    Quaternion elbowDelta = Quaternion.Inverse(elbowCalibLocalRot) * CurrentElbowLocalRotation;

    shoulderJoint.localRotation = shoulderInitialLocalRot * shoulderDelta;
    elbowJoint.localRotation = elbowInitialLocalRot * elbowDelta;

        // wristJoint i�in bir tracker'�m�z yok, o y�zden sadece dirse�i takip etsin.
        // �ste�e ba�l� olarak buraya bir el rotasyonu eklenebilir.
    }

    public void Calibrate()
    {
        // 1. Tracker'lar�n kemiklere g�re duru� offset'ini kaydet
        chestOffset = Quaternion.Inverse(chestTracker.rotation) * chestBone.rotation;
        upperArmOffset = Quaternion.Inverse(upperArmTracker.rotation) * shoulderJoint.rotation;
        forearmOffset = Quaternion.Inverse(forearmTracker.rotation) * elbowJoint.rotation;

        // 2. A�� hesab� i�in "s�f�r" noktalar�n� kaydet
        // Bu, kalibrasyon pozisyonunu 0 derece kabul etmemizi sa�lar
        chestCalibRot = chestTracker.rotation * chestOffset;
        upperArmCalibRot = upperArmTracker.rotation * upperArmOffset;
    forearmCalibRot = forearmTracker.rotation * forearmOffset;

    // 2.1 Kalibrasyonda yerel (parent'a g�re) rotasyonlar� da kaydet
    shoulderCalibLocalRot = Quaternion.Inverse(chestCalibRot) * upperArmCalibRot;
    elbowCalibLocalRot    = Quaternion.Inverse(upperArmCalibRot) * forearmCalibRot;

        // 3. Mevcut hesaplanan rotasyonlar� "s�f�rla"
        // Kalibrasyon an�nda omuz ve dirsek rotasyonlar� "yok" (identity) kabul edilir
        CurrentShoulderLocalRotation = Quaternion.identity;
        CurrentElbowLocalRotation = Quaternion.identity;

        calibrated = true;
        CalibrationVersion++;
        Debug.Log("[ArmTrackerSolver] Calibration completed.");
    }

    // --- BU METOTLAR ARTIK KULLANILMAMALI (Veya g�ncellenmeli) ---
    // ClinicalArmAngles script'i art�k bu metotlar� kullanmayacak.
    // Onlar yerine CurrentShoulder/ElbowLocalRotation property'lerini kullanacak.
    // public Quaternion GetChestRot() => chestBone.rotation;
    // public Quaternion GetUpperArmRot() => shoulderJoint.rotation;
    // public Quaternion GetForearmRot() => elbowJoint.rotation;

    // Bu metotlara da art�k gerek kalmad�, ��nk� hesaplamay� solver'da yap�yoruz.
    // public Quaternion GetChestCalibRot() => chestCalibRot;
    // public Quaternion GetUpperArmCalibRot() => upperArmCalibRot;
    // public Quaternion GetForearmCalibRot() => forearmCalibRot;
}