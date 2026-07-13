using System.Windows;
using PodRelay.App.Settings;
using PodRelay.Core.Audio;
using PodRelay.Core.Connection;
using PodRelay.Core.Devices;

namespace PodRelay.App;

public partial class MainWindow : Window
{
    private readonly WindowsDeviceDiscovery discovery;
    private readonly WindowsGameControllerWatcher gameControllerWatcher;
    private readonly HashSet<Guid> knownAudioContainers = [];
    private AppSettings settings;
    private bool allowClose;

    public MainWindow(
        AppSettings settings,
        WindowsDeviceDiscovery discovery,
        WindowsGameControllerWatcher gameControllerWatcher)
    {
        InitializeComponent();
        this.settings = settings;
        this.discovery = discovery;
        this.gameControllerWatcher = gameControllerWatcher;
        gameControllerWatcher.ControllerConnected += OnControllerListChanged;
        gameControllerWatcher.ControllerDisconnected += OnControllerListChanged;
        if (settings.TargetContainerId is Guid savedContainerId)
        {
            knownAudioContainers.Add(savedContainerId);
        }
        AutoRelayCheck.IsChecked = settings.AutoRelayEnabled;
        UnlockCheck.IsChecked = settings.ConnectOnUnlock;
        ReconnectCheck.IsChecked = settings.ReconnectOnDisconnect;
        CooldownText.Text = settings.CooldownMinutes.ToString();
        PopupCheck.IsChecked = settings.PopupEnabled;
        InEarMediaControlCheck.IsChecked = settings.InEarMediaControlEnabled;
        ControllerRelayCheck.IsChecked = settings.ConnectOnController;
        StartupCheck.IsChecked = settings.StartWithWindows;
        ModifiersText.Text = settings.HotkeyModifiers;
        HotkeyText.Text = settings.HotkeyKey;
        Loaded += async (_, _) =>
        {
            await RefreshDevicesAsync();
            RefreshControllers();
        };
        Closing += (_, args) =>
        {
            if (allowClose)
            {
                return;
            }

            args.Cancel = true;
            Hide();
        };
        Closed += (_, _) =>
        {
            gameControllerWatcher.ControllerConnected -= OnControllerListChanged;
            gameControllerWatcher.ControllerDisconnected -= OnControllerListChanged;
        };
    }

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler? TestConnectionRequested;
    public event EventHandler? PopupPreviewRequested;
    public event EventHandler? ExportDiagnosticsRequested;

    public void CloseForExit()
    {
        allowClose = true;
        Close();
    }

    public void SetConnectionState(ConnectionState state, string? detail = null)
    {
        var label = state switch
        {
            ConnectionState.Waiting => "可尝试连接",
            ConnectionState.BluetoothOff => "蓝牙已关闭",
            ConnectionState.DeviceUnavailable => "未配对或未发现",
            ConnectionState.Connecting => "正在连接",
            ConnectionState.AudioNotReady => "蓝牙已连接，音频未就绪",
            ConnectionState.Connected => "已连接且声音已切换",
            ConnectionState.CoolingDown => "自动接力冷却中",
            ConnectionState.TimedOut => "连接超时，可重试",
            ConnectionState.Failed => "连接失败",
            ConnectionState.Cancelled => "已取消",
            _ => state.ToString()
        };
        StatusText.Text = string.IsNullOrWhiteSpace(detail)
            ? $"当前状态：{label}"
            : $"当前状态：{label} — {detail}";
    }

    private async Task RefreshDevicesAsync()
    {
        StatusText.Text = "正在读取 Windows 已配对蓝牙设备…";
        try
        {
            var devices = await discovery.GetPairedBluetoothDevicesAsync();
            var audioContainers = await Task.Run(() => new WindowsBluetoothAudioController()
                .GetAllEndpoints()
                .Select(endpoint => endpoint.ContainerId)
                .ToHashSet());
            knownAudioContainers.UnionWith(audioContainers);
            var choices = BluetoothAudioCandidateSelector.Select(
                devices,
                knownAudioContainers,
                settings.GetTarget());

            DeviceCombo.ItemsSource = choices;
            DeviceCombo.SelectedItem = choices.FirstOrDefault(choice =>
                choice.Address.Equals(settings.TargetAddress, StringComparison.OrdinalIgnoreCase));
            DeviceCombo.SelectedItem ??= choices.FirstOrDefault(choice =>
                choice.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase));
            StatusText.Text = $"找到 {choices.Count} 个蓝牙音频候选设备。";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"读取设备失败：{exception.Message}";
        }
    }

    private async void OnRefreshDevices(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();

    private void RefreshControllers()
    {
        var choices = gameControllerWatcher.GetConnectedControllers()
            .Select(controller => new GameControllerChoice(controller.Id, controller.DisplayName, controller.Label))
            .ToList();
        if (!string.IsNullOrWhiteSpace(settings.GameControllerId) &&
            choices.All(choice => !choice.Id.Equals(settings.GameControllerId, StringComparison.Ordinal)))
        {
            choices.Insert(0, new GameControllerChoice(
                settings.GameControllerId,
                settings.GameControllerDisplayName ?? "已绑定手柄",
                $"{settings.GameControllerDisplayName ?? "已绑定手柄"}  ·  当前未连接"));
        }

        ControllerCombo.ItemsSource = choices;
        ControllerCombo.SelectedItem = choices.FirstOrDefault(choice =>
            choice.Id.Equals(settings.GameControllerId, StringComparison.Ordinal));
        ControllerCombo.SelectedItem ??= choices.FirstOrDefault();
    }

    private void OnRefreshControllers(object sender, RoutedEventArgs e) => RefreshControllers();

    private void OnControllerListChanged(object? sender, GameControllerSnapshot controller) =>
        Dispatcher.InvokeAsync(RefreshControllers);

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not BluetoothAudioCandidate choice)
        {
            StatusText.Text = "请先选择一副已配对耳机。";
            return;
        }

        var controllerChoice = ControllerCombo.SelectedItem as GameControllerChoice;
        if (ControllerRelayCheck.IsChecked == true && controllerChoice is null)
        {
            StatusText.Text = "请先连接并选择一个手柄，或关闭手柄接力。";
            return;
        }

        var cooldown = int.TryParse(CooldownText.Text, out var parsed) ? Math.Clamp(parsed, 1, 1440) : 30;
        settings = settings with
        {
            TargetAddress = choice.Address,
            TargetContainerId = choice.ContainerId,
            TargetDisplayName = choice.Name,
            AutoRelayEnabled = AutoRelayCheck.IsChecked == true,
            ConnectOnUnlock = UnlockCheck.IsChecked == true,
            ReconnectOnDisconnect = ReconnectCheck.IsChecked == true,
            CooldownMinutes = cooldown,
            PopupEnabled = PopupCheck.IsChecked == true,
            InEarMediaControlEnabled = InEarMediaControlCheck.IsChecked == true,
            ConnectOnController = ControllerRelayCheck.IsChecked == true,
            GameControllerId = controllerChoice?.Id,
            GameControllerDisplayName = controllerChoice?.Name,
            StartWithWindows = StartupCheck.IsChecked == true,
            HotkeyModifiers = ModifiersText.Text.Trim(),
            HotkeyKey = HotkeyText.Text.Trim()
        };
        SettingsSaved?.Invoke(this, settings);
        StatusText.Text = "设置已保存。";
    }

    private void OnTestConnection(object sender, RoutedEventArgs e) => TestConnectionRequested?.Invoke(this, EventArgs.Empty);

    private void OnPreviewPopup(object sender, RoutedEventArgs e) => PopupPreviewRequested?.Invoke(this, EventArgs.Empty);

    private void OnExportDiagnostics(object sender, RoutedEventArgs e) => ExportDiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnWindowClose(object sender, RoutedEventArgs e) => Close();

    private sealed record GameControllerChoice(string Id, string Name, string Label);

}
