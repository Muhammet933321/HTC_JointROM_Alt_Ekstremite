# Oyun Tekrarı Kayıt Sistemi

Bu sistem oturumu video olarak değil, Unity içinde tekrar üretilebilen veri paketi olarak kaydeder. Amaç gözlükte oynanan oyunu daha sonra Unity Editor veya standalone build içinde yeniden oynatmak, kullanıcının o anda hangi görevi yapmaya çalıştığını görmek ve hareket/metrikleri aynı zaman çizgisi üzerinde incelemektir.

## Kayıt Ne Zaman Başlar?

- `ReplayRecorder`, aktif sahnede `[Gamification]` objesine bağlıdır.
- Oturum `TaskSequencer` ile başladığında kayıt otomatik başlar.
- Kayıt kalibrasyon sonrası session akışını kapsar: geri sayım, ölçüm, dinlenme, görev başlangıç/bitişleri, sonuçlar ve session bitişi.
- Oturum normal biterse kayıt otomatik finalize edilir.
- Uygulama kapanırsa veya component kapanırsa mevcut kayıt güvenli şekilde finalize edilmeye çalışılır.
- Final release sahnesinde `LowerLimbDiagnosticsOverlay` otomatik CSV kaydı kapalıdır; tam oyun tekrarı için ana kayıt yolu `ReplayRecorder` sistemidir.

## Dosyalar Nereye Kaydedilir?

Headset/build içinde dosyalar şu klasöre yazılır:

```text
Application.persistentDataPath/Replays/replay_yyyyMMdd_HHmmss/
```

PC build örneği şu yapıya benzer:

```text
C:\Users\muham\AppData\LocalLow\RaFoRa\HTC_JointROM_Alt_Ekstremite.apk\Replays\replay_20260509_193000\
```

Android/VIVE standalone build için aynı klasör uygulamanın persistent data alanındadır. Dosyayı PC'ye aldıktan sonra Unity'de `ReplayPlaybackController` ile açılabilir.

## Replay Paketinin İçeriği

Her replay klasörü şu dosyaları içerir:

```text
manifest.json
events.jsonl
frames.jsonl
```

`manifest.json`:
- schema version
- kayıt ID ve tarih
- scene adı
- Unity/app version
- session/protokol adı
- kalibrasyon durumu
- tracker atamaları
- görev listesi
- her görevin Türkçe adı ve yönergesi
- eşik değerleri ve hedefler
- frame/event sayısı
- varsa session report JSON yolu

`events.jsonl`:
- kayıt başladı/bitti
- session başladı/bitti
- countdown tick
- görev başladı/bitti
- result hazır
- dinlenme tickleri
- manuel marker

`frames.jsonl`:
- timestamp
- phase: `Countdown`, `Measurement`, `Rest`, `SessionRunning`
- görev index/type/adı
- görev içinde geçen süre
- kalibrasyon/tracking/IK durumu
- biometrics snapshot
- HMD/kontrolcü/tracker kaynak pozları
- IK target pozları
- final avatar kemik pozları
- kayıtlı ek scene object pozları

## Senaryo ve Hedef Hareket Bilgisi

Replay dosyası yalnızca hareketi değil, kullanıcının o anda ne yapmaya çalıştığını da saklar. Bu bilgi `TaskDefinition` ve `SessionConfigSO` üzerinden kaydedilir:

- `taskType`
- `taskNameTR`
- `instructionsTR`
- `durationSeconds`
- `countdownSeconds`
- `restAfterSeconds`
- valgus/asimetri/fleksiyon/sway/reach eşikleri
- demo sequence adı

Bu sayede replay oynarken örneğin kullanıcının mini squat mı, single-leg balance mı, yoksa Y-Balance reach mi yapmaya çalıştığı doğrudan görülebilir.

## Unity'de Tekrar Oynatma

1. Replay klasörünü PC'ye alın.
2. Unity'de bir replay scene veya debug scene oluşturun.
3. Sahneye `ReplayPlaybackController` ekleyin.
4. Replay avatarına `ReplayAvatarDriver` ekleyin ve `ReplayPlaybackController.avatarDriver` alanına bağlayın.
5. `ReplayPlaybackController.replayFolderPath` alanına replay klasörünü yazın.
6. `loadOnStart` ve `playOnLoad` açıksa sahne başlar başlamaz replay oynar.

Varsayılan olarak `ReplayPlaybackController`, `Application.persistentDataPath/Replays/` altındaki en yeni replay klasörünü de yükleyebilir. Bu davranış `loadNewestFromPersistentDataPath` ile kontrol edilir.

UI label alanları boş bırakılırsa `ReplayPlaybackController` runtime'da basit bir fallback UI üretir. Bu fallback UI görev adını, yönergeyi, zamanı, fazı ve temel metrikleri gösterir. Yani ilk replay denemesi için ayrıca TMP label bağlamak zorunlu değildir.

Klavye kontrolleri:

```text
Space: oynat / duraklat
Sol ok / Sağ ok: 5 saniye geri / ileri
1: 0.5x hız
2: 1x hız
3: 2x hız
```

## Kayıt Analizi Aracı

Kopyalanan headset verileri için varsayılan klasör:

```text
Assets/KayitSonuclari/
```

Unity Editor menüsü:

```text
Tools/Gamification/Kayit Analizi
```

Bu araç klasördeki şu dosyaları otomatik tarar:

- `session_*.json`
- `session_*.csv`
- `replay_*/manifest.json`
- `replay_*/events.jsonl`
- `replay_*/frames.jsonl`
- `lower_limb_diagnostics_*.csv`

Replay ve report dosyaları önce `manifest.linkedSessionReportJson` ile eşleştirilir. Eşleşme yoksa dosya adındaki tarih/saat bilgisi kullanılır. Replay başlangıç zamanı ile session report bitiş zamanı farklı olabileceği için birebir timestamp beklenmez.

Analiz penceresinde görülebilenler:

- hangi oyun/protokol ne zaman oynandı
- replay/report/diagnostics dosyası var mı
- toplam görev sayısı, süre, frame/event sayısı
- görev bazlı skor, risk grade ve kısa yorum
- valgus, fleksiyon, salınım, simetri ve reach metrikleri
- `events.jsonl` zaman çizelgesi
- `frames.jsonl` üzerinden frame/metrik aralık analizi
- diagnostics CSV üzerinden kalibrasyon, IK, tracking ve mapping sürekliliği

`Analiz Raporu Export` butonu seçili kayıt için şu klasöre okunabilir rapor üretir:

```text
Assets/KayitSonuclari/AnalizRaporlari/
```

Çıktılar:

- `*_analiz.md`
- `*_gorevler.csv`

`Tumunu Export` butonu taranan bütün kayıtlar için aynı klasöre rapor üretir.

## Replay Sahnesini Otomatik Hazırlama

Unity Editor menüsü:

```text
Tools/Gamification/Replay Sahnesi Hazirla
```

Bu araç seçilen replay için sahnede şu işlemleri yapar:

- frame verisini kontrol eder
- sahnede humanoid `Animator` arar; yoksa `SkinnedMeshRenderer.rootBone` üzerinden kemik hiyerarşisini bulur
- avatara veya kemik/mesh ortak köküne `ReplayAvatarDriver` ekler veya mevcut olanı kullanır
- `[ReplayReview]` objesi oluşturur
- bu objeye `ReplayPlaybackController` ekler veya mevcut olanı kullanır
- `replayFolderPath` alanını seçilen `replay_...` klasörüne ayarlar
- `loadOnStart` ve `playOnLoad` ayarlarını açar

`Sahneyi Hazirla ve Oynat` butonu frame verisi varsa Play Mode'a geçip replay'i otomatik başlatır. Sahnede `Animator` yok ama `Ch36` gibi `SkinnedMeshRenderer` olan mesh ve `mixamorig...` kemikleri varsa `Avatar Root / Mesh / Bone` alanına mesh objesini, `HumanModel` objesini veya `mixamorig...:Hips` kök kemiğini verebilirsin. Araç ortak kökü bulup kemik isimlerinden replay uygular.

Replay yüklenirken aynı sahnedeki canlı tracking/IK sürücüleri runtime'da kapatılır ve replay kemik pozları `LateUpdate` sonunda uygulanır. Bu, `FullBodyIKSolver` gibi canlı sistemlerin replay pozlarını geri ezmesini engeller. Overlay'de `Bones: applied/missing` satırı görünür; normal durumda yaklaşık `20 applied, 0 missing` beklenir.

## Görev Sonucu Gösterimi

Final kararda her görevden sonra ayrı kısa sonuç paneli gösterilmez. Sonuçlar yine de kaybolmaz:

- `TaskEvaluator` her görev sonunda `TaskResult` üretir.
- `SessionReportWriter` session sonunda CSV/JSON rapor yazar.
- `ReplayRecorder`, `events.jsonl` içine `result_ready` event'i olarak görev sonucunu ve kısa yorumu kaydeder.
- Oyun içinde sonuçlar final summary panelinde görünür.

## Neden Video Değil Veri Kaydı?

- Dosya boyutu daha küçüktür.
- Avatar hareketi Unity içinde tekrar üretilebilir.
- Görev yönergeleri, metrikler ve sonuçlar zaman çizgisine bağlanır.
- Kayıt headset olmadan Editor'da incelenebilir.
- Ham tracker/HMD/kontrolcü pozları analiz için saklanır.
- Final avatar kemikleri saklandığı için gelecekte IK sistemi değişse bile görsel replay bozulmaz.

## Performans Ayarları

Varsayılan kayıt ayarları:

```text
sampleRateHz: 30
recordSourceDevices: true
recordIkTargets: true
recordAvatarBones: true
recordMetrics: true
recordCountdownAndRestFrames: true
flushInterval: 60
```

Aktif hardware sahnesinde final ayarlar:

```text
GameFlowController.simulatedMode: false
GameFlowController.autoStartAfterCalibration: false
ReplayRecorder.autoRecordSession: true
ReplayRecorder.outputFolder: Replays
LowerLimbDiagnosticsOverlay.showPanelOnStart: false
LowerLimbDiagnosticsOverlay.autoRecordAfterCalibration: false
LowerLimbDiagnosticsOverlay.enableControllerShortcuts: false
```

30 Hz replay için yeterlidir ve 90 Hz XR frame kaydına göre dosyayı daha küçük tutar. Eğer tam performans kaygısı oluşursa ilk düşürülecek ayarlar şunlardır:

1. `recordIkTargets = false`
2. `recordSourceDevices = false`
3. `sampleRateHz = 20`
4. JSONL yerine binary/compressed frame writer

## Doğrulama Protokolü

1. Build alın ve headset'te kalibrasyonu tamamlayın.
2. Kısa bir session başlatın.
3. En az iki görev yapın: örneğin Standing ve MiniSquat.
4. Session bitince replay klasörünün oluştuğunu kontrol edin.
5. Klasörü PC'ye alın.
6. Unity'de `ReplayPlaybackController` ile açın.
7. Şunları kontrol edin:
   - avatar hareketi doğru zamanda oynuyor mu?
   - görev adı ve yönerge doğru görünüyor mu?
   - countdown/measurement/rest fazları doğru mu?
   - `manifest.json` içinde kalibrasyon ve tracker bilgileri var mı?
   - frame sayısı yaklaşık `kayıt süresi x 30` değerine yakın mı?
   - result eventleri session report JSON ile tutarlı mı?

## Mevcut Sınırlamalar

- İlk sürüm video kaydetmez.
- Fizik objeleri otomatik deterministik rollback yapmaz; kaydedilmesi gereken hareketli objelere `ReplayRecordable` eklenmelidir.
- Manuel marker için kayıt sırasında klavye `F12` yolu vardır; controller marker için daha sonra Unity Input Actions tabanlı ayrı mapping önerilir.
- Replay avatarının kemik eşleşmesi `Humanoid Animator` üzerinden veya bone isimleri üzerinden yapılır. Farklı avatar kullanılırsa `ReplayAvatarDriver` eşleşmesi kontrol edilmelidir.
