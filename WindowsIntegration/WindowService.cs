using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace minimal_switcher;

public class WindowService
{
    static WindowService()
    {
        NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventProc, 0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        Debug.WriteLine("[HISTORY] WinEventHook for window history initialized.");
    }

    private static readonly List<IntPtr> _windowHistory = new List<IntPtr>();
    private static readonly IgnoredWindowRegistry IgnoredWindows = IgnoredWindowRegistry.Instance;
    private static readonly NativeMethods.WinEventDelegate _winEventProc = new NativeMethods.WinEventDelegate(WinEventProc);
    private static readonly Dictionary<string, IconAppearance> _iconCache = new();
    private static readonly Dictionary<IntPtr, (AppType Type, string Identifier)> _identifierCache = new();
    private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
        {
            UpdateWindowHistory(hwnd);
        }
    }

    public static void CleanupHistory()
    {
        int originalCount = _windowHistory.Count;
        _windowHistory.RemoveAll(hwnd => !NativeMethods.IsWindow(hwnd));
        IgnoredWindows.RemoveInvalidWindows();
        Debug.WriteLine($"[HISTORY] Cleanup completed. Removed {originalCount - _windowHistory.Count} invalid windows.");
    }

    public static IReadOnlyList<WindowItem> GetRecentWindows()
    {
        CleanupHistory();

        var recent = new List<WindowItem>();
        foreach (var hwnd in _windowHistory)
        {
            if (!IsAltTabWindow(hwnd)) continue;

            var item = CreateWindowItem(hwnd);
            if (item != null)
            {
                item.IsIgnored = IgnoredWindows.Contains(hwnd);
                recent.Add(item);
            }
        }

        return recent;
    }

    public static IReadOnlyList<WindowItem> GetIgnoredWindows()
    {
        CleanupHistory();

        var ignored = new List<WindowItem>();
        foreach (var hwnd in IgnoredWindows.GetAll())
        {
            var item = CreateWindowItem(hwnd);
            if (item != null)
            {
                item.IsIgnored = true;
                ignored.Add(item);
            }
        }

        return ignored;
    }

    public static void IgnoreWindow(IntPtr hwnd)
    {
        IgnoredWindows.Ignore(hwnd);
    }

    public static void RestoreWindow(IntPtr hwnd)
    {
        IgnoredWindows.Restore(hwnd);
    }

    private static void UpdateWindowHistory(IntPtr hwnd)
    {
        _identifierCache.Remove(hwnd);

        if (!IsAppWindow(hwnd)) return;

        GetAndCacheIdentifier(hwnd);
        var title = GetWindowTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title)) return;

        _windowHistory.Remove(hwnd);
        _windowHistory.Insert(0, hwnd);

        Debug.WriteLine($"[HISTORY] Active window: '{title}' (HWND: {hwnd}). History: {_windowHistory.Count} windows.");

        if (_windowHistory.Count > 20)
        {
            _windowHistory.RemoveRange(20, _windowHistory.Count - 20);
        }
    }

    public List<WindowItem> EnumerateWindowsWithIcons()
    {
        var allWindows = new List<WindowItem>();
        var handle = GCHandle.Alloc(allWindows);

        try
        {
            NativeMethods.EnumDesktopWindows(IntPtr.Zero, (IntPtr hWnd, ref GCHandle lParam) =>
            {
                if (!IsAltTabWindow(hWnd)) return true;
                if (IgnoredWindows.Contains(hWnd)) return true;
                
                var wi = CreateWindowItem(hWnd);
                if (wi != null && wi.Icon != null)
                {
                    Debug.WriteLine($"[ENUM] Window found: '{wi.Title}' (HWND: {hWnd})");
                    allWindows.Add(wi);
                }
                return true;
            }, ref handle);
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        return OrderWindowsByHistory(allWindows);
    
    }

    public IReadOnlyList<WindowItem> EnumerateWindowsForDebug()
    {
        var allWindows = new List<WindowItem>();
        var handle = GCHandle.Alloc(allWindows);

        try
        {
            NativeMethods.EnumDesktopWindows(IntPtr.Zero, (IntPtr hWnd, ref GCHandle lParam) =>
            {
                if (!IsAltTabWindow(hWnd)) return true;

                var item = CreateWindowItem(hWnd, processIcon: false);
                if (item != null)
                {
                    allWindows.Add(item);
                }

                return true;
            }, ref handle);
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }

        return OrderWindowsByHistory(allWindows);
    }

    private static WindowItem? CreateWindowItem(IntPtr hWnd, bool processIcon = true)
    {
        string title = GetWindowTitle(hWnd);
        if (string.IsNullOrWhiteSpace(title)) return null;

        var wi = new WindowItem { Hwnd = hWnd, Title = title };

        NativeMethods.GetWindowThreadProcessId(hWnd, out var framePid);
        wi.ProcessId = framePid;
        wi.RealProcessId = GetRealProcessId(hWnd);

        var (type, identifier) = GetAndCacheIdentifier(hWnd);
        if (!string.IsNullOrEmpty(identifier))
        {
            wi.Identifier = identifier;
            wi.AppName = GetDisplayName(identifier);

            var iconCacheKey = CreateIconCacheKey(identifier);
            if (processIcon && _iconCache.TryGetValue(iconCacheKey, out var cachedIcon))
            {
                wi.Icon = cachedIcon.Source;
                wi.IconSize = cachedIcon.Size;
            }
            else if (!NativeMethods.IsIconic(hWnd))
            {
                string? appTitle = null;
                var newIcon = type == AppType.Aumid
                    ? GetIconFromAumid(identifier, out appTitle)
                    : GetIconFromPath(identifier);

                if (!string.IsNullOrWhiteSpace(appTitle))
                {
                    wi.AppName = appTitle;
                }

                if (newIcon != null)
                {
                    if (processIcon)
                    {
                        var processedIcon = IconAppearanceService.Apply(newIcon);
                        wi.Icon = processedIcon.Source;
                        wi.IconSize = processedIcon.Size;
                        _iconCache[iconCacheKey] = processedIcon;
                    }
                    else
                    {
                        wi.Icon = newIcon;
                    }
                }
            }
        }

        if (wi.Icon == null)
        {
            var systemIcon = GetSystemIcon(hWnd);
            if (processIcon)
            {
                var fallbackIcon = IconAppearanceService.Apply(systemIcon);
                wi.Icon = fallbackIcon.Source;
                wi.IconSize = fallbackIcon.Size;
            }
            else
            {
                wi.Icon = systemIcon;
            }
        }
        return wi;
    }

    private static string CreateIconCacheKey(string identifier)
    {
        return $"{identifier}|{IconAppearanceService.SettingsKey}";
    }

    private static string GetDisplayName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return "App";

        if (Path.IsPathRooted(identifier))
        {
            return Path.GetFileNameWithoutExtension(identifier);
        }

        var parts = identifier.Split('!', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : identifier;
    }


    private static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (NativeMethods.GetWindowTextLength(hWnd) == 0) return false;

        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return false;

        long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;

        int cloaked = 0;
        NativeMethods.DwmGetWindowAttribute(hWnd, (int)NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, ref cloaked, Marshal.SizeOf<int>());
        if (cloaked != 0) return false;

        if (!NativeMethods.IsWindowVisible(hWnd) && !NativeMethods.IsIconic(hWnd))
        {
            return false;
        }

        return true;
    }

    private static (AppType Type, string? Identifier) GetAndCacheIdentifier(IntPtr hWnd)
    {
        if (_identifierCache.TryGetValue(hWnd, out var cachedIdentifier))
        {
            return cachedIdentifier;
        }

        var (type, identifier) = FindIdentifierForWindow(hWnd);

        if (!string.IsNullOrEmpty(identifier) && !identifier.EndsWith("ApplicationFrameHost.exe"))
        {
            _identifierCache[hWnd] = (type, identifier);
            return (type, identifier);
        }

        return (type, identifier);
    }

    public static IntPtr GetLastActiveWindow()
    {
        CleanupHistory();

        if (_windowHistory.Count > 1)
        {
            Debug.WriteLine($"[HISTORY] Returning last active window: {GetWindowTitle(_windowHistory[1])}");
            return _windowHistory[1];
        }

        Debug.WriteLine("[HISTORY] No last active window found.");
        return IntPtr.Zero;
    }
    private List<WindowItem> OrderWindowsByHistory(List<WindowItem> windows)
    {
        var orderedWindows = new List<WindowItem>();
        var usedHandles = new HashSet<IntPtr>();

        foreach (var hwnd in _windowHistory)
        {
            var window = windows.FirstOrDefault(w => w.Hwnd == hwnd);
            if (window != null && !usedHandles.Contains(hwnd))
            {
                if (NativeMethods.IsWindow(hwnd))
                {
                    orderedWindows.Add(window);
                    usedHandles.Add(hwnd);
                }
            }
        }

        var remainingWindows = windows.Where(w => !usedHandles.Contains(w.Hwnd)).ToList();
        orderedWindows.AddRange(remainingWindows);

        Debug.WriteLine($"[ORDER] Ordering completed. {orderedWindows.Count} total windows ({usedHandles.Count} from history, {remainingWindows.Count} remaining).");

        return orderedWindows;
    }




    private static (AppType Type, string? Identifier) FindIdentifierForWindow(IntPtr hWnd)
    {
        Debug.WriteLine($"[GetIdentifier] Searching for identifier for HWND: {hWnd}");

        try
        {
            uint pid = GetRealProcessId(hWnd);
            if (pid == 0)
            {
                Debug.WriteLine($"[GetIdentifier] Could not get PID for HWND: {hWnd}.");
                return (AppType.Path, null);
            }

            using (var handle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.QueryLimitedInformation, false, (int)pid))
            {
                if (handle.IsInvalid)
                {
                    Debug.WriteLine($"[GetIdentifier] OpenProcess failed for PID: {pid}. Attempting to get executable path.");
                    return (AppType.Path, GetPathForDesktopApp(pid));
                }

                int len = 1024;
                var sb = new StringBuilder(len);
                if (NativeMethods.GetApplicationUserModelId(handle.DangerousGetHandle(), ref len, sb) == 0)
                {
                    var aumid = sb.ToString();
                    Debug.WriteLine($"[GetIdentifier] Successfully obtained AUMID: '{aumid}' for PID: {pid}");
                    return (AppType.Aumid, aumid);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetIdentifier] Exception while getting AUMID: {ex.Message}. Will use executable path.");
        }

        uint finalPid = GetRealProcessId(hWnd);
        return (AppType.Path, GetPathForDesktopApp(finalPid));
    }

    private static string? GetPathForDesktopApp(uint pid)
    {
        if (pid == 0) return null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetPathForDesktopApp] Could not get path for PID {pid}: {ex.Message}");
            return null;
        }
    }

    private static uint GetRealProcessId(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint initialPid);
        if (initialPid == 0) return 0;

        try
        {
            using var initialProc = Process.GetProcessById((int)initialPid);
            if (initialProc.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[GetRealProcessId] HWND {hWnd} belongs to ApplicationFrameHost (PID: {initialPid}). Searching for child process...");
                foreach (var childHwnd in GetChildWindows(hWnd))
                {
                    NativeMethods.GetWindowThreadProcessId(childHwnd, out uint childPid);
                    if (childPid != 0 && childPid != initialPid)
                    {
                        Debug.WriteLine($"[GetRealProcessId] UWP child process found with PID: {childPid}");
                        return childPid;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetRealProcessId] Exception while checking ApplicationFrameHost: {ex.Message}");
        }

        return initialPid;
    }

    private static List<IntPtr> GetChildWindows(IntPtr parent)
    {
        var result = new List<IntPtr>();
        var listHandle = GCHandle.Alloc(result);
        try
        {
            NativeMethods.EnumChildWindows(parent, (hWnd, lParam) =>
            {
                if (GCHandle.FromIntPtr(lParam).Target is List<IntPtr> list) list.Add(hWnd);
                return true;
            }, GCHandle.ToIntPtr(listHandle));
        }
        finally
        {
            if (listHandle.IsAllocated) listHandle.Free();
        }
        return result;
    }

    public static bool IsAppWindow(IntPtr hWnd)
    {
        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return false;

        long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;

        if (NativeMethods.GetWindowTextLength(hWnd) == 0) return false;

        return true;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int len = NativeMethods.GetWindowTextLength(hWnd);
        if (len == 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    internal static BitmapSource? GetIconFromPathForDebug(string path)
    {
        return GetIconFromPath(path);
    }

    private static ImageSource? GetIconFromAumid(string aumid, out string? title)
    {
        title = null;
        Debug.WriteLine($"[GetIconFromAumid] Attempting to get icon for AUMID: {aumid}");

        NativeMethods.IShellItem2? shellItem = null;
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            int hr = NativeMethods.SHCreateItemInKnownFolder(NativeMethods.AppsFolder, 0, aumid,
                typeof(NativeMethods.IShellItem2).GUID, out shellItem);

            if (hr != 0 || shellItem == null)
            {
                Debug.WriteLine($"[GetIconFromAumid] SHCreateItemInKnownFolder failed with HRESULT: {hr:X}");
                return null;
            }

            var pkey = NativeMethods.PKEY_ItemNameDisplay;
            if (shellItem.GetString(ref pkey, out string displayName) == 0)
                title = displayName;

            var imageFactory = (NativeMethods.IShellItemImageFactory)shellItem;
            var size = new NativeMethods.SIZE { cx = 256, cy = 256 };
            hr = imageFactory.GetImage(size, NativeMethods.SIIGBF.ICONONLY, out hBitmap);

            if (hr == 0 && hBitmap != IntPtr.Zero)
            {
                Debug.WriteLine($"[GetIconFromAumid] Icon successfully obtained for AUMID: {aumid}");
                var bmp = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            else
            {
                Debug.WriteLine($"[GetIconFromAumid] imageFactory.GetImage failed with HRESULT: {hr:X}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetIconFromAumid] Exception: {ex.Message}");
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
            if (shellItem != null) Marshal.ReleaseComObject(shellItem);
        }
        return null;
    }

    private  static BitmapSource? GetIconFromPath(string path, int size = 256)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        Debug.WriteLine($"[GetIconFromPath] Attempting to get icon for path: {path}");
        IntPtr hBitmap = IntPtr.Zero;
        object? shellItem = null;
        try
        {
            Guid iid = typeof(NativeMethods.IShellItem2).GUID;
            int hr = NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out shellItem);

            if (hr == 0 && shellItem is NativeMethods.IShellItemImageFactory factory)
            {
                var sz = new NativeMethods.SIZE { cx = size, cy = size };
                if (factory.GetImage(sz, NativeMethods.SIIGBF.ICONONLY, out hBitmap) == 0 && hBitmap != IntPtr.Zero)
                {
                    var bmp = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetIconFromPath] Exception: {ex.Message}");
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
            if (shellItem != null) Marshal.ReleaseComObject(shellItem);
        }
        return null;
    }

    private static BitmapSource? GetSystemIcon(IntPtr hWnd)
    {
        IntPtr hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_BIG, IntPtr.Zero);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_SMALL2, IntPtr.Zero);
        }

        if (hIcon != IntPtr.Zero)
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        return null;
    }
}
