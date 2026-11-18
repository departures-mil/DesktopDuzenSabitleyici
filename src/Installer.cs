using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using DDS.Native;
using Microsoft.Win32;

namespace DDS;

/// <summary>Puts the app in a fixed place (no admin needed), creates shortcuts and handles "start with Windows".</summary>
public static class Installer
{
    public static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DesktopDuzenSabitleyici");
    public static readonly string InstallExe = Path.Combine(InstallDir, "DesktopDuzenSabitleyici.exe");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "DesktopDuzenSabitleyici";

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
        var name = "Desktop Düzen Sabitleyici.lnk";
        TryShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), name));
        var desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name);
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
            link.SetDescription("Desktop Düzen Sabitleyici");
            ((IPersistFile)obj).Save(lnk, true);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
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
        }
        catch { }
    }
}
