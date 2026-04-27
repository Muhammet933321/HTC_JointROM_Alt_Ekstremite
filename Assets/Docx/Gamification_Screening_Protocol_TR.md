# Gamification Screening Protocol (TR)

Bu not, projeye eklenen yeni tarama sistemlerinin neyi ölçtüğünü, kullanıcıya hangi hareketin yaptırılacağını ve sonucun nasıl yorumlanacağını özetler.

## 1. İniş Tarama

- Görev adı: `LandingScreen`
- Literatür hattı: jump-landing / LESS temelli saha taramaları, Hewett 2005, Read 2019, Hanzlikova 2021
- Kullanıcı hareketi:
  - Ayaklar omuz genişliğinde açık olur.
  - Kullanıcı yerinde iki ayakla 3 kontrollü küçük sıçrama yapar.
  - Her inişte dizlerini bükerek yumuşak ve sessiz inmeye çalışır.
- Sistem neye bakar:
  - Pik diz valgusu
  - İnişte ulaşılan diz fleksiyonu
  - İniş sonrası denge toparlanması
- Yorum:
  - Yüksek valgus: inişte dizin içe kaçtığını gösterir.
  - Düşük fleksiyon: sert iniş ve zayıf yük kabulü paternini gösterir.
  - Yüksek sway / sway velocity: inişten sonra stabilizasyonun zayıf olduğunu gösterir.
  - Valgus + düşük fleksiyon birlikte yüksekse, en kuvvetli uyarı budur.

## 2. Modifiye Y-Balance Ön Uzanma

- Görev adları:
  - `ModifiedYBalanceAnterior_R`
  - `ModifiedYBalanceAnterior_L`
- Literatür hattı: Y-Balance / mYBT, Shaffer 2013, O'Connor 2020, Bennett 2022
- Kullanıcı hareketi:
  - Sağ testte sağ ayak yerde sabit kalır, sol ayak öne uzatılır.
  - Sol testte sol ayak yerde sabit kalır, sağ ayak öne uzatılır.
  - Uzanan ayak önde en uzak kontrollü noktaya kadar gider, zemine veya hedef noktaya sadece hafifçe temas eder ve o ayağa yük vermeden başlangıç pozisyonuna geri döner; 3 kontrollü tekrar yapılır.
- Sistem neye bakar:
  - Ön uzanma mesafesi (% yaklaşık bacak uzunluğu)
  - Stance bacağındaki valgus kontrolü
  - Reach sırasında denge salınımı
- Yorum:
  - Kısa reach: dinamik denge / mobilite / güvenli erişim kapasitesi düşük olabilir.
  - Reach sırasında stance dizinde içe kaçış: frontal düzlem kontrolü zayıf olabilir.
  - Reach sırasında yüksek sway: postüral kontrol yetersiz olabilir.
- Not:
  - Bu test tek başına tanı koyucu değildir.
  - En iyi kullanım şekli, diğer görevlerle birlikte destekleyici tarama testidir.
  - Buradaki amaç bir adım atmak değil, stance ayağı yerde sabit tutarken serbest ayağın erişebildiği mesafeyi göstermektir.

## 3. Tek Ayak Squat Tarama

- Görev adları:
  - `SingleLegSquat_R`
  - `SingleLegSquat_L`
- Literatür hattı: dynamic knee valgus ve tek bacak kontrolü, Burnham 2026, Khou 2024
- Kullanıcı hareketi:
  - Sağ testte kullanıcı sağ ayakta dengede kalır, sol ayağı yerden kaldırır.
  - Sağ dizini kontrollü şekilde bükerek sığ bir tek ayak squat yapar ve geri kalkar.
  - Sol test aynı mantıkla karşı taraf için uygulanır.
- Sistem neye bakar:
  - Stance tarafındaki diz valgusu
  - Kontrollü squat derinliği için diz fleksiyonu
  - Tek taraflı denge kontrolü
- Yorum:
  - Yüksek valgus: yük altında dizin içe kaçtığını gösterir.
  - Düşük fleksiyon: güvenli yük kabulü / squat stratejisi kısıtlı olabilir.
  - Yüksek sway: tek taraflı postüral kontrol zayıf olabilir.

  ## 4. Tum Task'lar Icin Hareket Haritasi

  Bu bolum demo sequence duzeltirken hangi task'in hangi hareket oldugunu hizli gorebilmek icin eklendi.

  - `Standing`
    - Hareket: Nötr dik duruş.
    - Demo mantığı: Ayaklar yerde, gövde dik, ekstra eğilme veya squat yok.
    - Sequence önerisi: neutral -> kısa bekleme.

  - `LeanRight`
    - Hareket: Gövdeyi sağa doğru yana eğme veya ağırlığı sağa kaydırma.
    - Demo mantığı: Ayaklar çift destekte kalır, squat derinliği olmaz.
    - Sequence önerisi: neutral -> sağa eğilme -> neutral.

  - `LeanLeft`
    - Hareket: Gövdeyi sola doğru yana eğme veya ağırlığı sola kaydırma.
    - Demo mantığı: Ayaklar çift destekte kalır, squat derinliği olmaz.
    - Sequence önerisi: neutral -> sola eğilme -> neutral.

  - `LeanForward`
    - Hareket: Gövdeyi öne doğru kontrollü eğme.
    - Demo mantığı: Bu hareket küçük bir hip hinge gibi görünmeli; derin squat değil.
    - Sequence önerisi: neutral -> öne eğilme -> neutral.

  - `SingleLegBalance_R`
    - Hareket: Sağ ayak yerde sabit, sol ayak yerden hafif kalkık tek ayak denge.
    - Demo mantığı: Sağ taraf stance tarafıdır. Gövde olabildiğince dik kalır.
    - Sequence önerisi: neutral -> sağ tek ayak denge -> neutral.

  - `SingleLegBalance_L`
    - Hareket: Sol ayak yerde sabit, sağ ayak yerden hafif kalkık tek ayak denge.
    - Demo mantığı: Sol taraf stance tarafıdır. Gövde olabildiğince dik kalır.
    - Sequence önerisi: neutral -> sol tek ayak denge -> neutral.

  - `MiniSquat`
    - Hareket: Çift ayakla sığ squat.
    - Demo mantığı: Kalça hafif geriye gider, dizler kontrollü bükülür, ama hareket derin squat kadar aşağı inmez.
    - Sequence önerisi: neutral -> sığ squat -> neutral.

  - `WalkSimulation`
    - Hareket: Yerinde yürüme / sıra ile adım alma simülasyonu.
    - Demo mantığı: Bu gerçek yürüyüş analizi değil; sequence içinde sağ-sol adım alternasyonu görünmesi yeterlidir.
    - Sequence önerisi: neutral -> sağ adım fazı -> neutral -> sol adım fazı -> neutral.

  - `LandingScreen`
    - Hareket: Çift ayak kontrollü iniş.
    - Demo mantığı: Asıl gösterilmek istenen şey yumuşak iniş pozudur. İstersen küçük bir hazırlık veya çok küçük sıçrama öncesi pozu koyabilirsin, ama kritik frame inişte diz fleksiyonu olan çift ayak kabul pozudur.
    - Sequence önerisi: neutral -> iniş kabul pozu -> daha derin stabilizasyon pozu -> neutral.

  - `ModifiedYBalanceAnterior_R`
    - Hareket: Sağ ayak stance, sol ayak öne uzanır.
    - Demo mantığı: Sağ diz hafif kontrollü bükülü kalır, sol bacak öne reach yapar. Gövde hafif öne gidebilir ama hareket tek ayak squat'a dönüşmemeli.
    - Sequence önerisi: neutral -> sağ stance ile öne reach -> neutral.

  - `ModifiedYBalanceAnterior_L`
    - Hareket: Sol ayak stance, sağ ayak öne uzanır.
    - Demo mantığı: Sol diz hafif kontrollü bükülü kalır, sağ bacak öne reach yapar.
    - Sequence önerisi: neutral -> sol stance ile öne reach -> neutral.

  - `SingleLegSquat_R`
    - Hareket: Sağ ayak üzerinde tek ayak squat.
    - Demo mantığı: Sağ taraf stance tarafıdır. Sol ayak yerden kalkık kalır. Bu, Y-Balance reach değil; aşağı doğru kontrollü tek taraflı squat hareketidir.
    - Sequence önerisi: neutral -> sağ tek ayak squat dip pozu -> neutral.

  - `SingleLegSquat_L`
    - Hareket: Sol ayak üzerinde tek ayak squat.
    - Demo mantığı: Sol taraf stance tarafıdır. Sağ ayak yerden kalkık kalır. Bu da reach değil, aşağı kontrollü tek ayak squat hareketidir.
    - Sequence önerisi: neutral -> sol tek ayak squat dip pozu -> neutral.

  ## 5. Sequence Duzenlerken Karismamasi Gerekenler

  - `ModifiedYBalanceAnterior_R/L` ile `SingleLegSquat_R/L` birbirine benzemesin.
  - Y-Balance tarafinda hareketin ana fikri: bir ayak sabit, diger ayak one uzaniyor.
  - Single-leg squat tarafinda hareketin ana fikri: stance bacak yuk altinda asagi iniyor, bos bacak sadece havada dengede kaliyor.
  - `LandingScreen`, `MiniSquat` ile ayni gorunmemeli.
  - LandingScreen'de daha belirgin bir yuk kabul ve inis hissi olmali.
  - `LeanRight`, `LeanLeft`, `LeanForward` squat gibi gorunmemeli; esas degisim govde yonelimi olmali.

  ## Kullanım Mantığı

- Bu ekranlar tanı koymak için değil, risk paterni taramak için kullanılır.
- En savunulabilir ifade:
  - "Kullanıcıda yüksek dinamik diz valgusu paterni izlendi."
  - "Kullanıcıda yük kabulü sırasında sert iniş stratejisi izlendi."
  - "Kullanıcıda dinamik denge / anterior reach kapasitesi beklenenin altında bulundu."
- Klinik olarak daha riskli kombinasyonlar:
  - yüksek valgus + düşük fleksiyon
  - kısa reach + yüksek sway
  - tek ayak görevinde belirgin stance-side kontrol kaybı

## Projede Ne Değişti

- Yeni task type'lar eklendi.
- `LowerLimbBiometrics` artık anterior reach yüzdesi üretir.
- `TaskEvaluator` yeni reach metriklerini toplar.
- `TaskResult` yeni görevler için task-spesifik risk ağırlıkları ve Türkçe yorum üretir.
- `GamificationSetup` artık yeni task asset'lerini de oluşturur.
- UI ve rapor katmanı yeni metrikleri ve özet yorumları gösterir.