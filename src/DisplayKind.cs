using System.Runtime.InteropServices;
using DDS.Native;

namespace DDS;

/// <summary>
/// Hangi ekranın dizüstünün kendi paneli (dahili), hangisinin harici monitör olduğunu bulur
/// (QueryDisplayConfig → çıkış teknolojisi). Ekran düzenlerine "Sadece dizüstü", "Dizüstü + harici monitör" gibi
/// anlaşılır adlar vermek için kullanılır. Hata olursa boş küme döner (adlar eski biçimde kalır).
/// </summary>
public static class DisplayKind
{
    const uint QDC_ONLY_ACTIVE_PATHS = 2;
    const int PathSize = 72, ModeSize = 64, SourceNameSize = 84;
    // DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY: LVDS, DISPLAYPORT_EMBEDDED, UDI_EMBEDDED, INTERNAL
    static readonly HashSet<uint> InternalTech = new() { 6, 11, 13, 0x80000000 };

    static HashSet<string>? cache;
    static DateTime cacheUtc;

    /// <summary>Ekran değişince önbelleği boşaltır.</summary>
    public static void Invalidate() => cache = null;

    /// <summary>Dahili panellerin GDI aygıt adları (ör. \\.\DISPLAY1). Bilinemiyorsa null.</summary>
    public static HashSet<string>? InternalDevices()
    {
        if (cache != null && (DateTime.UtcNow - cacheUtc).TotalSeconds < 10) return cache;
        HashSet<string>? r = null;
        try { r = Query(); } catch { r = null; }
        cache = r;
        cacheUtc = DateTime.UtcNow;
        return r;
    }

    static HashSet<string>? Query()
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (Win32.GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint np, out uint nm) != 0 || np == 0) return null;
        IntPtr paths = Marshal.AllocHGlobal((int)(np * PathSize)), modes = Marshal.AllocHGlobal((int)Math.Max(1, nm * ModeSize));
        IntPtr pkt = Marshal.AllocHGlobal(SourceNameSize);
        try
        {
            if (Win32.QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < np; i++)
            {
                IntPtr p = paths + i * PathSize;
                uint tech = (uint)Marshal.ReadInt32(p, 36); // targetInfo.outputTechnology
                if (!InternalTech.Contains(tech)) continue;
                // sourceInfo: LUID adapterId (0..8), id (8)
                for (int b = 0; b < SourceNameSize; b += 4) Marshal.WriteInt32(pkt, b, 0);
                Marshal.WriteInt32(pkt, 0, 1);                       // DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME
                Marshal.WriteInt32(pkt, 4, SourceNameSize);
                Marshal.WriteInt32(pkt, 8, Marshal.ReadInt32(p, 0));  // adapterId.LowPart
                Marshal.WriteInt32(pkt, 12, Marshal.ReadInt32(p, 4)); // adapterId.HighPart
                Marshal.WriteInt32(pkt, 16, Marshal.ReadInt32(p, 8)); // source id
                if (Win32.DisplayConfigGetDeviceInfo(pkt) != 0) continue;
                var name = Marshal.PtrToStringUni(pkt + 20, 32).TrimEnd('\0');
                if (name.Length > 0) set.Add(name);
            }
            return set;
        }
        finally
        {
            Marshal.FreeHGlobal(paths);
            Marshal.FreeHGlobal(modes);
            Marshal.FreeHGlobal(pkt);
        }
    }
}
