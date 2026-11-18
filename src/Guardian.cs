using Microsoft.Win32;
using WinTimer = System.Windows.Forms.Timer;

namespace DDS;

/// <summary>
/// Watches the desktop. Saves the layout whenever the user rearranges icons, and restores it
/// automatically when the display, resolution or scaling changes (or Explorer restarts).
/// All work runs on the UI thread (shell COM objects need an STA thread).
/// </summary>
public sealed class Guardian : IDisposable
{
    readonly StoreData store;
    readonly Settings settings;
    readonly WinTimer tick = new() { Interval = 1500 };
    readonly WinTimer restoreTimer = new();

    string lastKey = "";
    IntPtr lastViewHwnd;
    Dictionary<string, Point>? baseline;
    Dictionary<string, Point>? candidate;
    DateTime suppressUntil = DateTime.MinValue;
    int restoreStep = -1;
    List<IconPos>? target;

    public event Action<string, object[]>? Log;
    public event Action? Changed;

    public int IconCount { get; private set; }
    public DateTime? LastSave { get; private set; }
    public DateTime? LastRestore { get; private set; }
    public List<MonitorInfo> Monitors { get; private set; } = new();
    public string CurrentKey => lastKey;
    public bool Busy => restoreStep >= 0;

    public Guardian(StoreData store, Settings settings)
    {
        this.store = store;
        this.settings = settings;
        tick.Tick += (_, _) => OnTick();
        restoreTimer.Tick += (_, _) => OnRestoreStep();
        if (store.Master != null) LastSave = store.Master.SavedUtc.ToLocalTime();
    }

    public void Start()
    {
        Monitors = DisplayInfo.GetMonitors();
        lastKey = DisplayInfo.KeyOf(Monitors);
        SystemEvents.DisplaySettingsChanged += OnSystemDisplayChanged;
        SystemEvents.PowerModeChanged += OnPower;
        Emit("log_started", Friendly());

        // The screen setup is different from the one we saw last time -> restore right away.
        if (settings.Protect && store.Master != null && store.LastKey != "" && store.LastKey != lastKey)
            ScheduleRestore(3000);
        tick.Start();
    }

    string Friendly() => DisplayInfo.Friendly(Monitors, Lang.T("primary"));

    void Emit(string key, params object[] args) => Log?.Invoke(key, args);

    void OnSystemDisplayChanged(object? s, EventArgs e) => CheckDisplay();

    void OnPower(object? s, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) { Emit("log_resume"); CheckDisplay(); }
    }

    void CheckDisplay()
    {
        var mons = DisplayInfo.GetMonitors();
        var key = DisplayInfo.KeyOf(mons);
        if (key == lastKey) return;
        Monitors = mons;
        lastKey = key;
        Emit("log_changed", Friendly());
        Changed?.Invoke();
        if (settings.Protect) ScheduleRestore(2500);
    }

    void OnTick()
    {
        CheckDisplay();
        if (!settings.Protect || Busy || DateTime.Now < suppressUntil) return;

        List<IconPos> cur;
        try { cur = DesktopIcons.Read(); }
        catch { return; }

        // Explorer restarted (crash, update, sign-in) -> it may have shuffled the icons.
        if (lastViewHwnd != IntPtr.Zero && DesktopIcons.LastViewHwnd != lastViewHwnd && store.Master != null)
        {
            lastViewHwnd = DesktopIcons.LastViewHwnd;
            Emit("log_explorer");
            ScheduleRestore(2500);
            return;
        }
        lastViewHwnd = DesktopIcons.LastViewHwnd;

        if (IconCount != cur.Count) { IconCount = cur.Count; Changed?.Invoke(); }
        var map = ToMap(cur);

        if (baseline == null)
        {
            baseline = map;
            if (store.Master == null) { Save(cur); Emit("log_saved", cur.Count); }
            else if (store.LastKey == "") { store.LastKey = lastKey; Storage.SaveStore(store); }
            return;
        }

        var (moved, membership) = Compare(baseline, map);
        if (!moved && !membership) { candidate = null; return; }

        if (!moved)
        {
            // Icons were only added or removed: keep everything else, just remember the new set.
            baseline = map;
            Save(cur);
            return;
        }

        // Something moved. Wait for one more identical reading so we never save in the middle of a drag.
        if (candidate != null && !Compare(candidate, map).moved)
        {
            baseline = map;
            candidate = null;
            Save(cur);
            Emit("log_saved", cur.Count);
        }
        else candidate = map;
    }

    void Save(List<IconPos> cur)
    {
        var layout = LayoutEngine.Capture(cur, Monitors, lastKey, Friendly());
        store.Master = layout;
        store.Profiles[lastKey] = layout;
        store.LastKey = lastKey;
        Storage.SaveStore(store);
        LastSave = DateTime.Now;
        Changed?.Invoke();
    }

    public void SaveNow()
    {
        try
        {
            var cur = DesktopIcons.Read();
            IconCount = cur.Count;
            baseline = ToMap(cur);
            candidate = null;
            Save(cur);
            Emit("log_saved", cur.Count);
        }
        catch (Exception ex) { Emit("log_err", ex.Message); }
    }

    /// <summary>Protection was switched back on: forget the old reference and catch up with any screen change.</summary>
    public void Resume()
    {
        baseline = null;
        candidate = null;
        if (store.Master != null && store.LastKey != "" && store.LastKey != lastKey) ScheduleRestore(500);
    }

    public void RestoreNow()
    {
        if (store.Master == null) { Emit("log_noLayout"); return; }
        ScheduleRestore(150);
    }

    /// <summary>Called after the store was replaced by a sync, to bring the desktop in line.</summary>
    public void ReloadAfterSync() => ScheduleRestore(300);

    public void ScheduleRestore(int delayMs)
    {
        restoreTimer.Stop();
        restoreStep = 0;
        target = null;
        restoreTimer.Interval = Math.Max(50, delayMs);
        restoreTimer.Start();
    }

    List<IconPos>? ComputeTarget(out bool exact)
    {
        exact = false;
        var mons = DisplayInfo.GetMonitors();
        Monitors = mons;
        lastKey = DisplayInfo.KeyOf(mons);

        if (settings.Mode == 1 && store.Profiles.TryGetValue(lastKey, out var prof) && prof.Icons.Count > 0)
        {
            exact = true;
            return LayoutEngine.Absolute(prof);
        }
        var m = store.Master;
        if (m == null || m.Icons.Count == 0) return null;
        if (m.ConfigKey == lastKey) { exact = true; return LayoutEngine.Absolute(m); }
        return LayoutEngine.Proportional(m, mons, DesktopIcons.LastSpacing);
    }

    void OnRestoreStep()
    {
        restoreTimer.Stop();
        try
        {
            if (restoreStep == 0)
            {
                try { DesktopIcons.Read(); } catch { } // refresh spacing for the new screen
                target = ComputeTarget(out bool exact);
                if (target == null) { Finish(); return; }
                int n = DesktopIcons.Write(target, settings.DisableAutoArrange);
                LastRestore = DateTime.Now;
                Emit("log_restored", n, Lang.T(exact ? "how_exact" : "how_prop"));
                restoreStep = 1;
                restoreTimer.Interval = 3500;
                restoreTimer.Start();
                return;
            }

            // Windows sometimes keeps rearranging for a few seconds after a screen change; re-check and re-apply.
            var cur = DesktopIcons.Read();
            if (target != null && LayoutEngine.Mismatches(target, cur, DesktopIcons.LastSpacing) > 0 && restoreStep < 3)
            {
                DesktopIcons.Write(target, settings.DisableAutoArrange);
                restoreStep++;
                restoreTimer.Interval = restoreStep == 2 ? 4000 : 6000;
                restoreTimer.Start();
                return;
            }
            Finish();
        }
        catch (Exception ex)
        {
            Emit("log_err", ex.Message);
            if (restoreStep < 3) { restoreStep++; restoreTimer.Interval = 3000; restoreTimer.Start(); }
            else Finish();
        }
    }

    void Finish()
    {
        restoreStep = -1;
        target = null;
        candidate = null;
        suppressUntil = DateTime.Now.AddSeconds(1.5);
        try
        {
            var cur = DesktopIcons.Read();
            IconCount = cur.Count;
            lastViewHwnd = DesktopIcons.LastViewHwnd;
            baseline = ToMap(cur);
            // Remember how this screen looks now, without touching the user's master arrangement.
            if (cur.Count > 0)
                store.Profiles[lastKey] = LayoutEngine.Capture(cur, Monitors, lastKey, Friendly());
        }
        catch { baseline = null; }
        store.LastKey = lastKey;
        Storage.SaveStore(store);
        Changed?.Invoke();
    }

    static Dictionary<string, Point> ToMap(List<IconPos> l)
    {
        var d = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in l) d[i.Key] = new Point(i.X, i.Y);
        return d;
    }

    static (bool moved, bool membership) Compare(Dictionary<string, Point> a, Dictionary<string, Point> b)
    {
        bool moved = false, membership = a.Count != b.Count;
        foreach (var kv in b)
        {
            if (!a.TryGetValue(kv.Key, out var p)) { membership = true; continue; }
            if (Math.Abs(p.X - kv.Value.X) > 3 || Math.Abs(p.Y - kv.Value.Y) > 3) moved = true;
        }
        return (moved, membership);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPower;
        tick.Dispose();
        restoreTimer.Dispose();
    }
}
