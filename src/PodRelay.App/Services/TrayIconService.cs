using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PodRelay.Core.Connection;

namespace PodRelay.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly Dictionary<ConnectionState, Icon> icons;

    public TrayIconService(
        Action ensureConnected,
        Action releaseAndCoolDown,
        Action showSettings,
        Action pauseOneHour,
        Action exit)
    {
        using var baseIcon = LoadBaseIcon();
        icons = Enum.GetValues<ConnectionState>()
            .ToDictionary(state => state, state => CreateIcon(baseIcon, GetColor(state)));
        var menu = new ContextMenuStrip();
        menu.Items.Add("确保 AirPods 已连接", null, (_, _) => ensureConnected());
        menu.Items.Add("释放给其他设备（30 分钟）", null, (_, _) => releaseAndCoolDown());
        menu.Items.Add("设置", null, (_, _) => showSettings());
        menu.Items.Add("暂停自动接力 1 小时", null, (_, _) => pauseOneHour());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "PodRelay — 等待 AirPods",
            Icon = icons[ConnectionState.Waiting],
            ContextMenuStrip = menu
        };
        notifyIcon.DoubleClick += (_, _) => ensureConnected();
    }

    public void SetState(ConnectionState state, string? detail = null)
    {
        notifyIcon.Icon = icons[state];
        var text = $"PodRelay — {FormatState(state)}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            text += $" — {detail}";
        }

        notifyIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        foreach (var icon in icons.Values)
        {
            icon.Dispose();
        }
    }

    private static string FormatState(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "已连接",
        ConnectionState.Connecting => "正在连接",
        ConnectionState.AudioNotReady => "音频未就绪",
        ConnectionState.BluetoothOff => "蓝牙已关闭",
        ConnectionState.DeviceUnavailable => "未发现",
        ConnectionState.Failed => "连接失败",
        ConnectionState.TimedOut => "连接超时",
        ConnectionState.CoolingDown => "自动接力已暂停",
        _ => "等待 AirPods"
    };

    private static Color GetColor(ConnectionState state) => state switch
    {
        ConnectionState.Connected => Color.FromArgb(70, 190, 120),
        ConnectionState.Connecting => Color.FromArgb(245, 180, 65),
        ConnectionState.AudioNotReady => Color.FromArgb(245, 180, 65),
        ConnectionState.Failed => Color.FromArgb(225, 75, 80),
        ConnectionState.TimedOut => Color.FromArgb(225, 75, 80),
        ConnectionState.BluetoothOff => Color.FromArgb(125, 125, 135),
        ConnectionState.DeviceUnavailable => Color.FromArgb(125, 125, 135),
        ConnectionState.CoolingDown => Color.FromArgb(125, 125, 135),
        _ => Color.FromArgb(95, 150, 245)
    };

    private static Bitmap LoadBaseIcon()
    {
        var resourceUri = new Uri(
            "pack://application:,,,/PodRelay;component/Assets/airpods-app.png",
            UriKind.Absolute);
        var resource = System.Windows.Application.GetResourceStream(resourceUri)
            ?? throw new InvalidOperationException("找不到托盘图标资源。");

        using (resource.Stream)
        using (var image = new Bitmap(resource.Stream))
        {
            return new Bitmap(image);
        }
    }

    private static Icon CreateIcon(Bitmap baseIcon, Color stateColor)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(baseIcon, 0, 0, 32, 32);

            // The AirPods remain the primary silhouette; the small badge preserves
            // the existing at-a-glance connection-state feedback in the tray.
            using var badgeBorder = new SolidBrush(Color.FromArgb(235, 10, 13, 19));
            using var badge = new SolidBrush(stateColor);
            graphics.FillEllipse(badgeBorder, 20, 20, 12, 12);
            graphics.FillEllipse(badge, 22, 22, 8, 8);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
