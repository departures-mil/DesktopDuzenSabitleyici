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

    [JsonIgnore] public Rectangle Bounds => new(X, Y, W, H);
    [JsonIgnore] public Rectangle Work => WW > 0 && WH > 0 ? new(WX, WY, WW, WH) : Bounds;

    public static MonitorRec From(MonitorInfo m) => new()
    {
        Model = m.Model, Primary = m.Primary, Dpi = m.Dpi,
        X = m.Bounds.X, Y = m.Bounds.Y, W = m.Bounds.Width, H = m.Bounds.Height,
        WX = m.Work.X, WY = m.Work.Y, WW = m.Work.Width, WH = m.Work.Height,
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

    [JsonIgnore] public Snapshot? Latest => History.Count > 0 ? History[0] : null;
}

/// <summary>Old (v1.0.0 / v1.0.1) layout format, read only for migration.</summary>
public sealed class Layout
{
    public string ConfigKey { get; set; } = "";
    public string Display { get; set; } = "";
    public DateTime SavedUtc { get; set; }
    public List<IconEntry> Icons { get; set; } = new();
}

public sealed class StoreData
{
    public const int MaxHistory = 25;

    public int Version { get; set; } = 2;
    public Dictionary<string, Profile> Displays { get; set; } = new();
    public string LastKey { get; set; } = "";

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
            if (shape.Monitors.Count > 0) p.Monitors = shape.Monitors;
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

    public static void Trim(Profile p)
    {
        p.History = p.History.OrderByDescending(h => h.SavedUtc).Take(MaxHistory).ToList();
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
        foreach (var p in Displays.Values) Trim(p);
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
}

public static class Storage
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopDuzenSabitleyici");
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

    public static void SaveStore(StoreData s) => Save(StorePath, s, keepBackup: true);
    public static Settings LoadSettings() => Load<Settings>(SettingsPath) ?? new Settings();
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
