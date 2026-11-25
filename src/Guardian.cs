using Microsoft.Win32;
using WinTimer = System.Windows.Forms.Timer;

namespace DDS;

/// <summary>
/// Watches the desktop. Saves the layout of every display setup separately (with history) and
/// restores it when the display, resolution or scaling changes, after sleep or when Explorer restarts.
///
/// Key rule: only arrangements that come from the user are ever saved. Right after a display change
/// the desktop is "untrusted" (Windows may have shuffled it, or it was adapted from another screen),
/// so nothing is saved until an exact restore has been verified or the user moves icons by hand.
/// All work runs on the UI thread (shell COM objects need an STA thread).
/// </summary>
public sealed class Guardian : IDisposable
{
    const int QuarantineSec = 90;

    readonly StoreData store;
    readonly Settings settings;
    readonly WinTimer tick = new() { Interval = 1500 };
    readonly WinTimer restoreTimer = new();
    readonly WinTimer autoSaveTimer = new() { Interval = 30 * 60 * 1000 };

    bool initialized;
    bool trusted;                     // the desktop as it is now is a real user arrangement
    List<IconPos>? lastTrusted;       // last reading taken while trusted
    string lastTrustedKey = "";
    List<MonitorInfo> lastTrustedMons = new();
    DateTime quarantineUntil = DateTime.MinValue;

    string lastKey = "";
    IntPtr lastViewHwnd;
    Dictionary<string, Point>? baseline;
    Dictionary<string, Point>? candidate;
    DateTime suppressUntil = DateTime.MinValue;
    int restoreStep = -1;
    List<IconPos>? target;
    bool targetExact, targetManual;
    string? forcedSnapId;

    public event Action<string, object[]>? Log;
    public event Action? Changed;
    /// <summary>Windows'un "Simgeleri otomatik düzenle" ayarı açıldı (oturum başına bir kez bildirilir).</summary>
    public event Action? AutoArrangeDetected;

    // 1.2.0: masaüstü görünüm ayarları ve uyarlanır tarama hızı
    public bool AutoArrangeOn { get; private set; }
    public bool SnapToGridOn { get; private set; }
    bool autoArrangeReported;
    int flagTicks;
    DateTime lastActivity = DateTime.Now;
    const int FastMs = 1500, IdleMs = 3000;
    /// <summary>Son geri yüklemede kullanılan kaydın türü ve adı (durum satırı için).</summary>
    public SourceKind? LastSourceKind { get; private set; }

    public int IconCount { get; private set; }
    public DateTime? LastSave { get; private set; }
    public DateTime? LastRestore { get; private set; }
    public List<MonitorInfo> Monitors { get; private set; } = new();
    public string CurrentKey => lastKey;
    public bool Busy => restoreStep >= 0;
    public bool Trusted => trusted;

    public Guardian(StoreData store, Settings settings)
    {
        this.store = store;
        this.settings = settings;
        tick.Tick += (_, _) => OnTick();
        restoreTimer.Tick += (_, _) => OnRestoreStep();
        autoSaveTimer.Tick += (_, _) => AutoSave();
        var latest = store.LatestUtc;
        if (latest > DateTime.MinValue) LastSave = latest.ToLocalTime();
    }

    public void Start()
    {
        Monitors = DisplayInfo.GetMonitors();
        lastKey = DisplayInfo.KeyOf(Monitors);
        SystemEvents.DisplaySettingsChanging += OnSystemDisplayChanged;
        SystemEvents.DisplaySettingsChanged += OnSystemDisplayChanged;
        SystemEvents.PowerModeChanged += OnPower;
        Emit("log_started", Friendly());
        if (Storage.Migrated > 0) Emit("log_migrated", Storage.Migrated);
        tick.Start();
        autoSaveTimer.Start();
    }

    string Friendly() => DisplayInfo.Friendly(Monitors, Lang.T("primary"));
    string FriendlyOf(List<MonitorInfo> mons) => DisplayInfo.Friendly(mons, Lang.T("primary"));

    void Emit(string key, params object[] args) => Log?.Invoke(key, args);

    bool InQuarantine => DateTime.Now < quarantineUntil;
    void Quarantine(int sec = QuarantineSec)
    {
        var until = DateTime.Now.AddSeconds(sec);
        if (until > quarantineUntil) quarantineUntil = until;
    }

    void OnSystemDisplayChanged(object? s, EventArgs e)
    {
        DisplayKind.Invalidate();
        Touch();
        // Even if the setup ends up the same (a short flicker of resolution), Windows may shuffle the icons.
        Quarantine();
        CheckDisplay();
    }

    void OnPower(object? s, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) { DisplayKind.Invalidate(); Touch(); Quarantine(); Emit("log_resume"); CheckDisplay(); }
        else if (e.Mode == PowerModes.Suspend) SaveBeforeShutdown("sleep", "log_sleep");
    }

    void CheckDisplay()
    {
        var mons = DisplayInfo.GetMonitors();
        var key = DisplayInfo.KeyOf(mons);
        if (key == lastKey) return;
        PreserveBeforeChange();
        Monitors = mons;
        lastKey = key;
        trusted = false;
        Touch();
        Quarantine();
        Emit("log_changed", Friendly());
        Changed?.Invoke();
        if (settings.Protect) ScheduleRestore(2500);
    }

    /// <summary>Bir şey oldu: birkaç dakika hızlı taramaya geç.</summary>
    void Touch()
    {
        lastActivity = DateTime.Now;
        if (tick.Interval != FastMs) tick.Interval = FastMs;
    }

    /// <summary>
    /// Düşük kaynak kullanımı: masaüstünde 3 dakikadır hiçbir şey değişmediyse tarama 3 saniyeye iner;
    /// ekran değişimi, uyanma, Explorer yeniden başlaması ya da simge hareketi anında hızlı taramaya döner.
    /// </summary>
    void AdaptTick()
    {
        bool fast = Busy || InQuarantine || (DateTime.Now - lastActivity).TotalMinutes < 3;
        int want = fast ? FastMs : IdleMs;
        if (tick.Interval != want) tick.Interval = want;
    }

    /// <summary>Windows'un "otomatik düzenle / kılavuza hizala" ayarlarını yaklaşık dakikada bir okur.</summary>
    void CheckFlags(bool force = false)
    {
        if (!force && ++flagTicks < 30) return;
        flagTicks = 0;
        try
        {
            var (auto, snap) = DesktopIcons.ReadFlags();
            if (auto == AutoArrangeOn && snap == SnapToGridOn) return;
            AutoArrangeOn = auto;
            SnapToGridOn = snap;
            if (auto && !autoArrangeReported)
            {
                autoArrangeReported = true;
                Emit("log_autoarr_on");
                AutoArrangeDetected?.Invoke();
            }
            Changed?.Invoke();
        }
        catch { }
    }

    /// <summary>Kullanıcı "Kapat" dedi: otomatik düzenle / kılavuza hizala kapatılır.</summary>
    public void TurnOffArrangeFlags()
    {
        try
        {
            DesktopIcons.TurnOff(AutoArrangeOn, SnapToGridOn);
            Emit("log_autoarr_off");
        }
        catch (Exception ex) { Emit("log_err", ex.Message); }
        CheckFlags(force: true);
        Changed?.Invoke();
    }

    /// <summary>
    /// Explorer yeniden başladı (çökme, güncelleme, oturum): MainForm "TaskbarCreated" yayınını alınca çağırır.
    /// Masaüstü penceresinin değişmesini beklemeden düzeni geri getirir.
    /// </summary>
    public void ExplorerRestarted()
    {
        Touch();
        Quarantine();
        if (!initialized || !settings.Protect) return;
        Emit("log_explorer");
        ScheduleRestore(3000);
    }

    void OnTick()
    {
        AdaptTick();
        CheckDisplay();
        if (Busy || DateTime.Now < suppressUntil) return;

        List<IconPos> cur;
        try { cur = DesktopIcons.Read(); }
        catch { return; }
        if (cur.Count == 0) return; // desktop not ready yet (sign-in) or empty

        if (!initialized) { Initialize(cur); CheckFlags(force: true); return; }
        CheckFlags();
        if (!settings.Protect) { baseline = null; return; }

        // Explorer restarted (crash, update, sign-in) -> it may have shuffled the icons.
        if (lastViewHwnd != IntPtr.Zero && DesktopIcons.LastViewHwnd != lastViewHwnd)
        {
            lastViewHwnd = DesktopIcons.LastViewHwnd;
            Quarantine();
            Emit("log_explorer");
            ScheduleRestore(2500);
            return;
        }
        lastViewHwnd = DesktopIcons.LastViewHwnd;

        if (IconCount != cur.Count) { IconCount = cur.Count; Changed?.Invoke(); }
        var map = ToMap(cur);

        if (baseline == null) { baseline = map; MarkTrusted(cur); return; }

        var (moved, membership) = Compare(baseline, map);
        if (!moved && !membership) { candidate = null; MarkTrusted(cur); return; }
        Touch();

        if (!moved)
        {
            // Icons were only added or removed: remember the new set (only if this desktop is a real arrangement).
            baseline = map;
            if (trusted) Save(cur, "user");
            MarkTrusted(cur);
            return;
        }

        // Something moved. Wait for one more identical reading so we never save in the middle of a drag.
        if (candidate == null || Compare(candidate, map).moved) { candidate = map; return; }

        bool reshuffle = LayoutEngine.LooksLikeReshuffle(baseline, map);
        candidate = null;
        if (reshuffle && InQuarantine && ChooseSource() != null)
        {
            // Windows rearranged the desktop on its own (screen change, sleep, Explorer): put it back.
            Emit("log_reshuffle");
            ScheduleRestore(300);
            return;
        }

        // A real change made by the user: from now on this screen's layout is the user's own.
        baseline = map;
        trusted = true;
        Save(cur, "user");
        MarkTrusted(cur);
        Emit("log_saved", cur.Count);
    }

    /// <summary>First successful look at the desktop after the app started.</summary>
    void Initialize(List<IconPos> cur)
    {
        initialized = true;
        IconCount = cur.Count;
        lastViewHwnd = DesktopIcons.LastViewHwnd;
        baseline = ToMap(cur);

        if (store.Displays.Count == 0)
        {
            trusted = true;
            Save(cur, "user");
            MarkTrusted(cur);
            Emit("log_saved", cur.Count);
            Changed?.Invoke();
            return;
        }

        // Just converted from the old version: old records may already be broken, never apply them on our own.
        if (Storage.Migrated > 0 || !settings.Protect)
        {
            trusted = settings.Protect;
            MarkTrusted(cur);
            Changed?.Invoke();
            return;
        }

        var src = ChooseSource();
        if (src != null)
        {
            var t = LayoutEngine.Build(src.Value.p, src.Value.s, Monitors, DesktopIcons.LastSpacing, out bool exact);
            if (!exact || LayoutEngine.Mismatches(t, cur, DesktopIcons.LastSpacing) >= 3)
            {
                // The desktop does not look like the saved layout of this screen (changed while the PC was off).
                Quarantine();
                ScheduleRestore(1000);
                return;
            }
        }
        trusted = true;
        MarkTrusted(cur);
        Changed?.Invoke();
    }

    void MarkTrusted(List<IconPos> cur)
    {
        if (!trusted) return;
        lastTrusted = cur;
        lastTrustedKey = lastKey;
        lastTrustedMons = Monitors;
    }

    /// <summary>
    /// The screen is about to change (or has just changed): store the last good layout of the
    /// old screen first, so Windows' reshuffle can never overwrite it.
    /// </summary>
    void PreserveBeforeChange()
    {
        if (!settings.Protect || Busy || !trusted || lastTrusted == null || lastTrusted.Count == 0 || lastTrustedKey != lastKey) return;
        var shape = LayoutEngine.Shape(lastTrustedMons, lastTrustedKey, FriendlyOf(lastTrustedMons));
        var snap = LayoutEngine.Capture(lastTrusted, lastTrustedMons, DesktopIcons.LastSpacing, "presave");
        if (store.Add(shape, snap)) Emit("log_presave", snap.Icons.Count);
        store.LastKey = lastTrustedKey;
        Storage.SaveStore(store);
        LastSave = DateTime.Now;
        lastTrusted = null;
    }

    /// <summary>Every 30 minutes: save the current layout (only a real user arrangement, never a half-restored one).</summary>
    void AutoSave()
    {
        if (!settings.Protect || !settings.AutoSave || !trusted || Busy || DateTime.Now < suppressUntil) return;
        try
        {
            if (DisplayInfo.CurrentKey() != lastKey) return;
            var cur = DesktopIcons.Read();
            if (cur.Count == 0) return;
            baseline = ToMap(cur);
            candidate = null;
            if (Save(cur, "auto")) Emit("log_autosave", cur.Count);
            MarkTrusted(cur);
        }
        catch { }
    }

    /// <summary>Windows is shutting down, restarting, signing out or going to sleep: save right now.</summary>
    public void SaveBeforeShutdown(string source, string reasonKey)
    {
        if (Busy || !trusted || !settings.Protect) return;
        // Reading the desktop can be refused while Windows is asking apps to close
        // (no outgoing COM calls inside that message), so fall back to the reading from 1.5 s ago.
        List<IconPos>? cur = null;
        try
        {
            if (DisplayInfo.CurrentKey() == lastKey) cur = DesktopIcons.Read();
        }
        catch { cur = null; }
        try
        {
            if (cur is { Count: > 0 }) { Save(cur, source); Emit(reasonKey, cur.Count); return; }
            if (lastTrusted != null && lastTrustedKey == lastKey) { Save(lastTrusted, source); Emit(reasonKey, lastTrusted.Count); }
        }
        catch { }
    }

    bool Save(List<IconPos> cur, string source)
    {
        var shape = LayoutEngine.Shape(Monitors, lastKey, Friendly());
        var snap = LayoutEngine.Capture(cur, Monitors, DesktopIcons.LastSpacing, source);
        bool added = store.Add(shape, snap);
        store.LastKey = lastKey;
        Storage.SaveStore(store);
        LastSave = DateTime.Now;
        Changed?.Invoke();
        return added;
    }

    /// <summary>Manual save (button, tray menu, Ctrl+Alt+S). Always trusted: the user says this is how it should look.</summary>
    public void SaveNow(string source = "manual")
    {
        try
        {
            CheckDisplay();
            var cur = DesktopIcons.Read();
            IconCount = cur.Count;
            baseline = ToMap(cur);
            candidate = null;
            trusted = true;
            Save(cur, source);
            MarkTrusted(cur);
            Emit("log_saved", cur.Count);
        }
        catch (Exception ex) { Emit("log_err", ex.Message); }
    }

    /// <summary>Protection was switched back on: forget the old reference and catch up with any screen change.</summary>
    public void Resume()
    {
        baseline = null;
        candidate = null;
        if (store.LastKey != "" && store.LastKey != lastKey) { Quarantine(); ScheduleRestore(500); }
    }

    public void RestoreNow()
    {
        if (ChooseSource() == null) { Emit("log_noLayout"); return; }
        ScheduleRestore(150);
    }

    /// <summary>Restores one specific entry from the history list, chosen by the user.</summary>
    public void RestoreSnapshot(string snapId)
    {
        if (store.Find(snapId) == null) { Emit("log_noLayout"); return; }
        ScheduleRestore(150, snapId);
    }

    public void ScheduleRestore(int delayMs, string? snapId = null)
    {
        restoreTimer.Stop();
        restoreStep = 0;
        target = null;
        forcedSnapId = snapId;
        restoreTimer.Interval = Math.Max(50, delayMs);
        restoreTimer.Start();
    }

    /// <summary>
    /// The best saved layout for the current screens:
    /// 1) this exact display setup, 2) the same monitors in another arrangement, 3) the newest layout of any other setup.
    /// </summary>
    (Profile p, Snapshot s)? ChooseSource()
    {
        var c = LayoutEngine.Choose(store.Displays.Values, lastKey, Monitors);
        return c == null ? null : (c.Value.p, c.Value.s);
    }

    /// <summary>Durum satırı için: bu ekranda hangi kayıt kullanılır (tam eşleşme / aynı ekranlar / en benzer).</summary>
    public (Profile p, SourceKind kind)? DescribeSource()
    {
        var c = LayoutEngine.Choose(store.Displays.Values, lastKey, Monitors.Count > 0 ? Monitors : DisplayInfo.GetMonitors());
        return c == null ? null : (c.Value.p, c.Value.kind);
    }

    List<IconPos>? ComputeTarget(out bool exact, IEnumerable<string>? present = null)
    {
        exact = false;
        Monitors = DisplayInfo.GetMonitors();
        lastKey = DisplayInfo.KeyOf(Monitors);
        (Profile p, Snapshot s)? src = forcedSnapId != null ? store.Find(forcedSnapId) : ChooseSource();
        if (src == null || src.Value.s.Icons.Count == 0) return null;
        if (forcedSnapId == null && LayoutEngine.Choose(store.Displays.Values, lastKey, Monitors) is { } ch)
        {
            LastSourceKind = ch.kind;
            if (ch.kind == SourceKind.Nearest) Emit("log_nearest", ch.p.Title);
        }
        return LayoutEngine.BuildAll(store.Displays.Values, src.Value.p, src.Value.s, Monitors,
            DesktopIcons.LastSpacing, present, out exact);
    }

    void OnRestoreStep()
    {
        restoreTimer.Stop();
        Touch();
        try
        {
            if (restoreStep == 0)
            {
                List<string>? present = null;
                try { present = DesktopIcons.Read().Select(i => i.Key).ToList(); } catch { } // also refreshes spacing for the new screen
                targetManual = forcedSnapId != null;
                target = ComputeTarget(out targetExact, present);
                if (target == null) { Finish(); return; }
                int n = DesktopIcons.Write(target, settings.DisableAutoArrange);
                LastRestore = DateTime.Now;
                Emit("log_restored", n, Lang.T(targetExact ? "how_exact" : "how_prop"));
                restoreStep = 1;
                restoreTimer.Interval = 2500;
                restoreTimer.Start();
                return;
            }

            // Windows sometimes keeps rearranging for a few seconds after a screen change; re-check and re-apply.
            var cur = DesktopIcons.Read();
            if (target != null && LayoutEngine.Mismatches(target, cur, DesktopIcons.LastSpacing) > 0 && restoreStep < 4)
            {
                DesktopIcons.Write(target, settings.DisableAutoArrange);
                restoreStep++;
                restoreTimer.Interval = restoreStep == 2 ? 3000 : 5000;
                restoreTimer.Start();
                return;
            }
            Finish();
        }
        catch (Exception ex)
        {
            Emit("log_err", ex.Message);
            if (restoreStep < 4) { restoreStep++; restoreTimer.Interval = 3000; restoreTimer.Start(); }
            else Finish();
        }
    }

    void Finish()
    {
        var t = target;
        bool wasExact = targetExact, wasManual = targetManual;
        restoreStep = -1;
        target = null;
        forcedSnapId = null;
        candidate = null;
        suppressUntil = DateTime.Now.AddSeconds(1.5);
        try
        {
            var cur = DesktopIcons.Read();
            IconCount = cur.Count;
            lastViewHwnd = DesktopIcons.LastViewHwnd;
            baseline = ToMap(cur);
            if (wasManual && cur.Count > 0)
            {
                // The user picked this record: it becomes the current layout of this screen.
                trusted = true;
                Save(cur, "manual");
            }
            else if (t != null && wasExact && LayoutEngine.Mismatches(t, cur, DesktopIcons.LastSpacing) <= 1)
                trusted = true; // the saved layout of this exact screen is back in place
            else if (t != null && !wasExact)
            {
                trusted = false; // adapted from another screen: not saved until the user touches it
                Emit("log_adapted");
            }
            MarkTrusted(cur);
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
        SystemEvents.DisplaySettingsChanging -= OnSystemDisplayChanged;
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPower;
        tick.Dispose();
        restoreTimer.Dispose();
        autoSaveTimer.Dispose();
    }
}
