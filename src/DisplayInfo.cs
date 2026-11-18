using DDS.Native;

namespace DDS;

public sealed record MonitorInfo(string Device, string Model, Rectangle Bounds, Rectangle Work, bool Primary, int Dpi);

/// <summary>Enumerates monitors directly through Win32 so results are always fresh (no caching).</summary>
public static class DisplayInfo
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        Win32.MonitorEnumProc proc = (h, _, _, _) =>
        {
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (Win32.GetMonitorInfo(h, ref mi))
            {
                int dpi = 96;
                try { if (Win32.GetDpiForMonitor(h, 0, out uint dx, out _) == 0 && dx > 0) dpi = (int)dx; } catch { }
                list.Add(new MonitorInfo(mi.szDevice, ModelOf(mi.szDevice), mi.rcMonitor.ToRectangle(),
                    mi.rcWork.ToRectangle(), (mi.dwFlags & 1) != 0, dpi));
            }
            return true;
        };
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);
        return list.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();
    }

    /// <summary>Returns the physical monitor model code (e.g. "BOE0A1C") so two different monitors never look the same.</summary>
    static string ModelOf(string device)
    {
        try
        {
            var dd = new DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<DISPLAY_DEVICE>() };
            for (uint i = 0; Win32.EnumDisplayDevices(device, i, ref dd, 0); i++)
            {
                if ((dd.StateFlags & 0x1) != 0 || i == 0) // attached / first
                {
                    var id = dd.DeviceID ?? "";
                    var parts = id.Split('\\', '#');
                    if (parts.Length > 1 && parts[1].Length > 0) return parts[1];
                }
                dd.cb = System.Runtime.InteropServices.Marshal.SizeOf<DISPLAY_DEVICE>();
            }
        }
        catch { }
        return device.Replace("\\\\.\\", "");
    }

    public static string KeyOf(List<MonitorInfo> mons) =>
        string.Join("|", mons.Select(m =>
            $"{m.Model}:{m.Bounds.Width}x{m.Bounds.Height}@{m.Bounds.X},{m.Bounds.Y}~{m.Dpi}{(m.Primary ? "P" : "")}"));

    public static string CurrentKey() => KeyOf(GetMonitors());

    public static string Friendly(List<MonitorInfo> mons, string primaryWord) =>
        string.Join("  +  ", mons.Select(m =>
            $"{m.Bounds.Width}×{m.Bounds.Height}  %{m.Dpi * 100 / 96}  {m.Model}{(m.Primary ? $" ({primaryWord})" : "")}"));

    public static MonitorInfo For(List<MonitorInfo> mons, int x, int y)
    {
        foreach (var m in mons) if (m.Bounds.Contains(x, y)) return m;
        MonitorInfo best = mons[0];
        long bestD = long.MaxValue;
        foreach (var m in mons)
        {
            long dx = Math.Max(0, Math.Max(m.Bounds.Left - x, x - m.Bounds.Right));
            long dy = Math.Max(0, Math.Max(m.Bounds.Top - y, y - m.Bounds.Bottom));
            long d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = m; }
        }
        return best;
    }
}
