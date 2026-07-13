using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
using PodRelay.Core.Connection;

namespace PodRelay.App;

public partial class RelayPopupWindow : Window
{
    private const double DefaultPopupWidth = 840;
    private CancellationTokenSource? autoCloseCancellation;

    public RelayPopupWindow()
    {
        InitializeComponent();
        Opacity = 0;
    }

    public event EventHandler? ConnectRequested;
    public event EventHandler? CancelRequested;

    public void ShowNearby(string deviceName)
    {
        ShowState(deviceName, "AirPods 已在附近", "按 Y 或确认键连接到这台电脑", canConnect: true, isBusy: false);
        ScheduleClose(TimeSpan.FromSeconds(10));
    }

    public void ShowConnecting(string deviceName, bool automatic)
    {
        ShowState(
            deviceName,
            automatic ? "正在自动连接…" : "正在连接…",
            "正在请求 Windows 激活 AirPods 立体声音频",
            canConnect: false,
            isBusy: true);
    }

    public void ShowResult(string deviceName, EnsureConnectionResult result)
    {
        var title = result.State switch
        {
            ConnectionState.Connected => "已连接，声音已切换",
            ConnectionState.BluetoothOff => "Windows 蓝牙已关闭",
            ConnectionState.DeviceUnavailable => "未找到 AirPods",
            ConnectionState.AudioNotReady => "蓝牙已连接，音频未就绪",
            ConnectionState.TimedOut => "连接超时，可重试",
            ConnectionState.Cancelled => "已取消",
            _ => "连接失败"
        };
        ShowState(deviceName, title, result.Message, canConnect: !result.IsSuccess, isBusy: false);
        ScheduleClose(result.IsSuccess ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(8));
    }

    public void ClosePopup()
    {
        autoCloseCancellation?.Cancel();
        Hide();
    }

    private void ShowState(string deviceName, string title, string message, bool canConnect, bool isBusy)
    {
        var foregroundMonitor = MonitorFromWindow(GetForegroundWindow(), MonitorDefaultToNearest);
        autoCloseCancellation?.Cancel();
        TitleText.Text = title;
        MessageText.Text = $"{deviceName}\n{message}";
        ConnectButton.Visibility = canConnect ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Content = isBusy ? "B  取消" : "B  忽略";
        Progress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        if (!IsVisible)
        {
            Show();
        }

        PositionWindow(foregroundMonitor);
        Activate();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        if (canConnect)
        {
            ConnectButton.Focus();
        }
    }

    private void PositionWindow(IntPtr preferredMonitor)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var monitor = preferredMonitor != IntPtr.Zero
            ? preferredMonitor
            : MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            var fallback = SystemParameters.WorkArea;
            Left = fallback.Left + (fallback.Width - Width) / 2;
            Top = fallback.Top + Math.Max(36, fallback.Height * 0.08);
            return;
        }

        var source = HwndSource.FromHwnd(handle);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = fromDevice.Transform(new System.Windows.Point(info.WorkArea.Left, info.WorkArea.Top));
        var bottomRight = fromDevice.Transform(new System.Windows.Point(info.WorkArea.Right, info.WorkArea.Bottom));
        var workWidth = bottomRight.X - topLeft.X;
        var workHeight = bottomRight.Y - topLeft.Y;
        Width = Math.Min(DefaultPopupWidth, Math.Max(320, workWidth - 32));
        var contentScale = Math.Min(1, Width / DefaultPopupWidth);
        RootBorder.LayoutTransform = new System.Windows.Media.ScaleTransform(contentScale, contentScale);
        Left = topLeft.X + (workWidth - Width) / 2;
        var desiredTop = topLeft.Y + Math.Max(20, workHeight * 0.08);
        Top = Math.Max(topLeft.Y + 12, Math.Min(desiredTop, bottomRight.Y - Height - 12));
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect WorkMonitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private async void ScheduleClose(TimeSpan delay)
    {
        autoCloseCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, autoCloseCancellation.Token);
            Hide();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnConnect(object sender, RoutedEventArgs e) => ConnectRequested?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        ClosePopup();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Y or Key.Enter or Key.Space)
        {
            ConnectRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key is Key.B or Key.Escape or Key.Back)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            ClosePopup();
            e.Handled = true;
        }
    }
}
