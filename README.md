# Desktop Düzen Sabitleyici

Masaüstü simgelerinin yerini korur. Laptop ekranı, büyük monitör, farklı çözünürlük ya da farklı ölçek (%100 / %125 / %150) arasında geçiş yaptığınızda simgeler artık karışmaz.

## Nasıl çalışır?

- Simgeleri istediğiniz gibi dizin. Uygulama düzeni kendiliğinden kaydeder.
- Ekran, çözünürlük veya ölçek değiştiğinde (monitör takma/çıkarma, uykudan uyanma, Explorer'ın yeniden başlaması) düzeni birkaç saniye içinde geri getirir.
- **Oransal mod (varsayılan):** Aynı düzen yeni ekrana orantılı olarak taşınır. Sol üstteki sol üstte, ortadaki ortada kalır.
- **Ekran başına profil:** Her ekran kendi düzenini hatırlar. İlk kez görülen bir ekranda oransal düzen kullanılır.
- Arka planda, sistem tepsisinde çalışır ve Windows ile birlikte başlar.
- Türkçe / English / العربية

## Kurulum

[Releases](../../releases/latest) sayfasından `DesktopDuzenSabitleyici.exe` dosyasını indirip çalıştırın. Uygulama kendini
`%LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici` klasörüne kurar, masaüstüne ve Başlat menüsüne kısayol ekler. Yönetici izni gerekmez.

## Güncelleme

Sağ üstteki ⬇ butonuna basmanız yeterli. Uygulama yeni sürümü GitHub'dan indirir, indirirken yüzdeyi, MB miktarını, kalan MB'yi ve
hızı gösterir, sonra kendini yeniler. Yeni sürüm çıktığında buton yeşile döner.

## Eşitleme

⇅ butonu, düzenlerinizi GitHub hesabınızdaki gizli bir gist'e yedekler ve diğer bilgisayarlarla eşitler. Bunun için bir kez erişim
anahtarı girmeniz gerekir. Anahtar girildikten sonra eşitleme her 5 dakikada bir kendiliğinden yapılır.

## Geliştirme

`main` dalına yapılan her gönderimde GitHub Actions uygulamayı derler. `.csproj` içindeki `<Version>` için henüz bir yayın yoksa
`vX.Y.Z` yayınını otomatik oluşturur.

## Küçük güncellemeler (yalnızca değişen kısımlar)
Her yayında CI, son 5 sürümün her biri için `DesktopDuzenSabitleyici-patch-from-X.Y.Z.bin` yaması üretir ve doğrular.
Uygulama önce bu yamayı indirir (genelde 1 MB'ın altında), bilgisayardaki exe ile birleştirir ve SHA-256 ile doğrular.
Yama yoksa veya bir sorun çıkarsa otomatik olarak tam dosyaya geçer.

## Asistan Agent (1.0.12)
`src/FcAgent.cs` + `src/FcAgentDil.cs` (= ortak-cekirdek/dotnet): bütün programlardaki çalışmayı sayılarla izler, çevrim dışı kurallarla öneri
verir; sağ üstte ✦ (okunmamış sayısı), Ayarlar'da "✦ Asistan Agent" (iletişim düğmesinin önünde), tepside balon bildirimi.
Aynı bilgisayardaki programlarla %APPDATA%\FalconCekirdek\agent üzerinden paylaşım.
