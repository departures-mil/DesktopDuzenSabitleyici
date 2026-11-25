using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDS;

public sealed class IconEntry
{
    public string Key { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string Mon { get; set; } = "";
    public bool Primary { get; set; }
    public double RX { get; set; }
    public double RY { get; set; }
}

/// <summary>A monitor as it was when a profile was recorded (screen coordinates, physical pixels).</summary>
public sealed class MonitorRec
{
    public string Model { get; set; } = "";
    public bool Primary { get; set; }
    public int Dpi { get; set; } = 96;
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int WX { get; set; }
    public int WY { get; set; }
    public int WW { get; set; }
    public int WH { get; set; }
    /// <summary>true = dizüstünün kendi ekranı, false = harici monitör, null = bilinmiyor (1.2.0 öncesi kayıtlar).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Internal { get; set; }

    [JsonIgnore] public Rectangle Bounds => new(X, Y, W, H);
    [JsonIgnore] public Rectangle Work => WW > 0 && WH > 0 ? new(WX, WY, WW, WH) : Bounds;

    public static MonitorRec From(MonitorInfo m) => new()
    {
        Model = m.Model, Primary = m.Primary, Dpi = m.Dpi,
        X = m.Bounds.X, Y = m.Bounds.Y, W = m.Bounds.Width, H = m.Bounds.Height,
        WX = m.Work.X, WY = m.Work.Y, WW = m.Work.Width, WH = m.Work.Height, Internal = m.Internal,
    };
}

/// <summary>One saved arrangement of the desktop on one display setup.</summary>
public sealed class Snapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime SavedUtc { get; set; }
    /// <summary>user, manual, hotkey, auto, presave, shutdown, sleep, old</summary>
    public string Source { get; set; } = "user";
    public int SpacingW { get; set; }
    public int SpacingH { get; set; }
    public List<IconEntry> Icons { get; set; } = new();
}

/// <summary>Everything known about one display configuration: its monitors and its own save history (newest first).</summary>
public sealed class Profile
{
    public string Key { get; set; } = "";
    public string Display { get; set; } = "";
    public List<MonitorRec> Monitors { get; set; } = new();
    public List<Snapshot> History { get; set; } = new();
    /// <summary>Kullanıcının verdiği ad ("Ofis masası" gibi). Boşsa <see cref="AutoLabel"/> kullanılır.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    /// <summary>Adın verildiği an; eşitlemede yeni ad eskisini yener.</summary>
    public DateTime NameUtc { get; set; }

    [JsonIgnore] public Snapshot? Latest => History.Count > 0 ? History[0] : null;

    /// <summary>Listede görünen ad: kullanıcının adı → otomatik ad → eski ayrıntılı metin.</summary>
    [JsonIgnore] public string Title =>
        !string.IsNullOrWhiteSpace(Name) ? Name!.Trim() : AutoLabel() is string a ? a : Display.Length > 0 ? Display : Key;

    /// <summary>
    /// "Sadece dizüstü ekranı · 1920×1080 %125", "Dizüstü + harici monitör · …" gibi otomatik ad.
    /// Ekranların dahili/harici bilgisi yoksa (eski kayıt) null.
    /// </summary>
    public string? AutoLabel()
    {
        var mons = Monitors.Count > 0 ? Monitors : StoreData.MonitorsFromKey(Key);
        if (mons.Count == 0 || mons.Any(m => m.Internal == null)) return null;
        int i = mons.Count(m => m.Internal == true), e = mons.Count - i;
        string kind = (i, e) switch
        {
            ( > 0, 0) => Lang.T("dk_laptop_only"),
            (0, 1) => Lang.T("dk_ext_only"),
            (0, _) => Lang.F("dk_ext_n", e),
            (_, 1) => Lang.T("dk_laptop_ext"),
            _ => Lang.F("dk_laptop_ext_n", e),
        };
        var res = string.Join(" + ", mons.OrderByDescending(m => m.Internal == true).ThenBy(m => m.X)
            .Select(m => $"{m.W}×{m.H} %{m.Dpi * 100 / 96}"));
        return kind + "  ·  " + res;
    }
}

/// <summary>Old (v1.0.0 / v1.0.1) layout format, read only for migration.</summary>
public sealed class Layout
{
    public string ConfigKey { get; set; } = "";
    public string Display { get; set; } = "";
    public DateTime SavedUtc { get; set; }
    public List<IconEntry> Icons { get; set; } = new();
}

/// <summary>Geçmişten çıkan bir kayıt. v1.1.0'dan beri hiçbir kayıt kalıcı silinmez (ortak eşitleme kuralı):
/// silinen, sınırı aşan ya da eşitlemede çakışmayı kaybeden kayıt buraya taşınır; Ayarlar → Arşiv'den geri alınabilir.</summary>
public sealed class ArchiveEntry
{
    public string Key { get; set; } = "";
    public string Display { get; set; } = "";
    /// <summary>deleted | old | conflict</summary>
    public string Reason { get; set; } = "deleted";
    public DateTime ArchivedUtc { get; set; }
    /// <summary>Geri alındı (yeni kimlikle geçmişe kopyalandı); eşitlemede VEYA ile birleşir, kayıt yine silinmez.</summary>
    public bool Restored { get; set; }
    public Snapshot Snap { get; set; } = new();
}

public sealed class StoreData
{
    public const int MaxHistory = 25;
    /// <summary>Arşivde tutulan en çok kayıt; fazlası arsiv-tasma.jsonl dosyasına eklenir (yine silinmez).</summary>
    public const int MaxArchive = 500;

    public int Version { get; set; } = 2;
    public Dictionary<string, Profile> Displays { get; set; } = new();
    public string LastKey { get; set; } = "";
    public List<ArchiveEntry> Archive { get; set; } = new();

    // ---- legacy fields (v1.0.0 / v1.0.1) ----
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Layout? Master { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, Layout>? Profiles { get; set; }

    [JsonIgnore] public DateTime LatestUtc =>
        Displays.Values.Select(p => p.Latest?.SavedUtc ?? DateTime.MinValue).DefaultIfEmpty(DateTime.MinValue).Max();

    /// <summary>Adds a snapshot to the profile's history unless it is the same as the newest one. Returns true if added.</summary>
    public bool Add(Profile shape, Snapshot snap)
    {
        if (!Displays.TryGetValue(shape.Key, out var p))
        {
            p = new Profile { Key = shape.Key, Display = shape.Display, Monitors = shape.Monitors };
            Displays[shape.Key] = p;
        }
        else
        {
            p.Display = shape.Display;
            if (shape.Monitors.Count > 0)
            {
                // dahili/harici bilgisi bu sefer okunamadıysa önceki bilgiyi koru
                foreach (var nm in shape.Monitors.Where(x => x.Internal == null))
                    nm.Internal = p.Monitors.FirstOrDefault(o => o.Model == nm.Model)?.Internal;
                p.Monitors = shape.Monitors;
            }
        }
        if (p.Latest is { } last && LayoutEngine.SameIcons(last, snap))
        {
            // Same arrangement again: only upgrade a weak source (e.g. auto) to a stronger one (manual).
            if (Rank(snap.Source) > Rank(last.Source)) last.Source = snap.Source;
            return false;
        }
        p.History.Insert(0, snap);
        Trim(p);
        return true;
    }

    static int Rank(string s) => s is "manual" or "hotkey" ? 2 : s == "user" ? 1 : 0;

    /// <summary>Geçmişi en yeni MaxHistory kayda indirir; taşan kayıtlar arşive gider (silinmez).</summary>
    public void Trim(Profile p)
    {
        var sorted = p.History.OrderByDescending(h => h.SavedUtc).ToList();
        foreach (var old in sorted.Skip(MaxHistory)) ToArchive(p, old, "old");
        p.History = sorted.Take(MaxHistory).ToList();
    }

    public void ToArchive(Profile p, Snapshot s, string reason)
    {
        if (Archive.Any(a => a.Snap.Id == s.Id)) return;
        Archive.Add(new ArchiveEntry { Key = p.Key, Display = p.Display, Reason = reason, ArchivedUtc = DateTime.UtcNow, Snap = s });
    }

    /// <summary>Kaydı geçmişten arşive taşır (kullanıcı "sil" dediğinde).</summary>
    public bool ArchiveSnapshot(string snapId, string reason = "deleted")
    {
        if (Find(snapId) is not { } f) return false;
        ToArchive(f.p, f.s, reason);
        f.p.History.Remove(f.s);
        if (f.p.History.Count == 0) Displays.Remove(f.p.Key);
        return true;
    }

    /// <summary>Arşivdeki kaydı yeni kimlikle geçmişe geri koyar; arşiv kaydı "geri alındı" olarak kalır.</summary>
    public Snapshot? RestoreFromArchive(string snapId)
    {
        var a = Archive.FirstOrDefault(x => x.Snap.Id == snapId);
        if (a == null) return null;
        var copy = System.Text.Json.JsonSerializer.Deserialize<Snapshot>(System.Text.Json.JsonSerializer.Serialize(a.Snap))!;
        copy.Id = Guid.NewGuid().ToString("N");
        if (!Displays.TryGetValue(a.Key, out var p))
        {
            p = new Profile { Key = a.Key, Display = a.Display, Monitors = MonitorsFromKey(a.Key) };
            Displays[a.Key] = p;
        }
        p.History.Add(copy);
        p.History = p.History.OrderByDescending(h => h.SavedUtc).ToList();
        a.Restored = true;
        Trim(p);
        return copy;
    }

    public (Profile p, Snapshot s)? Find(string snapId)
    {
        foreach (var p in Displays.Values)
            foreach (var s in p.History)
                if (s.Id == snapId) return (p, s);
        return null;
    }

    /// <summary>Converts the old single-master format. Every old record is kept as a history entry marked "old".</summary>
    public int MigrateLegacy()
    {
        int n = 0;
        var all = new List<Layout>();
        if (Profiles != null) all.AddRange(Profiles.Values);
        if (Master != null) all.Add(Master);
        foreach (var l in all.Where(l => l.Icons.Count > 0 && l.ConfigKey != ""))
        {
            var shape = new Profile { Key = l.ConfigKey, Display = l.Display, Monitors = MonitorsFromKey(l.ConfigKey) };
            var snap = new Snapshot { SavedUtc = l.SavedUtc, Source = "old", Icons = l.Icons };
            if (!Displays.TryGetValue(l.ConfigKey, out var ex) || ex.History.All(h => !LayoutEngine.SameIcons(h, snap)))
            {
                Add(shape, snap);
                n++;
            }
        }
        foreach (var p in Displays.Values.ToList()) Trim(p);
        Master = null;
        Profiles = null;
        Version = 2;
        return n;
    }

    static readonly Regex KeyPart = new(@"^(?<m>.*):(?<w>\d+)x(?<h>\d+)@(?<x>-?\d+),(?<y>-?\d+)~(?<d>\d+)(?<p>P?)$", RegexOptions.Compiled);

    /// <summary>Rebuilds monitor rectangles from a display key (old records stored nothing else).</summary>
    public static List<MonitorRec> MonitorsFromKey(string key)
    {
        var list = new List<MonitorRec>();
        foreach (var part in key.Split('|'))
        {
            var m = KeyPart.Match(part);
            if (!m.Success) continue;
            int I(string g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
            var r = new MonitorRec
            {
                Model = m.Groups["m"].Value, Primary = m.Groups["p"].Value == "P", Dpi = I("d"),
                X = I("x"), Y = I("y"), W = I("w"), H = I("h"),
            };
            // Unknown work area: assume a bottom taskbar on the main screen.
            r.WX = r.X; r.WY = r.Y; r.WW = r.W;
            r.WH = r.Primary ? r.H - (int)Math.Round(48 * r.Dpi / 96.0) : r.H;
            list.Add(r);
        }
        return list;
    }
}

public sealed class Settings
{
    public string Lang { get; set; } = "tr";
    public bool Protect { get; set; } = true;
    public int Mode { get; set; } = 0; // unused since v1.0.2 (kept so old settings files load)
    public bool StartWithWindows { get; set; } = true;
    public bool DisableAutoArrange { get; set; } = true;
    public bool AutoSave { get; set; } = true;
    public string? TokenEnc { get; set; }
    public string? GistId { get; set; }
    public bool TrayHintShown { get; set; }

    // ---- ortak çekirdek (v1.1.0) ----
    /// <summary>Renk şeması: falcon (varsayılan) | klasik (v1.0.x görünümü) | zumrut | gunbatimi | mor | okyanus | grafit | yuksek-kontrast</summary>
    public string Scheme { get; set; } = "falcon";
    /// <summary>dark | light | auto</summary>
    public string Theme { get; set; } = "dark";
    public double FontScale { get; set; } = 1.0;
    /// <summary>İlk açılış rehberi tamamlandı.</summary>
    public bool Onboarded { get; set; }
    /// <summary>"Neler yeni" penceresinin en son gösterildiği sürüm.</summary>
    public string? WhatsNewSeen { get; set; }
}

public static class Storage
{
    public static readonly string Dir =
        Environment.GetEnvironmentVariable("DDS_DATA_DIR") is { Length: > 0 } d ? d :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopDuzenSabitleyici");
    /// <summary>Ekran görüntüsü kipinde hiçbir şey diske yazılmaz.</summary>
    public static bool ReadOnly { get; set; }
    static readonly string StorePath = Path.Combine(Dir, "layouts.json");
    static readonly string SettingsPath = Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    /// <summary>Number of old records converted on this start (0 if none).</summary>
    public static int Migrated { get; private set; }

    public static StoreData LoadStore()
    {
        var s = Load<StoreData>(StorePath);
        if (s == null)
        {
            // A broken main file: fall back to the last good backup.
            s = Load<StoreData>(StorePath + ".bak") ?? new StoreData();
        }
        if (s.Master != null || s.Profiles != null || s.Version < 2)
        {
            try { if (File.Exists(StorePath)) File.Copy(StorePath, Path.Combine(Dir, "layouts.v1-backup.json"), true); } catch { }
            Migrated = s.MigrateLegacy();
            SaveStore(s);
        }
        return s;
    }

    public static void SaveStore(StoreData s)
    {
        SpillArchive(s);
        Save(StorePath, s, keepBackup: true);
    }

    /// <summary>Arşiv MaxArchive'ı aşarsa en eski arşiv kayıtları arsiv-tasma.jsonl dosyasına eklenir (silinmez).</summary>
    static void SpillArchive(StoreData s)
    {
        if (s.Archive.Count <= StoreData.MaxArchive || ReadOnly) return;
        try
        {
            var spill = s.Archive.OrderBy(a => a.ArchivedUtc).Take(s.Archive.Count - StoreData.MaxArchive).ToList();
            Directory.CreateDirectory(Dir);
            File.AppendAllLines(Path.Combine(Dir, "arsiv-tasma.jsonl"), spill.Select(a => JsonSerializer.Serialize(a)));
            foreach (var a in spill) s.Archive.Remove(a);
        }
        catch { }
    }
    /// <summary>Bu açılışta ayar dosyası zaten vardı (false = ilk kurulum → ilk açılış rehberi).</summary>
    public static bool SettingsExisted { get; private set; }
    public static Settings LoadSettings()
    {
        SettingsExisted = File.Exists(SettingsPath);
        return Load<Settings>(SettingsPath) ?? new Settings();
    }
    public static void SaveSettings(Settings s) => Save(SettingsPath, s, keepBackup: false);

    public static string Serialize(StoreData s) => JsonSerializer.Serialize(s, Opt);
    public static StoreData? Deserialize(string json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<StoreData>(json);
            if (s != null && (s.Master != null || s.Profiles != null || s.Version < 2)) s.MigrateLegacy();
            return s;
        }
        catch { return null; }
    }

    static T? Load<T>(string path) where T : class
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null; }
        catch { return null; }
    }

    static void Save<T>(string path, T obj, bool keepBackup)
    {
        if (ReadOnly) return;
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(obj, Opt));
            if (keepBackup && File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(tmp, path, true);
        }
        catch { }
    }
}
