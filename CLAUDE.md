# Desktop Simge Sabitleyici

Masaüstü simgelerinin yerini koruyan Windows aracı. .NET 8 WinForms, çerçevesiz pencere, tek exe (self-contained single-file),
sistem tepsisinde çalışır. Kaynak `src/`, testler `tests/CoreTests`, kurulum `installer/setup.nsi`, CI `.github/workflows/release.yml`.

## Ortak çekirdek (v1.1.0'dan beri)
- C# olduğu için web/Qt çekirdeği doğrudan kullanılmaz; davranış port edildi. Gömülü kopya `cekirdek/` (hangi commit: `cekirdek/VERSION`):
  `web/tools/schemes.js`, `web/js/{i18n,look,progress,crash,…}.js`, `delta/node/fcdelta.js`, `fonts/*.ttf` (Inter + Noto Sans Arabic, OFL).
  ortak-cekirdek deposunu buradan değiştirme.
- `node tools/gen-core.js` → `src/Core/Generated.cs` (şema tokenları + klasik şema + fc.* metinleri tr/en/ar) ve `tests/fixtures/*`
  (ilerleme satırı, çökme maskesi, FCD1 vektörleri). Elle düzenleme yok; `--check` CI'da çalışır.
- `src/Core/`: `Look` (şema/mod/yazı boyutu, token → renk), `Fonts` (PrivateFontCollection + AddFontMemResourceEx), `Fcd1` (yama uygulayıcı),
  `Inbox` (admin gelen kutusu, kuyruk), `Crash` (çökme raporu), `CoreState` (`cekirdek.json`), `CoreForms` (rehber, neler yeni, iletişim, arşiv, News).
- Görünüm: varsayılan `falcon`; `klasik` = 1.0.x renkleri (gen-core.js'te). `Theme.*` renkleri etkin şemadan gelir; şema/tema/dil değişince
  `MainForm.RebuildUI()` pencereyi yeniden kurar.
- Eşitleme: kullanıcının kendi GitHub gist'i (Wi‑Fi/LAN yok — tek masaüstü aracı). Kural (`SyncMerge`, GitHubSync.cs): hiçbir kayıt silinmez;
  "sil" = Arşiv'e taşı, 25 sınırını aşan → arşiv, çakışmada kaybeden → arşiv ("conflict"), uzakta arşivlenen → yerelde de arşiv.
  Arşiv 500'ü aşarsa en eskiler `arsiv-tasma.jsonl`'a eklenir.

## Ad (1.1.3)
- Görünen ad **Desktop Simge Sabitleyici** (en: Desktop Icon Keeper, ar: مثبّت أيقونات سطح المكتب) — `Installer.DisplayName`.
  Depo, exe, klasör, veri, kayıt defteri, olay/mutex adları ve release asset adları eski `DesktopDuzenSabitleyici` olarak KALIR (güncelleme uyumu).
  `Installer.MigrateName` eski adlı kısayolları yerinde yeniden adlandırır ve kayıtlardaki kısayol anahtarını günceller.

## Kurallar (Milen)
- Yazışma ve arayüz Türkçe; arayüz tr/en/ar (Arapça RTL). Yeni metin: `src/Lang.cs` (üçlü) ya da çekirdek metni `CoreStrings.T("fc.…")`.
- Sürüm semver X.Y.Z, etiket `vX.Y.Z`. Tek kaynak `src/DesktopDuzenSabitleyici.csproj` `<Version>`; ayrıca `News.Items` (src/Core/CoreForms.cs)
  en üste yeni sürüm (tr/en/ar) ve `release-notes.json`. `node tools/check-version.js` tutarlılığı denetler.
- Veri klasörü `%APPDATA%\DesktopDuzenSabitleyici` (layouts.json, settings.json, cekirdek.json) ve kurulum klasörü
  `%LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici` DEĞİŞMEZ. Atomik yazma.
- Ayarlar sırası: koruma · Görünüm · Arşiv · Kullanım Limitleri · Otomatik hata raporu · Hakkında · Asistan Agent · **Admin ile iletişim EN SON**.
- Sağ üst (soldan sağa): ✦ · 🔔 · Eşitle · Güncelle · tema · dil | Küçült · Orta · Tam ekran · Kapat. Her açılışta tam ekran.

## Güncelleme / yayın
- İstemci (`src/Updater.cs`): `releases/latest/download/latest.json` → kanal `win-exe` (tek exe; asset adı eski adla
  `DesktopDuzenSabitleyici.exe`), `minVersion` altı → `win-setup` (NSIS). Önce kurulu sürümün FCD1 yaması, SHA-256; olmazsa tam dosya.
  latest.json yoksa (404) eski API yoluna düşer.
- CI: `test` (ubuntu: check-version, gen-core --check, `dotnet run --project tests/CoreTests`, NSIS sözdizimi) → `build` (windows: publish,
  NSIS, `--screenshot` açılış testi + ekran görüntüleri → `ci-ekran` dalı) → `release` (main; ortak `reusable-release.yml`,
  `legacy_assets: win-exe=DesktopDuzenSabitleyici.exe`) → `legacy-patches`.
- KÖPRÜ: 1.0.x istemcileri `api.github.com/.../releases/latest`te `DesktopDuzenSabitleyici.exe` ve DDSD1 `DesktopDuzenSabitleyici-patch-from-X.bin`
  arar; `legacy-patches` işi (tools/DeltaMaker, DDSD1) bunları ekler. tests/CoreTests/Old/ = 1.0.11'in güncelleyicisi birebir, köprü testi.
- Yayın: main'e gönder ya da `gh api -X POST repos/departures-mil/DesktopDuzenSabitleyici/actions/workflows/release.yml/dispatches -f ref=main`.

## Test
- `dotnet run --project tests/CoreTests -c Release` (Linux'ta da; NuGet gerekmez). WinForms kısmı yalnız Windows CI'da derlenir.
- Ekran görüntüsü elle: `DesktopDuzenSabitleyici.exe --screenshot a.png --lang ar --scheme falcon --theme light --dialog onboarding`.
