# Gamification Scoring Evidence Matrix (TR)

LastUpdated: 2026-05-09
Scope: Alt ekstremite gamification görevlerinin puanlama, geri bildirim ve literatür uyumu
ClaimLevel: Adolesan / genç sporcularda alt ekstremite hareket-risk paterni taraması

## Kullanım Cümlesi

Bu sistem bir tanı veya tek başına yaralanma tahmin motoru olarak değil, adolesan / genç sporcularda alt ekstremite hareket-risk paternlerini çoklu görev ve çoklu metrik üzerinden tarayan bir XR geri bildirim sistemi olarak konumlandırılmalıdır.

## Çıktı Mantığı

Puanlama ters risk mantığıyla çalışır:

- `GameScore = (1 - TotalRiskScore) * 100`
- Risk skorları 0 ile 1 arasındadır.
- Düşük risk skoru yüksek oyun puanı üretir.
- Yüksek risk skoru düşük oyun puanı üretir.
- UI etiketleri "risk tanısı" gibi değil, hareket paterni uyarısı olarak yorumlanmalıdır.

Ana kod referansları:

- `Assets/Scripts/New System/Gamification/TaskResult.cs`: risk formülleri, görev ağırlıkları, puan ve Türkçe özet.
- `Assets/Scripts/New System/Gamification/LowerLimbBiometrics.cs`: tracker pozisyonlarından ham metrik üretimi.
- `Assets/Scripts/New System/Gamification/TaskEvaluator.cs`: görev süresince frame-by-frame örnekleme ve sonuç üretimi.
- `Assets/Scripts/New System/Gamification/GameUIController.cs`: oturum sonu görüntülenen uyarı etiketleri.
- `Assets/Scripts/New System/Gamification/SessionReportWriter.cs`: CSV/JSON rapor çıktısı.

## Metrik Bazlı Kanıt ve Yorum Matrisi

| Geri bildirim alanı | Ham veri | Yüksek puan ne demek? | Düşük puan ne demek? | Kod formülü / eşik | Literatür dayanağı | Güvenli yorum | Sınır |
|---|---|---|---|---|---|---|---|
| Valgus uyarısı | Sol/sağ ortalama valgus, sol/sağ pik valgus, sağ-sol pik valgus farkı | Diz içe kaçışı düşük, sağ-sol kontrol benzer | Dinamik diz valgusu veya iki taraf arasında belirgin fark | Ortalama valgus 5 derece altı güvenli kabul edilir; pik valgus 8 derece sonrası risk artar; 18 derece civarında tam risk. Bilateral fark 8 dereceye normalize edilir. | Hewett 2005, Numata 2017, Tamura 2017, Saki 2024 | "Kullanıcıda diz içe kaçış paterni izlendi / izlenmedi." | KAM ölçülmez; valgus bir kinematik proxy'dir. |
| Fleksiyon uyarısı | Sol/sağ diz fleksiyon maksimumları | Görev için beklenen diz bükülmesine ulaşıldı | Sert iniş, sınırlı yük kabulü veya yetersiz squat derinliği | Landing yaklaşık 45 derece, MiniSquat 60 derece, SingleLegSquat 40 derece, SingleLegBalance/Y-Balance 20 derece hedeflenir. | Hewett 2005, Read 2019, landing mechanics literatürü | "Yük kabulü sırasında yeterli/yetersiz fleksiyon stratejisi izlendi." | Tam LESS skoru değildir; kalça fleksiyonu ayrı puanlanmaz. |
| Denge uyarısı | Pelvis sway RMS ve pelvis XZ hız ortalaması | Salınım düşük, stabilizasyon iyi | Salınım yüksek veya toparlanma yavaş | Sway velocity 20 mm/s altı iyi; 50 mm/s civarı tam risk. RMS görev eşiğine normalize edilir; çoğu görev 15 mm, landing/Y-Balance/single-leg squat 20 mm. | Maki 1990, Kaptein 2006 | "Postüral kontrol / stabilizasyon uyarısı var." | COP veya force plate yok; pelvis sway proxy'dir. |
| Asimetri uyarısı | Sağ-sol diz fleksiyon simetri indeksi | Sağ-sol hareket amplitüdü benzer | Bilateral görevlerde taraflar arasında belirgin hareket farkı | SI = 100 * abs(R-L) / mean(R,L). 10% üstü risk artar, 25% civarı tam risk. | Saki 2024 ve bilateral asimetri literatürü | "Sağ-sol hareket asimetrisi belirgin." | Tek ayak ve reach görevlerinde bu alan sıfırlanır; taraf karşılaştırması aynı görev içinde yapılmaz. |
| Erişim uyarısı | Modified Y-Balance anterior reach yüzdesi | Stance ayak sabitken iyi anterior reach kapasitesi | Kısa reach, dinamik denge/mobilite sınırlılığı olasılığı | TargetReachPct varsayılan 65%; hedefin 15 puan altı tam risk. | Shaffer 2013, O'Connor 2020, Bennett 2022; ayrıca PMID 32362482 ve 34801389 uyarıları | "Anterior reach kapasitesi destekleyici olarak düşük/yeterli." | Tek başına injury predictor değildir; full Y-Balance geçerliliği iddia edilmemeli. |

## Görev Bazlı Yorum Matrisi

| Görev | Ana ağırlık | Güçlü puan paterni | Zayıf puan paterni | Kanıt gücü | Güvenli tez ifadesi | Kaçınılması gereken ifade |
|---|---|---|---|---|---|---|
| Standing | Denge %50, valgus/asimetri destekleyici | Nötr duruşta düşük sway ve simetrik duruş | Statik duruşta yüksek sway veya asimetri | Orta-düşük | Statik postüral kontrol bağlamında destekleyici tarama. | Tek başına yaralanma riski tahmini. |
| LeanRight / LeanLeft / LeanForward | Denge %35, valgus %30, asimetri %25 | Kontrollü ağırlık aktarımı, düşük salınım | Lean sırasında denge kaybı veya valgus artışı | Kavramsal orta, uygulama düşük-orta | Trunk/proksimal kontrol fikrini destekleyen görev. | Klasik trunk displacement çalışmasını birebir yeniden üretir. |
| SingleLegBalance_R/L | Denge %55, valgus %30 | Tek ayakta düşük sway ve stance diz kontrolü | Tek ayakta yüksek sway veya stance diz içe kaçışı | Orta | Tek taraflı postüral kontrol ve stance diz kontrolü taranır. | Klinik denge testi eşdeğeri veya tanı. |
| MiniSquat | Valgus %40, fleksiyon %25, denge %20 | Kontrollü çift ayak squat, yeterli fleksiyon, düşük valgus | Diz içe kaçışı, düşük fleksiyon veya salınım | Orta | Çift ayak yük kabulü ve dinamik valgus paterni taranır. | Yaralanma riskini tek başına kanıtlar. |
| WalkSimulation | Valgus %35, asimetri %25, denge %30 | Yerinde adımda belirgin kontrol kaybı olmaması | Adım sırasında sway/asimetri/valgus artışı | Zayıf | Keşifsel hareket gözlemi. | Kanıta dayalı gait analizi. |
| LandingScreen | Valgus %40, fleksiyon %35, denge %20 | Yumuşak iniş, yeterli diz fleksiyonu, düşük valgus | Yüksek valgus + düşük fleksiyon en güçlü uyarı | En güçlü pratik dayanak | Landing mechanics ve yük kabul paterni taranır. | Tam LESS skoru veya ACL tanısı. |
| ModifiedYBalanceAnterior_R/L | Reach %40, denge %30, valgus %20 | İyi reach, stance diz kontrolü, düşük sway | Kısa reach, stance valgus veya yüksek sway | Destekleyici | Anterior reach/dinamik denge destekleyici bilgi sağlar. | Anterior reach tek başına injury predictor. |
| SingleLegSquat_R/L | Valgus %40, fleksiyon %35, denge %25 | Stance diz kontrolü ve yeterli tek ayak squat derinliği | Stance valgus, düşük squat derinliği veya sway | Mekanistik orta | Tek ayak yük kabulü ve dinamik valgus paterni izlenir. | Tek başına ACL yaralanmasını öngörür. |

## Literatür Uyum Kararı

Mevcut sistem literatürle en güçlü şekilde şu noktada uyumludur:

- landing mechanics,
- dinamik diz valgusu,
- yük kabulü / diz fleksiyonu,
- çoklu görev bataryası içinde tek ayak kontrolü,
- destekleyici anterior reach ve denge metrikleri.

Mevcut sistemin dikkatli ifade edilmesi gereken yönleri:

- KAM, EMG, force plate veya COP doğrudan ölçülmez.
- LandingScreen tam LESS değildir.
- Modified Y-Balance anterior reach tek başına yaralanma tahmini değildir.
- WalkSimulation gerçek gait analizi değildir.
- Lean görevleri trunk kontrol literatürüne kavramsal olarak bağlıdır, fakat mevcut kod doğrudan trunk angle/displacement üretmez.

## Pilot Doğrulama Protokolü

Build/hardware testinde en az aşağıdaki yön kontrolleri yapılmalıdır:

1. Kalibrasyon sonrası nötr dik duruş: düşük sway, düşük valgus, yüksek skor beklenir.
2. Bilinçli diz içe kaçışı: valgus uyarısının yükselmesi ve skorun düşmesi beklenir.
3. LandingScreen yumuşak iniş: yeterli fleksiyon ve düşük valgus ile yüksek skor beklenir.
4. LandingScreen sert/az fleksiyonlu iniş: fleksiyon uyarısı ve skor düşüşü beklenir.
5. SingleLegSquat kontrollü tekrar: stance diz uyarısı düşük, fleksiyon yeterli, skor yüksek beklenir.
6. SingleLegSquat bilinçli dengesiz tekrar: denge/valgus uyarısı ve skor düşüşü beklenir.
7. ModifiedYBalance uzun reach: reach uyarısı düşük beklenir.
8. ModifiedYBalance kısa reach veya stance dizi içe kaçırma: reach/valgus uyarısı ve skor düşüşü beklenir.

Her testten sonra `Application.persistentDataPath/Reports/` altındaki CSV/JSON raporunda ham değerlerin beklenen yönde değiştiği kontrol edilmelidir.

## Tezde Kullanılabilecek Kısa İfade

Geliştirilen sistem, VIVE tracker verilerinden elde edilen diz valgusu, diz fleksiyonu, pelvis salınımı, hareket asimetrisi ve anterior reach proxy metriklerini kullanarak, adolesan/genç sporcularda alt ekstremite hareket-risk paternlerini görev bazlı olarak puanlayan bir XR geri bildirim sistemi olarak tasarlanmıştır. Sistem tanı koymayı veya tek bir görev üzerinden yaralanma riskini kesin olarak tahmin etmeyi amaçlamaz; literatürde desteklenen hareket kalitesi göstergelerini çoklu görev bataryası içinde yorumlar.