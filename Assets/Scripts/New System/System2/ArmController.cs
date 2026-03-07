using UnityEngine;

public class ArmController : MonoBehaviour
{
    [Header("Tracker Objeleri")]
    public Transform chestTracker;
    public Transform upperArmTracker; // Dirsek ile omuz arasýndaki tracker
    public Transform forearmTracker;  // Bilek ile dirsek arasýndaki tracker

    [Header("Kolun Eklem (Joint) Objeleri")]
    public Transform omuzJoint;
    public Transform dirsekJoint;
    public Transform bilekJoint; // Elin pozisyonu için gerekli

    // Kalibrasyon sýrasýnda alýnacak baþlangýç rotasyonlarý
    private Quaternion initialChestRotation;
    private Quaternion initialUpperArmRotation;
    private Quaternion initialForearmRotation;

    // Eklemlerin baþlangýçtaki yerel rotasyonlarý
    private Quaternion initialOmuzLocalRotation;
    private Quaternion initialDirsekLocalRotation;

    private bool isCalibrated = false;

    private void Start()
    {
        Calibrate();
    }
    void Update()
    {

        // Sadece kalibrasyon yapýldýysa kolu güncelle
        if (isCalibrated)
        {
            UpdateArmPose();
        }
    }

    /// <summary>
    /// Kullanýcý T-Pose pozisyonundayken çaðrýlýr.
    /// Tracker'larýn ve eklemlerin baþlangýç rotasyonlarýný kaydeder.
    /// </summary>
    public void Calibrate()
    {
        Debug.Log("Kalibrasyon Baþladý!");

        // Tracker'larýn baþlangýçtaki dünya (world) rotasyonlarýný kaydet
        initialChestRotation = chestTracker.rotation;
        initialUpperArmRotation = upperArmTracker.rotation;
        initialForearmRotation = forearmTracker.rotation;

        // Eklemlerin baþlangýçtaki yerel (local) rotasyonlarýný kaydet
        // Bu, modelin orijinal duruþunu korumak için önemlidir.
        initialOmuzLocalRotation = omuzJoint.localRotation;
        initialDirsekLocalRotation = dirsekJoint.localRotation;

        isCalibrated = true;
        Debug.Log("Kalibrasyon Tamamlandý!");
    }

    /// <summary>
    /// Her frame'de kolun pozunu günceller.
    /// </summary>
    void UpdateArmPose()
    {
        // 1. Omuz Rotasyonunu Hesapla
        // Gövdenin mevcut dönüþünü hesapla
        Quaternion chestRotationOffset = chestTracker.rotation * Quaternion.Inverse(initialChestRotation);
        // Üst kolun mevcut dönüþünü hesapla
        Quaternion upperArmRotationOffset = upperArmTracker.rotation * Quaternion.Inverse(initialUpperArmRotation);

        // Omuzun saf yerel dönüþünü bulmak için, üst kolun dönüþünden gövdenin dönüþünü çýkar.
        // Quaternion'larda bölme iþlemi, tersiyle (inverse) çarpmak anlamýna gelir.
        Quaternion omuzLocalRotation = Quaternion.Inverse(chestRotationOffset) * upperArmRotationOffset;

        // Hesaplanan rotasyonu, modelin orijinal duruþuna ekleyerek uygula
        omuzJoint.localRotation = initialOmuzLocalRotation * omuzLocalRotation;


        // 2. Dirsek Rotasyonunu Hesapla
        // Ön kolun mevcut dönüþünü hesapla
        Quaternion forearmRotationOffset = forearmTracker.rotation * Quaternion.Inverse(initialForearmRotation);

        // Dirseðin saf yerel dönüþünü bulmak için, ön kolun dönüþünden üst kolun dönüþünü çýkar.
        Quaternion dirsekLocalRotation = Quaternion.Inverse(upperArmRotationOffset) * forearmRotationOffset;

        // Hesaplanan rotasyonu, modelin orijinal duruþuna ekleyerek uygula
        dirsekJoint.localRotation = initialDirsekLocalRotation * dirsekLocalRotation;

        // Bilek için özel bir tracker olmadýðýndan, o sadece dirseði takip edecektir.
        // Bu yüzden bilekJoint'e ek bir kod yazmýyoruz.
    }
}