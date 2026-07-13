using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace PodRelay.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x504f44;
    private const int WmHotkey = 0x0312;
    private readonly HwndSource source;
    private bool registered;

    public GlobalHotkeyService()
    {
        source = new HwndSource(new HwndSourceParameters("PodRelay.Hotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        source.AddHook(WindowHook);
    }

    public event EventHandler? Pressed;

    public bool Register(string modifiersText, string keyText)
    {
        Unregister();
        if (!Enum.TryParse<Key>(keyText, ignoreCase: true, out var key) ||
            !Enum.IsDefined(key) ||
            key == Key.None)
        {
            return false;
        }

        if (!TryParseModifiers(modifiersText, out var modifiers))
        {
            return false;
        }

        registered = RegisterHotKey(
            source.Handle,
            HotkeyId,
            (uint)(modifiers | HotkeyModifiers.NoRepeat),
            (uint)KeyInterop.VirtualKeyFromKey(key));
        return registered;
    }

    public void Dispose()
    {
        Unregister();
        source.RemoveHook(WindowHook);
        source.Dispose();
    }

    private void Unregister()
    {
        if (registered)
        {
            UnregisterHotKey(source.Handle, HotkeyId);
            registered = false;
        }
    }

    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private static bool TryParseModifiers(string text, out HotkeyModifiers modifiers)
    {
        modifiers = HotkeyModifiers.None;
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            const HotkeyModifiers allowed = HotkeyModifiers.Alt |
                HotkeyModifiers.Control |
                HotkeyModifiers.Shift |
                HotkeyModifiers.Windows;
            if (!Enum.TryParse<HotkeyModifiers>(token, ignoreCase: true, out var value) ||
                (value & ~allowed) != 0)
            {
                modifiers = HotkeyModifiers.None;
                return false;
            }

            modifiers |= value;
        }

        return true;
    }

    [Flags]
    private enum HotkeyModifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Windows = 0x0008,
        NoRepeat = 0x4000
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
