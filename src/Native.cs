using System.Runtime.InteropServices;

namespace DDS.Native;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X, Y;
    public POINT(int x, int y) { X = x; Y = y; }
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;
    public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAY_DEVICE
{
    public int cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
    public int StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
}

// ---- Shell COM interfaces (only the slots we call are typed; the rest are placeholders keeping vtable order) ----

[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IShellWindows
{
    void _Count(); void _Item(); void _NewEnum(); void _Register(); void _RegisterPending();
    void _Revoke(); void _OnNavigate(); void _OnActivated();
    [PreserveSig]
    int FindWindowSW(ref object? pvarLoc, ref object? pvarLocRoot, int swClass, out int phwnd, int swfwOptions,
        [MarshalAs(UnmanagedType.IDispatch)] out object? ppdispOut);
}

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IComServiceProvider
{
    [PreserveSig]
    int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppvObject);
}

[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellBrowser
{
    void _GetWindow(); void _ContextSensitiveHelp(); void _InsertMenusSB(); void _SetMenuSB(); void _RemoveMenusSB();
    void _SetStatusTextSB(); void _EnableModelessSB(); void _TranslateAcceleratorSB(); void _BrowseObject();
    void _GetViewStateStream(); void _GetControlWindow(); void _SendControlMsg();
    [PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object? ppshv);
}

[ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellView
{
    [PreserveSig] int GetWindow(out IntPtr phwnd);
}

[ComImport, Guid("1af3a467-214f-4298-908e-06b03e0b39f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFolderView2
{
    // IFolderView
    [PreserveSig] int GetCurrentViewMode(out uint pViewMode);
    [PreserveSig] int SetCurrentViewMode(uint viewMode);
    [PreserveSig] int GetFolder(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppv);
    [PreserveSig] int Item(int iItemIndex, out IntPtr ppidl);
    [PreserveSig] int ItemCount(uint uFlags, out int pcItems);
    [PreserveSig] int Items(uint uFlags, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetSelectionMarkedItem(out int piItem);
    [PreserveSig] int GetFocusedItem(out int piItem);
    [PreserveSig] int GetItemPosition(IntPtr pidl, out POINT ppt);
    [PreserveSig] int GetSpacing(out POINT ppt);
    [PreserveSig] int GetDefaultSpacing(out POINT ppt);
    [PreserveSig] int GetAutoArrange();
    [PreserveSig] int SelectItem(int iItem, uint dwFlags);
    [PreserveSig]
    int SelectAndPositionItems(uint cidl,
        [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
        [In, MarshalAs(UnmanagedType.LPArray)] POINT[] apt, uint dwFlags);
    // IFolderView2
    void _SetGroupBy(); void _GetGroupBy(); void _SetViewProperty(); void _GetViewProperty();
    void _SetTileViewProperties(); void _SetExtendedTileViewProperties(); void _SetText();
    [PreserveSig] int SetCurrentFolderFlags(uint dwMask, uint dwFlags);
    [PreserveSig] int GetCurrentFolderFlags(out uint pdwFlags);
}

[ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellFolder
{
    void _ParseDisplayName(); void _EnumObjects(); void _BindToObject(); void _BindToStorage();
    void _CompareIDs(); void _CreateViewObject(); void _GetAttributesOf(); void _GetUIObjectOf();
    [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
}

[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellLinkW
{
    void _GetPath(); void _GetIDList(); void _SetIDList(); void _GetDescription();
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void _GetWorkingDirectory();
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void _GetArguments();
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void _GetHotkey(); void _SetHotkey(); void _GetShowCmd(); void _SetShowCmd(); void _GetIconLocation();
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void _SetRelativePath(); void _Resolve();
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

public static class Win32
{
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprc, IntPtr data);

    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern int StrRetToStrW(IntPtr pstr, IntPtr pidl, out IntPtr ppsz);
    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX mi);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    public static extern bool EnumDisplayDevices(string? device, uint devNum, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
