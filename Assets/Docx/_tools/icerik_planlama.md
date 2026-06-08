# Poster & Sunum İçerik Planlaması

Kaynak: `Tez/BitirmeTezi_Taslak.tex` + Hafta_01..05 raporları.

## Proje Künyesi
- **Proje Adı:** HTC VIVE Ultimate Tracker ve XR Tabanlı Oyunlaştırılmış Alt Ekstremite Hareket Analizi Sistemi
- **Proje Yürütücüsü:** Muhammet Çiğdem (227231041)
- **Proje Danışmanı:** Samet Tonyalı
- **Kurum:** Gümüşhane Üniversitesi – Mühendislik ve Doğa Bilimleri Fakültesi – Yazılım Mühendisliği Bölümü
- **Tarih:** Mayıs 2026

## Anahtar Kelimeler
Sanal Gerçeklik, Hareket Açıklığı (ROM), Alt Ekstremite, HTC VIVE Ultimate Tracker, OpenXR, Oyunlaştırma, Hareket Tarama.

---

## POSTER PANELLERİ (A4 dikey – 8 kutu)

### TextBox 97 – Proje Adı / Numarası
HTC VIVE Ultimate Tracker ve XR Tabanlı Oyunlaştırılmış Alt Ekstremite Hareket Analizi Sistemi

### TextBox 98 – Yürütücü / Danışman / Ekip
- **Yürütücü:** Muhammet Çiğdem
- **Danışman:** Samet Tonyalı
- **Ekip:** Gümüşhane Üniversitesi Yazılım Mühendisliği

### TextBox 99 – Özet
Bu çalışmada HTC VIVE Ultimate Tracker ve OpenXR verisini kullanan Unity tabanlı bir XR hareket analizi sistemi geliştirilmiştir. Sistem; kalça ve diz kinematiğini gerçek zamanlı hesaplamakta, tam vücut avatar görselleştirmesi sağlamakta ve oyunlaştırılmış görevler aracılığıyla hareket paternlerini izlemektedir. Kontrollü iniş, mini squat, tek ayak denge, tek ayak squat ve modifiye anterior reach görevlerinde valgus eğilimi, fleksiyon, reach yüzdesi ve salınım temelli metrikler üretilmektedir. Yapı tanı koyan bir sistemden çok destekleyici bir hareket tarama prototipi olarak konumlandırılmıştır.

### TextBox 100 – Projenin Materyali ve Yöntemi
- **Donanım:** HTC VIVE Ultimate Tracker (pelvis, femur, tibia), XR başlık ve kontrolcü
- **Yazılım:** Unity 6, OpenXR + VIVE eklentileri
- **Algoritma:** FK ve IK çözücüler, kalibrasyon bazlı sensör-kemik ofseti $Q_{offset}=Q_T^{-1}\cdot Q_B$
- **Mimari:** OpenXR Cihaz → Toplama/Eşleme → Kinematik Çözüm → Uygulama (görev motoru + UI)
- Kalça ve diz lokal rotasyonları hiyerarşik FK ile, çalışma anı yönelimleri $Q_B^t=Q_T^t\cdot Q_{offset}$ ile çıkarılmaktadır.

### TextBox 101 – Amaç ve Hedefler
**Amaç:** Alt ekstremite hareket analizini XR ortamına taşıyarak sensör verisi, avatar görselleştirme, görev akışı ve kullanıcı geri bildirimini tek mimaride birleştirmek.

**Hedefler:**
- Gerçek zamanlı kalça/diz kinematiği üretimi
- Tam vücut avatar ile görsel geri bildirim
- Görev tabanlı tarama akışı (LESS ve Y-Balance kavramsal referans)
- Türkçe yorum ve risk alanı çıktıları
- Modüler ve sürdürülebilir yazılım yapısı

**Beklenen başarı ölçütleri:** Düşük gecikmeli açı akışı, kararlı IK temsili, görev başına yorumlanabilir metrik üretimi.

### TextBox 104 – Şekil, Grafik, Tablo (Görev × Metrik Özeti)
| Görev | Birincil Metrik | Yorumlanan Patern |
|---|---|---|
| Kontrollü iniş | Pik diz valgusu, fleksiyon, sway | Yük kabulü, iniş kalitesi |
| Mini squat | Diz fleksiyonu, valgus eğilimi | Çift ayak yüklenme |
| Tek ayak squat | Valgus, fleksiyon, sway | Tek taraflı kontrol |
| Modifiye ant. reach | Reach %, stance valgusu, sway | Dinamik denge |
| Tek ayak denge | Pelvis sway RMS, hız | Denge kontrolü |
| Gövde eğimi | Trunk yönelimi | Proksimal kontrol |

Reach % = $d_{reach}/L_{limb}\times100$; Sway$_{RMS}=\sqrt{\frac{1}{N}\sum(x_i-\bar{x})^2}$.

### TextBox 102 – Sonuç ve Öneriler
Sistem, tracker verisinden gerçek zamanlı alt ekstremite kinematiği üretebilmekte, eşzamanlı avatar sürüşü ve görev tabanlı metrik üretimi sağlamaktadır. Hint kararlılığı, bind-pose sıfırlaması, uzuv oranı uyumu ve shin-tracker temsili gibi pratik IK problemlerine yönelik iyileştirmeler belgelenmiştir. Mevcut çıktı tanı amaçlı değildir; klinik geçerlilik için referans sistemle nicel doğrulama, görev eşiklerinin kullanıcı grubuna kalibrasyonu ve uzman görüşü ile desteklenmiş örnek veri toplanması önerilmektedir.

### TextBox 103 – Kaynaklar (kısaltılmış)
1. Norkin & White. *Measurement of Joint Motion*, F.A. Davis, 2016.
2. Niehorster ve ark. HTC Vive tracking accuracy. *i-Perception*, 8(3), 2017.
3. HTC. VIVE Ultimate Tracker Specs, 2024.
4. Renström ve ark. Non-contact ACL injuries. *Br. J. Sports Med.*, 42(6), 2008.
5. Hanzlíková ve ark. LESS systematic review. *J. Sci. Med. Sport*, 24(3), 2021.
6. Kenwright. CCD inverse kinematics. *J. Graphics Tools*, 16(1), 2012.

---

## SUNUM SLAYTLARI (6 slayt, widescreen)

### Slayt 1 – Başlık
- **Başlık:** HTC VIVE Ultimate Tracker ve XR Tabanlı Oyunlaştırılmış Alt Ekstremite Hareket Analizi Sistemi
- **Alt başlık:** Lisans Bitirme Projesi
- Muhammet Çiğdem (227231041) – Danışman: Samet Tonyalı
- Gümüşhane Üniversitesi · Yazılım Mühendisliği · Mayıs 2026

### Slayt 2 – Problem & Motivasyon
- Diz valgusu, tek ayak dengesizliği ve yük kabulü gibi dinamik paternler statik açı ölçümleriyle açıklanamaz.
- Lab tipi mocap sistemleri pahalı ve taşınması zor.
- Geleneksel gonyometri değerlendirici bağımlıdır.
- **Hedef:** Erişilebilir XR altyapısı + görev tabanlı akış + tanı iddiası taşımayan destekleyici çıktılar.

### Slayt 3 – Sistem Mimarisi & Yöntem
- **Donanım:** HTC VIVE Ultimate Tracker (pelvis + 2× alt ekstremite), XR başlık & kontrolcü
- **Yazılım:** Unity 6, OpenXR, VIVE eklentileri
- **4 Katmanlı Veri Akışı:**
  - OpenXR Cihaz Katmanı (HMD, tracker, kontrolcü)
  - Toplama ve Eşleme Katmanı (cihaz eşleme, kalibrasyon)
  - Kinematik Çözüm Katmanı (FK + tam vücut IK)
  - Uygulama Katmanı (klinik açı göstergeleri, görev motoru, UI)
- Kalibrasyon: $Q_{offset}=Q_T^{-1}\cdot Q_B$ → Çalışma anı: $Q_B^t=Q_T^t\cdot Q_{offset}$

### Slayt 4 – Görevler & Üretilen Metrikler
- **Kontrollü iniş** → pik diz valgusu, fleksiyon, sway
- **Mini squat & Tek ayak squat** → fleksiyon, valgus eğilimi
- **Modifiye anterior reach** → reach %, stance valgusu
- **Tek ayak denge** → pelvis sway RMS / hızı
- **Gövde eğimi & yerinde yürüme** → proksimal kontrol, akış gözlemi
- Sonuçlar Türkçe yorumlar + risk alanı bildirimi olarak sunulur.

### Slayt 5 – Bulgular & Teknik Katkılar
- Gerçek zamanlı kalça-diz kinematiği üretimi ✓
- Eşzamanlı tam vücut avatar sürüşü ✓
- Görev tabanlı, yorumlanabilir metrik akışı ✓
- **Teknik iyileştirmeler:**
  - Hint yönü düzeltmesi (anatomik bükülme düzlemi)
  - Shin-tracker modu (alt bacak temsili)
  - Bind-pose sıfırlaması (drift azaltma)
  - Çoklu tracker ile doğrudan FK + CCD tabanlı omurga
  - Otomatik kemik bulma & test sürücüleri (geliştirme süresi ↓)

### Slayt 6 – Sınırlılıklar, Gelecek Çalışmalar & Sonuç
- **Sınırlılıklar:** Referans sistemle nicel doğrulama yok; görevler literatürdekilerle birebir değil; trunk-lean ve yürüme ölçütleri henüz ayrıntısız.
- **Gelecek çalışmalar:**
  - Referans sistemle ROM doğrulaması ve tekrarlanabilirlik analizi
  - Yürüyüş ve trunk metriklerinin eklenmesi
  - Çoklu tracker rol seçimi
  - Terapist paneli ve raporlama modülü
- **Sonuç:** Sistem; ROM ölçümü, full-body avatar sürüşü ve oyunlaştırılmış görev akışını tek mimaride birleştirerek savunulabilir bir lisans bitirme prototipi sunmaktadır.

**Teşekkürler.**
