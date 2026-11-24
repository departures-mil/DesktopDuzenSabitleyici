using DDS.Core;

namespace DDS;

static class Program
{
    public const string ShowEventName = "DesktopDuzenSabitleyici_Show_v1";
    /// <summary>Kurulum programı çalışan kopyayı kapatmak için kullanır (--quit).</summary>
    public const string QuitEventName = "DesktopDuzenSabitleyici_Quit_v1";
    static bool errorShown;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--quit"))
        {
            try { EventWaitHandle.OpenExisting(QuitEventName).Set(); } catch { }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--screenshot") return Screenshot(args);

        if (Installer.RelocateIfNeeded(args)) return 0;

        using var mutex = new Mutex(true, "DesktopDuzenSabitleyici_Single_v1", out bool first);
        if (!first)
        {
            if (args.Contains("--tray")) return 0; // already running: stay quiet at startup
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return 0;
        }

        InstallCrashReporting();
        ApplicationConfiguration.Initialize();
        Fonts.Load();
        Application.Run(new MainForm(startHidden: args.Contains("--tray")));
        GC.KeepAlive(mutex);
        return 0;
    }

    /// <summary>Çökme raporu (SPEC): beklenmeyen hata → admin gelen kutusu (tur:"cokme"), günde en çok 5, aynı imza bir kez.</summary>
    static void InstallCrashReporting()
    {
        CoreStrings.Lang = () => Lang.Current;
        Crash.Version = () => Updater.CurrentText;
        Inbox.Version = () => Updater.CurrentText;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Crash.Report(e.Exception, "ui");
            if (errorShown) return;
            errorShown = true;
            try
            {
                MessageBox.Show(Lang.T("err_unexpected") + "\n\n" + e.Exception.Message, Lang.T("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, Lang.IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);
            }
            catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash.Report(e.ExceptionObject as Exception, "domain", waitMs: 4000);
        TaskScheduler.UnobservedTaskException += (_, e) => { Crash.Report(e.Exception, "task"); e.SetObserved(); };
    }

    /// <summary>
    /// CI ekran görüntüleri: --screenshot &lt;dosya.png&gt; [--lang tr|en|ar] [--scheme falcon|klasik|…] [--theme dark|light] [--dialog onboarding|whatsnew|contact|archive]
    /// Örnek verilerle açar, hiçbir şeyi diske yazmaz, korumayı başlatmaz.
    /// </summary>
    static int Screenshot(string[] args)
    {
        string? Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        Storage.ReadOnly = true;
        CoreState.FilePath = null;
        var s = new Settings
        {
            Lang = Arg("--lang") ?? "tr", Scheme = Arg("--scheme") ?? "falcon", Theme = Arg("--theme") ?? "dark",
            Onboarded = true, WhatsNewSeen = Updater.CurrentText, StartWithWindows = true,
        };
        Lang.Current = s.Lang;
        CoreStrings.Lang = () => Lang.Current;
        var st = SampleStore();
        ApplicationConfiguration.Initialize();
        Fonts.Load();
        int code = 0;
        var form = new MainForm(false, s, st, screenshot: true);
        form.Shown += async (_, _) =>
        {
            try { await form.CaptureAsync(args[1], Arg("--dialog")); }
            catch (Exception ex) { Console.Error.WriteLine(ex); code = 1; }
            Application.Exit();
        };
        Application.Run(form);
        return code;
    }

    static StoreData SampleStore()
    {
        var st = new StoreData();
        var rnd = new Random(7);
        Snapshot Snap(int daysAgo, string src, int n)
        {
            var sn = new Snapshot { SavedUtc = DateTime.UtcNow.AddDays(-daysAgo).AddMinutes(-rnd.Next(600)), Source = src };
            for (int i = 0; i < n; i++)
                sn.Icons.Add(new IconEntry { Key = "Simge " + i, X = 20 + (i / 8) * 100, Y = 10 + (i % 8) * 110, RX = (i / 8) * 0.05, RY = (i % 8) * 0.11 });
            return sn;
        }
        void Add(string key, string display, params Snapshot[] snaps)
        {
            var p = new Profile { Key = key, Display = display, Monitors = StoreData.MonitorsFromKey(key) };
            foreach (var sn in snaps.OrderByDescending(x => x.SavedUtc)) p.History.Add(sn);
            st.Displays[key] = p;
        }
        Add("DELL U2720Q:3840x2160@0,0~144P", "DELL U2720Q 3840×2160 %150", Snap(0, "user", 24), Snap(1, "auto", 24), Snap(3, "manual", 22));
        Add("Laptop:1920x1080@0,0~120P", "Dizüstü ekranı 1920×1080 %125", Snap(2, "presave", 18), Snap(5, "shutdown", 18));
        Add("Laptop:1920x1080@0,0~120P|LG 27UL500:3840x2160@1920,0~96", "Dizüstü + LG 27UL500", Snap(4, "hotkey", 30));
        st.Archive.Add(new ArchiveEntry { Key = "Laptop:1920x1080@0,0~120P", Display = "Dizüstü ekranı 1920×1080 %125", Reason = "deleted", ArchivedUtc = DateTime.UtcNow.AddDays(-1), Snap = Snap(9, "auto", 17) });
        st.Archive.Add(new ArchiveEntry { Key = "DELL U2720Q:3840x2160@0,0~144P", Display = "DELL U2720Q 3840×2160 %150", Reason = "old", ArchivedUtc = DateTime.UtcNow.AddDays(-2), Snap = Snap(30, "user", 21) });
        return st;
    }
}
