using System.Text.Json;
using System.Text.Json.Nodes;

namespace DDS.Core;

/// <summary>Çekirdeğin küçük durum dosyası (%APPDATA%\DesktopDuzenSabitleyici\cekirdek.json): cihaz kimliği,
/// gönderilemeyen mesaj kuyruğu, çökme sayaçları, bekleyen sürüm notları. Atomik yazma.</summary>
public static class CoreState
{
    static readonly object Gate = new();
    static JsonObject? data;
    /// <summary>Test / ekran görüntüsü için değiştirilebilir; null = yalnız bellekte.</summary>
    public static string? FilePath { get; set; } = Path.Combine(Storage.Dir, "cekirdek.json");

    static JsonObject D()
    {
        if (data != null) return data;
        try
        {
            if (FilePath != null && File.Exists(FilePath))
                data = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject;
        }
        catch { }
        return data ??= new JsonObject();
    }

    public static JsonNode? Get(string key) { lock (Gate) return D()[key]?.DeepClone(); }

    public static T? Get<T>(string key, T? def = default)
    {
        lock (Gate)
        {
            try { var n = D()[key]; return n == null ? def : n.Deserialize<T>() ?? def; }
            catch { return def; }
        }
    }

    public static void Set(string key, object? value)
    {
        lock (Gate)
        {
            D()[key] = value == null ? null : JsonSerializer.SerializeToNode(value);
            Save();
        }
    }

    public static void Reset() { lock (Gate) data = null; }

    static void Save()
    {
        if (FilePath == null || Storage.ReadOnly) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, D().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, true);
        }
        catch { }
    }

    public static string DeviceId()
    {
        var d = Get<string>("device");
        if (string.IsNullOrEmpty(d))
        {
            d = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9)).ToLowerInvariant();
            Set("device", d);
        }
        return d;
    }
}
