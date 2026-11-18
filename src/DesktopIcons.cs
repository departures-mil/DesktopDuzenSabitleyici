using System.Runtime.InteropServices;
using DDS.Native;

namespace DDS;

/// <summary>Icon position in physical screen coordinates.</summary>
public sealed record IconPos(string Key, int X, int Y);

/// <summary>Reads and writes desktop icon positions through the shell's IFolderView (the same API Explorer uses).</summary>
public static class DesktopIcons
{
    static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");

    const int CSIDL_DESKTOP = 0, SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1;
    const uint SVGIO_ALLVIEW = 2, SVSI_POSITIONITEM = 0x80, SHGDN_FORPARSING = 0x8000, FWF_AUTOARRANGE = 0x1;

    /// <summary>Window handle of the desktop view seen on the last call (changes when Explorer restarts).</summary>
    public static IntPtr LastViewHwnd { get; private set; }
    public static Size LastSpacing { get; private set; } = new(100, 100);

    sealed class Ctx : IDisposable
    {
        public IFolderView2 View = null!;
        public IShellFolder Folder = null!;
        public POINT Origin;
        public readonly List<object> Com = new();
        public void Dispose()
        {
            foreach (var o in Com)
                try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { }
        }
    }

    static Ctx Open()
    {
        var c = new Ctx();
        try
        {
            var t = Type.GetTypeFromCLSID(CLSID_ShellWindows, true)!;
            var swObj = Activator.CreateInstance(t)!;
            c.Com.Add(swObj);
            var sw = (IShellWindows)swObj;

            object? loc = CSIDL_DESKTOP;
            object? root = null;
            int hr = sw.FindWindowSW(ref loc, ref root, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH, out object? disp);
            if (hr != 0 || disp == null) throw new COMException("Desktop window not found", hr == 0 ? -1 : hr);
            c.Com.Add(disp);

            var sp = (IComServiceProvider)disp;
            Guid sid = SID_STopLevelBrowser, iid = IID_IShellBrowser;
            Marshal.ThrowExceptionForHR(sp.QueryService(ref sid, ref iid, out object? browserObj));
            c.Com.Add(browserObj!);

            Marshal.ThrowExceptionForHR(((IShellBrowser)browserObj!).QueryActiveShellView(out object? viewObj));
            c.Com.Add(viewObj!);

            ((IShellView)viewObj!).GetWindow(out IntPtr defView);
            c.View = (IFolderView2)viewObj;

            Guid iidSf = IID_IShellFolder;
            Marshal.ThrowExceptionForHR(c.View.GetFolder(ref iidSf, out object? folderObj));
            c.Com.Add(folderObj!);
            c.Folder = (IShellFolder)folderObj!;

            var list = Win32.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            var hwnd = list != IntPtr.Zero ? list : defView;
            var org = new POINT(0, 0);
            Win32.ClientToScreen(hwnd, ref org);
            c.Origin = org;
            LastViewHwnd = defView;

            if (c.View.GetSpacing(out POINT spc) == 0 && spc.X > 10 && spc.Y > 10)
                LastSpacing = new Size(spc.X, spc.Y);
            return c;
        }
        catch
        {
            c.Dispose();
            throw;
        }
    }

    static string? NameOf(IShellFolder folder, IntPtr pidl)
    {
        IntPtr strret = Marshal.AllocCoTaskMem(1024);
        try
        {
            for (int i = 0; i < 1024; i += 4) Marshal.WriteInt32(strret, i, 0);
            if (folder.GetDisplayNameOf(pidl, SHGDN_FORPARSING, strret) != 0) return null;
            if (Win32.StrRetToStrW(strret, pidl, out IntPtr psz) != 0 || psz == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(psz); }
            finally { Marshal.FreeCoTaskMem(psz); }
        }
        finally { Marshal.FreeCoTaskMem(strret); }
    }

    public static List<IconPos> Read()
    {
        using var c = Open();
        var result = new List<IconPos>();
        if (c.View.ItemCount(SVGIO_ALLVIEW, out int n) != 0) return result;
        for (int i = 0; i < n; i++)
        {
            if (c.View.Item(i, out IntPtr pidl) != 0 || pidl == IntPtr.Zero) continue;
            try
            {
                var name = NameOf(c.Folder, pidl);
                if (string.IsNullOrEmpty(name)) continue;
                if (c.View.GetItemPosition(pidl, out POINT pt) != 0) continue;
                result.Add(new IconPos(name, pt.X + c.Origin.X, pt.Y + c.Origin.Y));
            }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        return result;
    }

    /// <summary>Moves the given icons to the given screen positions. Returns how many were positioned.</summary>
    public static int Write(IReadOnlyCollection<IconPos> targets, bool disableAutoArrange)
    {
        if (targets.Count == 0) return 0;
        using var c = Open();
        if (disableAutoArrange && c.View.GetAutoArrange() == 0)
            c.View.SetCurrentFolderFlags(FWF_AUTOARRANGE, 0);

        var wanted = new Dictionary<string, IconPos>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targets) wanted[t.Key] = t;

        var pidls = new List<IntPtr>();
        var pts = new List<POINT>();
        var toFree = new List<IntPtr>();
        try
        {
            if (c.View.ItemCount(SVGIO_ALLVIEW, out int n) != 0) return 0;
            for (int i = 0; i < n; i++)
            {
                if (c.View.Item(i, out IntPtr pidl) != 0 || pidl == IntPtr.Zero) continue;
                toFree.Add(pidl);
                var name = NameOf(c.Folder, pidl);
                if (name == null || !wanted.TryGetValue(name, out var t)) continue;
                pidls.Add(pidl);
                pts.Add(new POINT(t.X - c.Origin.X, t.Y - c.Origin.Y));
            }
            // Position one by one so that every icon reaches its own cell (more reliable with "align to grid").
            for (int i = 0; i < pidls.Count; i++)
                c.View.SelectAndPositionItems(1, new[] { pidls[i] }, new[] { pts[i] }, SVSI_POSITIONITEM);
            return pidls.Count;
        }
        finally
        {
            foreach (var p in toFree) Marshal.FreeCoTaskMem(p);
        }
    }
}
