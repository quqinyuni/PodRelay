using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PodRelay.App.Services;

public sealed record MediaSessionCommandResult(
    bool Executed,
    string Reason,
    string? SourceAppUserModelId = null);

public sealed class SystemMediaSessionService
{
    private const uint WmAppCommand = 0x0319;
    private const int AppCommandMediaPlay = 46;
    private const int AppCommandMediaPause = 47;
    private const uint SmtoAbortIfHung = 0x0002;
    private IntPtr pausedTargetWindow;

    public Task<MediaSessionCommandResult> PauseCurrentAsync()
    {
        var target = GetForegroundWindow();
        var result = SendMediaCommand(target, AppCommandMediaPause, "暂停");
        pausedTargetWindow = result.Executed ? target : IntPtr.Zero;
        return Task.FromResult(result);
    }

    public Task<MediaSessionCommandResult> ResumePausedAsync()
    {
        if (pausedTargetWindow == IntPtr.Zero || !IsWindow(pausedTargetWindow))
        {
            pausedTargetWindow = IntPtr.Zero;
            return Task.FromResult(new MediaSessionCommandResult(
                false,
                "刚才接收暂停命令的窗口已经关闭，不自动恢复。"));
        }

        var target = pausedTargetWindow;
        pausedTargetWindow = IntPtr.Zero;
        return Task.FromResult(SendMediaCommand(target, AppCommandMediaPlay, "播放"));
    }

    public void Reset() => pausedTargetWindow = IntPtr.Zero;

    private static MediaSessionCommandResult SendMediaCommand(
        IntPtr target,
        int appCommand,
        string action)
    {
        if (target == IntPtr.Zero)
        {
            return new(false, $"没有可接收{action}命令的前台窗口。");
        }

        var lParam = new IntPtr(appCommand << 16);
        var delivered = SendMessageTimeout(
            target,
            WmAppCommand,
            target,
            lParam,
            SmtoAbortIfHung,
            1000,
            out _) != IntPtr.Zero;
        return delivered
            ? new(true, $"已向 Windows 发送独立的媒体{action}命令。")
            : new(false, $"媒体{action}命令未送达：{new Win32Exception(Marshal.GetLastWin32Error()).Message}");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
