using System.Text.Json;

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

public sealed class Layout
{
    public string ConfigKey { get; set; } = "";
    public string Display { get; set; } = "";
    public DateTime SavedUtc { get; set; }
    public List<IconEntry> Icons { get; set; } = new();
}

public sealed class StoreData
{
    /// <summary>The arrangement the user made most recently (on any screen). Used to rebuild layouts on new screens.</summary>
    public Layout? Master { get; set; }
    /// <summary>Exact layout per display configuration.</summary>
    public Dictionary<string, Layout> Profiles { get; set; } = new();
    public string LastKey { get; set; } = "";
}

public sealed class Settings
{
    public string Lang { get; set; } = "tr";
    public bool Protect { get; set; } = true;
    /// <summary>0 = proportional, 1 = per-display profile.</summary>
    public int Mode { get; set; } = 0;
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

    public static StoreData LoadStore() => Load<StoreData>(StorePath) ?? new StoreData();
    public static void SaveStore(StoreData s) => Save(StorePath, s);
    public static Settings LoadSettings() => Load<Settings>(SettingsPath) ?? new Settings();
    public static void SaveSettings(Settings s) => Save(SettingsPath, s);

    public static string Serialize(StoreData s) => JsonSerializer.Serialize(s, Opt);
    public static StoreData? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<StoreData>(json); } catch { return null; }
    }

    static T? Load<T>(string path) where T : class
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null; }
        catch { return null; }
    }

    static void Save<T>(string path, T obj)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(obj, Opt));
            File.Move(tmp, path, true);
        }
        catch { }
    }
}
