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
  - Uzanan ayak hafif dokunup geri çekilir; 3 kontrollü tekrar yapılır.
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