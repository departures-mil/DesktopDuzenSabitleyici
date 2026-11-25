# Desktop Simge Sabitleyici

Masaüstü simgelerinin yerini korur. Laptop ekranı, büyük monitör, farklı çözünürlük ya da farklı ölçek (%100 / %125 / %150) arasında geçiş yaptığınızda simgeler artık karışmaz.

## Nasıl çalışır?

- Simgeleri istediğiniz gibi dizin. Uygulama düzeni kendiliğinden kaydeder.
- Ekran, çözünürlük veya ölçek değiştiğinde (monitör takma/çıkarma, uykudan uyanma, Explorer'ın yeniden başlaması) düzeni birkaç saniye içinde geri getirir.
- **Oransal mod (varsayılan):** Aynı düzen yeni ekrana orantılı olarak taşınır. Sol üstteki sol üstte, ortadaki ortada kalır.
- **Ekran başına profil:** Her ekran kendi düzenini hatırlar. İlk kez görülen bir ekranda oransal düzen kullanılır.
- Arka planda, sistem tepsisinde çalışır ve Windows ile birlikte başlar.
- Türkçe / English / العربية

## Kurulum

[Releases](../../releases/latest) sayfasından `DesktopDuzenSabitleyici-Setup-X.Y.Z.exe` kurulumunu indirip çalıştırın (tek tık, kullanıcı
başına, yönetici izni gerekmez). Program `%LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici` klasörüne kurulur, masaüstüne ve Başlat
menüsüne kısayol eklenir, "Uygulamalar" listesinden kaldırılabilir. Kayıtlarınız `%APPDATA%\DesktopDuzenSabitleyici` klasöründedir;
kurulum da kaldırma da onlara dokunmaz. Tek `DesktopDuzenSabitleyici.exe` dosyasını doğrudan çalıştırmak da hâlâ olur (kendini aynı klasöre kurar).

## Güncelleme

Sağ üstteki ⬇ butonuna basmanız yeterli. Uygulama yeni sürümü GitHub'dan indirir, indirirken yüzdeyi, MB miktarını, kalan MB'yi ve
hızı gösterir (yama kipinde "yalnızca değişen kısımlar"), SHA-256 ile doğrular ve kendini yeniler. Yeni sürüm çıktığında buton yeşile döner.

## Eşitleme

⇅ butonu, düzenlerinizi GitHub hesabınızdaki gizli bir gist'e yedekler ve diğer bilgisayarlarla eşitler. Bunun için bir kez erişim
anahtarı girmeniz gerekir. Anahtar girildikten sonra eşitleme her 5 dakikada bir kendiliğinden yapılır.
Hiçbir kayıt silinmez ya da üstüne yazılmaz: "Arşive taşı" dediğiniz, 25 kayıt sınırını aşan ya da eşitlemede çakışan kayıtlar
Ayarlar → Arşiv'de durur ve geri alınabilir; başka bilgisayarda arşive taşınan kayıt burada da arşive gider.

## Görünüm

Varsayılan Falcon görünümü (Inter / Noto Sans Arabic yazı tipleri). Ayarlar → Görünüm: 8 renk şeması (Falcon, Klasik = 1.0.x'in
renkleri, Zümrüt, Gün batımı, Mor, Okyanus, Grafit, Yüksek kontrast), koyu / açık / otomatik tema, yazı boyutu. Sağ üstte tema ve dil düğmeleri.

## Geliştirme

Ayrıntılar: `CLAUDE.md`. Her gönderimde GitHub Actions test eder ve derler; `main`'de `.csproj` `<Version>` için henüz yayın yoksa
ortak çekirdeğin release iş akışıyla `vX.Y.Z` yayınlanır.

## Küçük güncellemeler (yalnızca değişen kısımlar)
Her yayında CI (ortak çekirdek `reusable-release.yml`) son 5 sürümün her biri için FCD1 biçiminde
`DesktopDuzenSabitleyici-win-exe.patch-from-X.Y.Z.bin` yaması üretir, doğrular ve `latest.json`'a yazar.
Uygulama önce bu yamayı indirir, bilgisayardaki exe ile birleştirir ve SHA-256 ile doğrular. Yama yoksa veya bir sorun çıkarsa
otomatik olarak tam dosyaya geçer. 1.0.x sürümleri için eski biçimde (`DesktopDuzenSabitleyici-patch-from-X.Y.Z.bin`, DDSD1) köprü
yamaları da eklenir.

## Asistan Agent (1.0.12)
`src/FcAgent.cs` + `src/FcAgentDil.cs` (= ortak-cekirdek/dotnet): bütün programlardaki çalışmayı sayılarla izler, çevrim dışı kurallarla öneri
verir; sağ üstte ✦ (okunmamış sayısı), Ayarlar'da "✦ Asistan Agent" (iletişim düğmesinin önünde), tepside balon bildirimi.
Aynı bilgisayardaki programlarla %APPDATA%\FalconCekirdek\agent üzerinden paylaşım.
