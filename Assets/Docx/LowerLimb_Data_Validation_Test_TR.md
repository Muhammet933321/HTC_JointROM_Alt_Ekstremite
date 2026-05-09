# Alt Ekstremite Veri Dogrulama Testi (TR)

Amaç: Build/gözlük üzerinde kalibrasyonun doğru tamamlandığını, açıların gerçek hareket yönüyle uyumlu değiştiğini ve kayıt dosyasının test için incelenebilir olduğunu kontrol etmek.

## Teste Başlamadan Önce

1. `AltEkstremite 2` sahnesiyle build al.
2. Tracker'ları sabitle ve SteamVR/VIVE tarafında hepsinin takipte olduğunu doğrula.
3. Oyunu başlatınca gözlük içinde `ALT EKSTREMITE VERI DOGRULAMA` paneli görünmelidir.
4. Kalibrasyondan önce panelde `Kalibrasyon` ve `Mapping` kırmızı olabilir; bu normaldir.
5. T-pozu kalibrasyonunu tamamladıktan sonra şunlar beklenir:
   - `Tracker Atama: OK`
   - `Mapping: OK`
   - `Kalibrasyon: OK`
   - `IK: OK`
   - `SimulatedInput: KAPALI`
   - `Sol/Sag veri: OK / OK`

Bu projedeki mevcut 3-tracker kurulumunda diz tracker'ı ayrı kullanılmadığı için panelde kaynak olarak genellikle `Avatar IK kemik fallback` görünür. Bu beklenen bir durumdur: açı metrikleri, kalibre edilmiş IK avatar bacak kemiklerinden okunur.

## Otomatik Diagnostic Kaydı

Kalibrasyon tamamlanınca sistem otomatik olarak 180 saniyelik diagnostic CSV kaydı başlatır.

Kayıt yeri:

- Unity karşılığı: `Application.persistentDataPath/Diagnostics/`
- Windows testinde beklenen klasör: `C:\Users\muham\AppData\LocalLow\RaFoRa\HTC_JointROM_Alt_Ekstremite.apk\Diagnostics\`
- Android/VIVE build tarafında beklenen klasör: `Android/data/<uygulama_paket_adi>/files/Diagnostics/`

Dosya adı örneği:

`lower_limb_diagnostics_20260509_184230.csv`

Klavye varsa:

- `F9`: Paneli aç/kapat.
- `F10`: Diagnostic kaydı başlat/durdur.
- `F11`: CSV içinde marker numarasını artır.

Gözlük/controller ile:

- Sol controller `X`: CSV içindeki marker numarasını artırır.
- Sol controller `Y` tuşunu yaklaşık 1.5 saniye basılı tutmak: diagnostic kaydı durdurur veya kalibrasyon hazırsa yeniden başlatır.

## Test Blokları

Her bloğu yaklaşık 8-10 saniye uygula. Paneli gözlükte izle; sonrasında CSV'yi açıp aynı yön değişimlerini kontrol et.

### 1. Nötr Duruş

Hareket: Ayaklar omuz genişliğinde, dizler düz ama kilitlenmemiş, gövde dik, 10 saniye sabit dur.

Beklenen:

- Sol/sağ fleksiyon genellikle `0-15 derece` aralığında kalır.
- Sol/sağ valgus mutlak değer olarak yaklaşık `0-8 derece` bandında kalır.
- Sway RMS idealde `30 mm` altında kalır.
- Panelde `Notr durus kontrolu: OK` görmen iyi işarettir.

Sorun işareti:

- Fleksiyon sürekli 0 ise ve squat yapınca da artmıyorsa açı kaynağı çalışmıyor olabilir.
- Valgus nötrde sürekli çok yüksekse T-pozu/ayak hizası/tracker yerleşimi tekrar kontrol edilmeli.
- `SimulatedInput: ACIK` görünürse gerçek veri okunmuyordur.

### 2. Yavaş Mini Squat

Hareket: 3 tekrar yavaş squat yap, dizleri ayak hizasında tut.

Beklenen:

- Sol/sağ fleksiyon squat sırasında belirgin artar; yaklaşık `25-60 derece` aralığını görmen normaldir.
- Dizler içe kaçmıyorsa valgus çok yükselmemelidir.
- Sağ-sol fleksiyon birbirine yakınsa symmetry düşük kalır.

Sorun işareti:

- Squat sırasında fleksiyon hiç artmıyorsa IK/bone fallback veya kalibrasyon zinciri kontrol edilmeli.
- Bir taraf ters çalışıyorsa sol/sağ tracker/kemik eşleşmesi kontrol edilmeli.

### 3. Bilinçli Valgus Kontrolü

Hareket: Önce nötr dur, sonra kontrollü şekilde dizlerini hafif içe kaçır. Ağrı veya zorlayıcı hareket yapma.

Beklenen:

- Valgus değeri nötre göre artmalıdır.
- Hangi tarafı daha çok içe kaçırıyorsan o taraftaki valgus daha belirgin yükselmelidir.

Sorun işareti:

- Diz içe kaçarken değer negatif yöne gidiyorsa valgus işareti ters olabilir.
- Sol hareket sağ değerde artıyorsa sol/sağ eşleşme karışmış olabilir.

### 4. Tek Ayak Denge

Hareket: Sağ ayakta 5-8 saniye, sonra sol ayakta 5-8 saniye dengede dur.

Beklenen:

- Sway RMS ve sway velocity çift ayak nötr duruşa göre artabilir.
- Stance diz kontrolü bozulursa valgus da artabilir.

Sorun işareti:

- Pelvis sabit dururken sway çok yüksekse kalibrasyon, tracker titreşimi veya HMD/pelvis kaynak seçimi kontrol edilmeli.

### 5. Modified Y-Balance Anterior Reach

Hareket: Bir ayak yerde sabitken diğer ayağı öne uzat.

Beklenen:

- Reach yüzdesi uzanma sırasında artar.
- Daha uzun kontrollü reach daha yüksek yüzde üretir.
- Stance diz içe kaçarsa valgus uyarısı artabilir.

Sorun işareti:

- Öne uzanırken reach hiç değişmiyorsa avatar foot/ankle referansı veya forward yönü kontrol edilmeli.

### 6. Landing / Yük Kabul

Hareket: Küçük ve güvenli bir çift ayak iniş hareketi yap; önce yumuşak iniş, sonra kontrollü şekilde daha sert/daha az diz bükümlü inişi karşılaştır.

Beklenen:

- Yumuşak inişte fleksiyon daha yüksek olur.
- Sert/düşük fleksiyonlu inişte fleksiyon hedefi daha zayıf kalır ve sway/velocity artabilir.
- Diz içe kaçarsa valgus yükselir.

## Hızlı Hata Karar Tablosu

| Panel / CSV bulgusu | Olası anlamı | Yapılacak kontrol |
|---|---|---|
| `SimulatedInput: ACIK` | Gerçek veri yerine simülasyon okunuyor | Sahne ayarı ve `GameFlowController.simulatedMode` kontrol edilir |
| `Tracker Atama: YOK` | Tracker'lar OpenXR tarafında aktif değil veya index eşleşmedi | VIVE/SteamVR tracker bağlantısı, index sırası, pil/takip durumu |
| `Mapping: YOK` | Tracker-target mapping kalibre edilmemiş | T-pozu kalibrasyonu yeniden yapılır |
| `Kalibrasyon: OK`, `IK: YOK` | Calibrator/IK solver durumu uyuşmuyor | Kalibrasyonu tekrar yap; console warning varsa not al |
| `Sol/Sag veri: YOK` | Hip-knee-ankle kaynağı eksik | Fallback kemik referansları veya tracker atamaları kontrol edilir |
| Fleksiyon squat sırasında artmıyor | Açı kaynağı çalışmıyor | IK solver, bone fallback, kalibrasyon sırası kontrol edilir |
| Valgus yönü ters | İşaret konvansiyonu ters olabilir | CSV ile doğrula, gerekirse formülde sign düzeltilir |
| Sol hareket sağ değeri etkiliyor | Sol/sağ eşleşme karışmış olabilir | Tracker indexleri ve avatar bone referansları kontrol edilir |

## Test Sonunda Bana Gönderilecek En Yararlı Bilgi

1. Diagnostic CSV dosyasındaki ilk 30-60 satır.
2. Mini squat sırasında fleksiyonun kaç dereceye çıktığı.
3. Bilinçli valgus sırasında hangi tarafın kaç dereceye çıktığı.
4. Panelde görünen `Kaynak`, `SimulatedInput`, `Kalibrasyon`, `IK`, `Mapping` satırları.

Bu bilgilerle formül yönü, sol/sağ eşleşme ve kalibrasyon zinciri hızlıca doğrulanabilir.