using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using DDS.Native;
using Microsoft.Win32;

namespace DDS;

/// <summary>
/// Puts the app in a fixed place (no admin needed), creates shortcuts and handles "start with Windows".
/// v1.1.0'dan beri asıl kurulum NSIS ile (installer/setup.nsi, kullanıcı başına, aynı klasör:
/// %LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici). Tek exe'yi doğrudan çalıştıranlar için kendini kurma korunur.
/// Veri her zaman %APPDATA%\DesktopDuzenSabitleyici (değişmez).
/// </summary>
public static class Installer
{
    public static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DesktopDuzenSabitleyici");
    public static readonly string InstallExe = Path.Combine(InstallDir, "DesktopDuzenSabitleyici.exe");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "DesktopDuzenSabitleyici";
    /// <summary>Görünen ad (1.1.3'ten beri). Klasör, exe, veri ve kayıt defteri adları DEĞİŞMEZ (eski sürümlerle uyum).</summary>
    public const string DisplayName = "Desktop Simge Sabitleyici";
    public const string OldDisplayName = "Desktop Düzen Sabitleyici";
    static string Lnk => DisplayName + ".lnk";
    static string OldLnk => OldDisplayName + ".lnk";
    static string ProgramsDir => Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>If started from somewhere else (e.g. Downloads), copy to the install folder and relaunch from there.</summary>
    public static bool RelocateIfNeeded(string[] args)
    {
        try
        {
            var me = Environment.ProcessPath;
            if (me == null || args.Contains("--portable")) return false;
            if (string.Equals(Path.GetFullPath(me), Path.GetFullPath(InstallExe), StringComparison.OrdinalIgnoreCase)) return false;

            Directory.CreateDirectory(InstallDir);
            try { File.Copy(me, InstallExe, true); }
            catch (IOException) { /* installed copy is running: just bring it up */ }

            if (!File.Exists(InstallExe)) return false;
            CreateShortcuts();
            Process.Start(new ProcessStartInfo(InstallExe) { UseShellExecute = true, WorkingDirectory = InstallDir });
            return true;
        }
        catch { return false; }
    }

    public static void CreateShortcuts()
    {
        RenameShortcuts();
        TryShortcut(Path.Combine(ProgramsDir, Lnk));
        var desk = Path.Combine(DesktopDir, Lnk);
        if (!File.Exists(desk)) TryShortcut(desk);
    }

    static void TryShortcut(string lnk)
    {
        try
        {
            var t = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true)!;
            var obj = Activator.CreateInstance(t)!;
            var link = (IShellLinkW)obj;
            link.SetPath(InstallExe);
            link.SetWorkingDirectory(InstallDir);
            link.SetIconLocation(InstallExe, 0);
            link.SetDescription(DisplayName);
            ((IPersistFile)obj).Save(lnk, true);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
        }
        catch { }
    }

    /// <summary>Eski adlı kısayolları yeni ada taşır (masaüstündeki yerinde yeniden adlandırılır, konumu korunur).
    /// Masaüstü kısayolu yeniden adlandırıldıysa yolu döner (eski, yeni); kayıtlardaki anahtarlar buna göre güncellenir.</summary>
    static (string oldPath, string newPath)? RenameShortcuts()
    {
        (string, string)? desk = null;
        try
        {
            var po = Path.Combine(ProgramsDir, OldLnk);
            var pn = Path.Combine(ProgramsDir, Lnk);
            if (File.Exists(po)) { if (File.Exists(pn)) File.Delete(po); else File.Move(po, pn); }
        }
        catch { }
        try
        {
            var dOld = Path.Combine(DesktopDir, OldLnk);
            var dNew = Path.Combine(DesktopDir, Lnk);
            if (File.Exists(dOld))
            {
                if (File.Exists(dNew)) File.Delete(dOld);
                else { File.Move(dOld, dNew); desk = (dOld, dNew); }
            }
        }
        catch { }
        return desk;
    }

    /// <summary>1.1.3 ad değişikliği: kısayollar, "Uygulamalar" listesindeki ad ve kayıtlı düzenlerdeki kısayol anahtarı.
    /// Hiçbir kayıt silinmez; yalnızca eski kısayol yolu yeni yolla değiştirilir. Kayıt değiştiyse true.</summary>
    public static bool MigrateName(StoreData store)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(UninstallKey, true);
            if (k != null && (k.GetValue("DisplayName") as string) != DisplayName) k.SetValue("DisplayName", DisplayName);
        }
        catch { }
        RenameShortcuts();
        // Eski adlı masaüstü kısayolunun kayıtlardaki anahtarını yeni ada çevir (her masaüstü klasörü için: eşitlenen diğer bilgisayarlar dahil).
        bool changed = false;
        void Fix(Snapshot? s)
        {
            if (s == null) return;
            foreach (var i in s.Icons)
                if (i.Key.EndsWith("\\" + OldLnk, StringComparison.OrdinalIgnoreCase))
                {
                    var nk = i.Key.Substring(0, i.Key.Length - OldLnk.Length) + Lnk;
                    if (s.Icons.Any(o => string.Equals(o.Key, nk, StringComparison.OrdinalIgnoreCase))) continue;
                    i.Key = nk; changed = true;
                }
        }
        try
        {
            foreach (var p in store.Displays.Values) foreach (var s in p.History) Fix(s);
            foreach (var a in store.Archive) Fix(a.Snap);
        }
        catch { }
        return changed;
    }

    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopDuzenSabitleyici";

    /// <summary>Uygulama içinden güncellenince "Uygulamalar" listesindeki sürüm de güncel kalsın (NSIS kaydı varsa).</summary>
    public static void TouchUninstallEntry()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(UninstallKey, true);
            if (k != null && (k.GetValue("DisplayVersion") as string) != Updater.CurrentText) k.SetValue("DisplayVersion", Updater.CurrentText);
        }
        catch { }
    }

    public static void SetStartup(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue(RunName, $"\"{Environment.ProcessPath}\" --tray");
            else k.DeleteValue(RunName, false);
            if (on)
            {
                // If it was switched off in Task Manager > Startup apps, switch it back on.
                using var ap = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                if (ap.GetValue(RunName) is byte[] b && b.Length > 0 && b[0] != 2)
                    ap.SetValue(RunName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            }
        }
        catch { }
    }
}
