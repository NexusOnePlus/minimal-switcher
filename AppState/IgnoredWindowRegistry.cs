using System;
using System.Collections.Generic;

namespace minimal_switcher;

public sealed class IgnoredWindowRegistry
{
    private readonly HashSet<IntPtr> _ignoredWindows = new();

    public static IgnoredWindowRegistry Instance { get; } = new();

    private IgnoredWindowRegistry()
    {
    }

    public bool Contains(IntPtr hwnd) => _ignoredWindows.Contains(hwnd);

    public IReadOnlyCollection<IntPtr> GetAll() => _ignoredWindows;

    public void Ignore(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
        {
            _ignoredWindows.Add(hwnd);
        }
    }

    public void Restore(IntPtr hwnd)
    {
        _ignoredWindows.Remove(hwnd);
    }

    public void RemoveInvalidWindows()
    {
        _ignoredWindows.RemoveWhere(hwnd => !NativeMethods.IsWindow(hwnd));
    }
}
