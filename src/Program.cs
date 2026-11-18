namespace DDS;

static class Program
{
    public const string ShowEventName = "DesktopDuzenSabitleyici_Show_v1";

    [STAThread]
    static void Main(string[] args)
    {
        if (Installer.RelocateIfNeeded(args)) return;

        using var mutex = new Mutex(true, "DesktopDuzenSabitleyici_Single_v1", out bool first);
        if (!first)
        {
            if (args.Contains("--tray")) return; // already running: stay quiet at startup
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(startHidden: args.Contains("--tray")));
        GC.KeepAlive(mutex);
    }
}
