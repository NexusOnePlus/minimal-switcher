using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;

namespace minimal_switcher;

class KeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_TAB = 0x09;
    private const int VK_OEM_3 = 0xC0; // US: `
    private const int VK_OEM_5 = 0xDC; // Spanish: º/ª \

    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    private static bool _isAltDown = false;
    private static bool _isCustomAltTabActive = false;
    private static readonly SwitcherController Switcher = SwitcherController.Instance;

    public static void Start()
    {
        if (_hookID != IntPtr.Zero) return;
        RegistryHelper.EnsureSystemAltTabRestored();
        _hookID = SetHook(_proc);
    }

    public static void Stop()
    {
        ResetState();

        if (_hookID == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hookID);
        _hookID = IntPtr.Zero;
    }

    private static void ResetState()
    {
        if (_isCustomAltTabActive)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
                {
                    if (dispatcher.CheckAccess()) Switcher.Cancel();
                    else dispatcher.Invoke(Switcher.Cancel);
                }
            }
            catch
            {
                // Best effort cleanup during Windows logoff/process teardown.
            }
        }

        RegistryHelper.RestoreSystemAltTab();
        _isAltDown = false;
        _isCustomAltTabActive = false;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = hookStruct.vkCode;
            bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
            bool isKeyUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

            bool isKeyRepeat = isKeyDown && (hookStruct.flags & 0x4000) != 0;

            if (isKeyDown)
            {
                if (vkCode == VK_LMENU || vkCode == VK_RMENU)
                {
                    _isAltDown = true;
                }

                var isAltTab = vkCode == VK_TAB;
                var isSameProcessShortcut = (vkCode == VK_OEM_3 || vkCode == VK_OEM_5)
                    && AppSettingsService.Instance.Current.EnableSameProcessShortcut;

                if (_isAltDown && (isAltTab || isSameProcessShortcut) && !isKeyRepeat && !_isCustomAltTabActive)
                {
                    _isCustomAltTabActive = true;
                    var filter = isSameProcessShortcut ? SwitcherFilter.SameProcess : SwitcherFilter.AllWindows;
                    Debug.WriteLine($"[HOOK] Custom Alt sequence STARTED ({filter})");

                    var foregroundHwnd = NativeMethods.GetForegroundWindow();
                    Application.Current?.Dispatcher.Invoke(() => Switcher.Begin(foregroundHwnd, filter));
                    return (IntPtr)1;
                }

                if (_isCustomAltTabActive && (vkCode == VK_TAB || vkCode == VK_OEM_3 || vkCode == VK_OEM_5))
                {
                    Debug.WriteLine("[HOOK] Custom Alt+Tab NEXT");
                    Application.Current?.Dispatcher.Invoke(Switcher.Next);
                    return (IntPtr)1;
                }
            }
            else if (isKeyUp)
            {
                if (vkCode == VK_LMENU || vkCode == VK_RMENU)
                {
                    if (_isCustomAltTabActive)
                    {
                        Debug.WriteLine("[HOOK] Custom Alt+Tab Sequence ENDED");
                        Application.Current?.Dispatcher.Invoke(Switcher.Complete);
                        _isCustomAltTabActive = false;
                    }
                    _isAltDown = false;
                }
            }
        }

        if (_isCustomAltTabActive)
        {
            return (IntPtr)1;
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static bool IsCurrentProcessWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule? curModule = curProcess.MainModule)
        {
            if (curModule == null) throw new InvalidOperationException("Could not get the main module of the process.");
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    #region P/Invoke Declarations
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    #endregion
}
