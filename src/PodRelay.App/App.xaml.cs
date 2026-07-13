using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PodRelay.App.Services;
using PodRelay.App.Settings;
using PodRelay.Core.Audio;
using PodRelay.Core.AutoRelay;
using PodRelay.Core.Connection;
using PodRelay.Core.Devices;
using PodRelay.Core.Discovery;

namespace PodRelay.App;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\PodRelay.9DA9021F-6FB1-4B4C-9D6B-5CF68242A2C4";
    private readonly SettingsStore settingsStore = new();
    private readonly WindowsDeviceDiscovery discovery = new();
    private readonly WindowsConnectionPlatform platform = new();
    private readonly WindowsDefaultAudioController defaultAudio = new();
    private AppSettings settings = new();
    private AutoRelayPolicy autoRelayPolicy = new(AutoRelaySettings.Default);
    private EnsureConnectedCoordinator coordinator = null!;
    private MainWindow settingsWindow = null!;
    private RelayPopupWindow popup = null!;
    private TrayIconService tray = null!;
    private GlobalHotkeyService hotkey = null!;
    private AirPodsAdvertisementWatcher advertisementWatcher = null!;
    private WindowsGameControllerWatcher gameControllerWatcher = null!;
    private DispatcherTimer healthTimer = null!;
    private CancellationTokenSource? activeConnectionCancellation;
    private Task<EnsureConnectionResult>? activeConnection;
    private bool sessionLocked;
    private bool exiting;
    private bool automaticCheckRunning;
    private bool? lastObservedFullyConnected;
    private DateTimeOffset lastFullyConnectedAt = DateTimeOffset.MinValue;
    private AirPodsWearState lastAirPodsWearState = AirPodsWearState.Unknown;
    private DateTimeOffset lastAirPodsStateAt = DateTimeOffset.MinValue;
    private ushort? lastLoggedAirPodsModelCode;
    private AirPodsWearState lastLoggedAirPodsWearState = AirPodsWearState.Unknown;
    private LocalDiagnosticLog diagnosticLog = null!;
    private readonly NearbyPopupGate nearbyPopupGate = new(TimeSpan.FromSeconds(30));
    private readonly EarDetectionMediaPolicy earMediaPolicy = new();
    private readonly SystemMediaSessionService mediaSessionService = new();
    private CancellationTokenSource? pendingEarMediaCancellation;
    private Mutex? instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(
                    "PodRelay 已在运行。请使用通知区域图标或全局快捷键。",
                    "PodRelay",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            instanceMutex.Dispose();
            instanceMutex = null;
            Shutdown();
            return;
        }

        diagnosticLog = new LocalDiagnosticLog(settingsStore.DirectoryPath);
        settings = await settingsStore.LoadAsync();
        await diagnosticLog.WriteAsync("application.started", new { background = e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase) });
        coordinator = new EnsureConnectedCoordinator(platform);
        coordinator.StateChanged += OnConnectionStateChanged;

        popup = new RelayPopupWindow();
        popup.ConnectRequested += async (_, _) => await RunGuardedAsync(
            "popup.connect.failed",
            () => EnsureConnectedAsync(automatic: false));
        popup.CancelRequested += (_, _) => CancelConnectionAndCoolDown();

        gameControllerWatcher = new WindowsGameControllerWatcher();
        gameControllerWatcher.ControllerConnected += OnGameControllerConnected;
        gameControllerWatcher.ControllerDisconnected += OnGameControllerDisconnected;

        settingsWindow = new MainWindow(settings, discovery, gameControllerWatcher);
        settingsWindow.SettingsSaved += async (_, updated) => await RunGuardedAsync(
            "settings.apply.failed",
            () => ApplySettingsAsync(updated));
        settingsWindow.TestConnectionRequested += async (_, _) => await RunGuardedAsync(
            "settings.test.failed",
            () => EnsureConnectedAsync(automatic: false));
        settingsWindow.PopupPreviewRequested += (_, _) =>
            popup.ShowNearby(settings.GetTarget()?.DisplayName ?? "AirPods Pro");
        settingsWindow.ExportDiagnosticsRequested += (_, _) => ExportDiagnostics();

        hotkey = new GlobalHotkeyService();
        hotkey.Pressed += async (_, _) => await RunGuardedAsync(
            "hotkey.connect.failed",
            async () =>
            {
                await diagnosticLog.WriteAsync("hotkey.pressed", new
                {
                    settings.HotkeyModifiers,
                    settings.HotkeyKey
                });
                await EnsureConnectedAsync(automatic: false);
            });

        tray = new TrayIconService(
            () => Dispatcher.InvokeAsync(() => RunGuardedAsync("tray.connect.failed", () => EnsureConnectedAsync(automatic: false))),
            () => Dispatcher.InvokeAsync(() => RunGuardedAsync("tray.release.failed", ReleaseAndCoolDownAsync)),
            () => Dispatcher.Invoke(ShowSettings),
            () => Dispatcher.Invoke(PauseOneHour),
            () => Dispatcher.Invoke(ExitApplication));

        advertisementWatcher = new AirPodsAdvertisementWatcher();
        advertisementWatcher.AirPodsSeen += (_, advertisement) => Dispatcher.InvokeAsync(
            () => RunGuardedAsync("airpods.advertisement.failed", () => OnAirPodsSeenAsync(advertisement)));

        SystemEvents.SessionSwitch += OnSessionSwitch;
        healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        healthTimer.Tick += async (_, _) => await RunGuardedAsync("health.check.failed", CheckAutomaticRelayAsync);

        await ApplySettingsAsync(settings);
        advertisementWatcher.Start();
        gameControllerWatcher.Start();
        healthTimer.Start();

        if (settings.GetTarget() is null || !e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            ShowSettings();
        }

        var startupDecision = autoRelayPolicy.Evaluate(
            AutoRelayTrigger.ApplicationStarted,
            DateTimeOffset.Now,
            sessionLocked);
        await diagnosticLog.WriteAsync("automatic.startup.evaluated", startupDecision);
        if (startupDecision.ShouldConnect && settings.GetTarget() is not null)
        {
            _ = Dispatcher.InvokeAsync(() => RunGuardedAsync(
                "automatic.startup.failed",
                () => RunPresenceSensitiveRelayAsync(
                    AutoRelayTrigger.ApplicationStarted,
                    showAutomaticPopup: false)));
        }
    }

    private async Task ApplySettingsAsync(AppSettings updated)
    {
        settings = updated;
        autoRelayPolicy = new AutoRelayPolicy(settings.GetAutoRelaySettings());
        if (!settings.InEarMediaControlEnabled)
        {
            earMediaPolicy.Reset();
            mediaSessionService.Reset();
        }
        await settingsStore.SaveAsync(settings);
        StartupRegistrationService.Apply(settings.StartWithWindows);
        var hotkeyRegistered = hotkey.Register(settings.HotkeyModifiers, settings.HotkeyKey);
        tray.SetState(
            hotkeyRegistered ? ConnectionState.Waiting : ConnectionState.Failed,
            hotkeyRegistered ? null : "快捷键注册失败");
        await diagnosticLog.WriteAsync("settings.saved", new
        {
            updated.TargetDisplayName,
            updated.AutoRelayEnabled,
            updated.ConnectOnUnlock,
            updated.ReconnectOnDisconnect,
            updated.PopupEnabled,
            updated.InEarMediaControlEnabled,
            updated.ConnectOnController,
            updated.GameControllerDisplayName,
            hotkeyRegistered
        });

        if (updated.ConnectOnController && !string.IsNullOrWhiteSpace(updated.GameControllerId))
        {
            var connectedController = gameControllerWatcher.GetConnectedControllers()
                .FirstOrDefault(controller => controller.Id.Equals(
                    updated.GameControllerId,
                    StringComparison.Ordinal));
            if (connectedController is not null)
            {
                await TryRelayForControllerAsync(connectedController, "SettingsApplied");
            }
        }
    }

    private async Task EnsureConnectedAsync(bool automatic, bool showAutomaticPopup = true)
    {
        var target = settings.GetTarget();
        if (target is null)
        {
            ShowSettings();
            return;
        }

        if (activeConnection is not null)
        {
            await activeConnection;
            return;
        }

        activeConnectionCancellation = new CancellationTokenSource();
        await diagnosticLog.WriteAsync("connection.started", new { target.DisplayName, automatic });
        var showConnectionUi = !automatic || (settings.PopupEnabled && showAutomaticPopup);
        if (showConnectionUi)
        {
            popup.ShowConnecting(target.DisplayName, automatic);
        }
        activeConnection = coordinator.EnsureConnectedAsync(target, activeConnectionCancellation.Token);
        var result = await activeConnection;
        activeConnection = null;
        activeConnectionCancellation.Dispose();
        activeConnectionCancellation = null;

        if (result.IsSuccess)
        {
            autoRelayPolicy.NotifySuccess();
            lastObservedFullyConnected = true;
            lastFullyConnectedAt = DateTimeOffset.Now;
        }
        else if (result.State != ConnectionState.Cancelled)
        {
            autoRelayPolicy.NotifyFailure(DateTimeOffset.Now);
        }

        tray.SetState(result.State, target.DisplayName);
        settingsWindow.SetConnectionState(result.State, result.Message);
        if (showConnectionUi)
        {
            popup.ShowResult(target.DisplayName, result);
        }
        await diagnosticLog.WriteAsync("connection.finished", result);
    }

    private async Task OnAirPodsSeenAsync(AirPodsAdvertisement advertisement)
    {
        var target = settings.GetTarget();
        if (target is null)
        {
            return;
        }

        if (!AirPodsAdvertisementClassifier.IsLikelyTargetModel(
            target.DisplayName,
            advertisement.PublicStatus.ModelCode))
        {
            return;
        }

        lastAirPodsWearState = advertisement.PublicStatus.WearState;
        lastAirPodsStateAt = advertisement.ReceivedAt;
        var presenceAction = AirPodsPresencePolicy.Evaluate(advertisement.PublicStatus.WearState);
        if (lastLoggedAirPodsModelCode != advertisement.PublicStatus.ModelCode ||
            lastLoggedAirPodsWearState != advertisement.PublicStatus.WearState)
        {
            lastLoggedAirPodsModelCode = advertisement.PublicStatus.ModelCode;
            lastLoggedAirPodsWearState = advertisement.PublicStatus.WearState;
            await diagnosticLog.WriteAsync("airpods.state-observed", new
            {
                advertisement.PublicStatus.ModelCodeHex,
                advertisement.PublicStatus.RawStatus,
                advertisement.PublicStatus.WearState,
                advertisement.SignalStrengthDbm,
                advertisement.ReceivedAt
            });
        }

        ScheduleEarMediaState(
            advertisement.PublicStatus.InEarPodCount,
            advertisement.ReceivedAt);

        if (presenceAction == AirPodsPresenceAction.Ignore)
        {
            nearbyPopupGate.Reset();
            popup.ClosePopup();
            return;
        }

        if (sessionLocked)
        {
            return;
        }

        var observation = await platform.ObserveAsync(target, CancellationToken.None);
        if (!observation.IsDeviceFound || !observation.IsPaired)
        {
            return;
        }

        if (observation.IsFullyConnected)
        {
            nearbyPopupGate.ShouldShow(
                advertisement.ReceivedAt,
                isTargetConnected: true,
                isUserSuppressed: false);
            return;
        }

        var now = DateTimeOffset.Now;
        if (presenceAction == AirPodsPresenceAction.Prompt)
        {
            var nearbyDecision = new AutoRelayDecision(
                false,
                TimeSpan.Zero,
                "AirPods are out of the case but not yet in an ear.");
            if (settings.PopupEnabled && nearbyPopupGate.ShouldShow(
                advertisement.ReceivedAt,
                observation.IsFullyConnected,
                now < autoRelayPolicy.UserSuppressedUntil))
            {
                await LogActionableAdvertisementAsync(advertisement, observation, nearbyDecision);
                popup.ShowNearby(target.DisplayName);
            }

            return;
        }

        if (presenceAction != AirPodsPresenceAction.AutoConnect)
        {
            return;
        }

        var decision = autoRelayPolicy.Evaluate(AutoRelayTrigger.TargetSeen, now, sessionLocked);
        if (decision.ShouldConnect)
        {
            await LogActionableAdvertisementAsync(advertisement, observation, decision);
            await EnsureConnectedAsync(automatic: true);
        }
        else if (settings.PopupEnabled && nearbyPopupGate.ShouldShow(
            advertisement.ReceivedAt,
            observation.IsFullyConnected,
            now < autoRelayPolicy.UserSuppressedUntil))
        {
            await LogActionableAdvertisementAsync(advertisement, observation, decision);
            popup.ShowNearby(target.DisplayName);
        }
    }

    private Task LogActionableAdvertisementAsync(
        AirPodsAdvertisement advertisement,
        ConnectionObservation observation,
        AutoRelayDecision decision) =>
        diagnosticLog.WriteAsync("airpods.actionable-advertisement", new
        {
            advertisement.SignalStrengthDbm,
            advertisement.ReceivedAt,
            advertisement.PublicStatus,
            targetPresent = observation.IsDevicePresent,
            alreadyConnected = observation.IsFullyConnected,
            decision
        });

    private void ScheduleEarMediaState(int inEarPodCount, DateTimeOffset observedAt)
    {
        pendingEarMediaCancellation?.Cancel();
        pendingEarMediaCancellation?.Dispose();
        pendingEarMediaCancellation = new CancellationTokenSource();
        var cancellationToken = pendingEarMediaCancellation.Token;

        _ = Dispatcher.InvokeAsync(() => RunGuardedAsync(
            "airpods.media-control.failed",
            async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(180), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await ApplyEarMediaStateAsync(inEarPodCount, observedAt);
            }));
    }

    private async Task ApplyEarMediaStateAsync(int inEarPodCount, DateTimeOffset observedAt)
    {
        var now = DateTimeOffset.Now;
        var recentlyConnected = lastObservedFullyConnected == true ||
            now - lastFullyConnectedAt <= TimeSpan.FromSeconds(10);
        var action = earMediaPolicy.Observe(
            inEarPodCount,
            recentlyConnected && !sessionLocked,
            settings.InEarMediaControlEnabled,
            observedAt);
        if (action == EarMediaAction.None)
        {
            return;
        }

        MediaSessionCommandResult result;
        if (action == EarMediaAction.Pause)
        {
            result = await mediaSessionService.PauseCurrentAsync();
            earMediaPolicy.NotifyPauseResult(result.Executed, now);
        }
        else
        {
            result = await mediaSessionService.ResumePausedAsync();
            earMediaPolicy.NotifyResumeResult(result.Executed, now);
        }

        await diagnosticLog.WriteAsync("airpods.media-control", new
        {
            action,
            inEarPodCount,
            result.Executed,
            result.Reason,
            result.SourceAppUserModelId
        });
    }

    private void OnGameControllerConnected(object? sender, GameControllerSnapshot controller) =>
        Dispatcher.InvokeAsync(() => RunGuardedAsync(
            "controller.relay.failed",
            () => TryRelayForControllerAsync(controller, "ControllerConnected")));

    private void OnGameControllerDisconnected(object? sender, GameControllerSnapshot controller) =>
        Dispatcher.InvokeAsync(() => diagnosticLog.WriteAsync("controller.disconnected", new
        {
            controller.DisplayName,
            controller.Id,
            isBoundController = controller.Id.Equals(settings.GameControllerId, StringComparison.Ordinal)
        }));

    private async Task TryRelayForControllerAsync(GameControllerSnapshot controller, string source)
    {
        var isBoundController = settings.ConnectOnController &&
            !string.IsNullOrWhiteSpace(settings.GameControllerId) &&
            controller.Id.Equals(settings.GameControllerId, StringComparison.Ordinal);
        await diagnosticLog.WriteAsync("controller.observed", new
        {
            controller.DisplayName,
            controller.Id,
            controller.VendorId,
            controller.ProductId,
            controller.IsWireless,
            source,
            isBoundController
        });
        if (!isBoundController)
        {
            return;
        }

        var retryDelays = new[] { TimeSpan.Zero }
            .Concat(settings.GetAutoRelaySettings().RetryDelays)
            .ToArray();
        for (var attempt = 0; attempt < retryDelays.Length; attempt++)
        {
            var delay = retryDelays[attempt];
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay + TimeSpan.FromMilliseconds(250));
            }

            var controllerStillConnected = gameControllerWatcher.GetConnectedControllers()
                .Any(candidate => candidate.Id.Equals(controller.Id, StringComparison.Ordinal));
            if (!settings.ConnectOnController || !controllerStillConnected)
            {
                await diagnosticLog.WriteAsync("automatic.controller.retry-stopped", new
                {
                    attempt = attempt + 1,
                    reason = "The bound controller is no longer connected or controller relay was disabled."
                });
                return;
            }

            var decision = autoRelayPolicy.Evaluate(
                AutoRelayTrigger.ControllerConnected,
                DateTimeOffset.Now,
                sessionLocked);
            await diagnosticLog.WriteAsync("automatic.controller.evaluated", new
            {
                attempt = attempt + 1,
                configuredDelay = delay,
                decision
            });
            if (!decision.ShouldConnect)
            {
                return;
            }

            await EnsureConnectedAsync(automatic: true);
            var target = settings.GetTarget();
            if (target is null)
            {
                return;
            }

            var observation = await platform.ObserveAsync(target, CancellationToken.None);
            if (observation.IsFullyConnected)
            {
                await diagnosticLog.WriteAsync("automatic.controller.succeeded", new
                {
                    attempt = attempt + 1,
                    observation
                });
                return;
            }
        }

        await diagnosticLog.WriteAsync("automatic.controller.retries-exhausted", new
        {
            attempts = retryDelays.Length
        });
    }

    private bool HasRecentInEarSignal(DateTimeOffset now) =>
        AirPodsPresencePolicy.HasRecentInEarSignal(
            lastAirPodsWearState,
            lastAirPodsStateAt,
            now,
            TimeSpan.FromSeconds(30));

    private async Task RunPresenceSensitiveRelayAsync(
        AutoRelayTrigger trigger,
        bool showAutomaticPopup)
    {
        var target = settings.GetTarget();
        if (target is null)
        {
            return;
        }

        var observation = await platform.ObserveAsync(target, CancellationToken.None);
        var now = DateTimeOffset.Now;
        if (observation.IsFullyConnected || HasRecentInEarSignal(now))
        {
            await EnsureConnectedAsync(automatic: true, showAutomaticPopup);
            return;
        }

        await diagnosticLog.WriteAsync("automatic.presence-sensitive.deferred", new
        {
            trigger,
            reason = "Waiting for an in-ear AirPods frame, the bound controller, or a manual request.",
            lastAirPodsWearState,
            lastAirPodsStateAt
        });
    }

    private async Task CheckAutomaticRelayAsync()
    {
        if (automaticCheckRunning || sessionLocked || activeConnection is not null)
        {
            return;
        }

        automaticCheckRunning = true;
        try
        {
            var target = settings.GetTarget();
            if (target is null)
            {
                return;
            }

            var observation = await platform.ObserveAsync(target, CancellationToken.None);
            var now = DateTimeOffset.Now;
            var connectionLost = lastObservedFullyConnected == true && !observation.IsFullyConnected;
            lastObservedFullyConnected = observation.IsFullyConnected;
            if (observation.IsFullyConnected)
            {
                lastFullyConnectedAt = now;
            }
            var observedState = observation switch
            {
                { IsBluetoothOn: false } => ConnectionState.BluetoothOff,
                { IsDeviceFound: false } or { IsPaired: false } => ConnectionState.DeviceUnavailable,
                { IsFullyConnected: true } => ConnectionState.Connected,
                { IsBluetoothConnected: true } => ConnectionState.AudioNotReady,
                _ => ConnectionState.Waiting
            };
            var inUserCooldown = settings.AutoRelayEnabled && now < autoRelayPolicy.UserSuppressedUntil;
            var displayState = inUserCooldown ? ConnectionState.CoolingDown : observedState;
            var displayDetail = inUserCooldown
                ? $"暂停至 {autoRelayPolicy.UserSuppressedUntil:HH:mm}"
                : target.DisplayName;
            tray.SetState(displayState, displayDetail);
            settingsWindow.SetConnectionState(displayState, displayDetail);

            if (!settings.AutoRelayEnabled || inUserCooldown)
            {
                return;
            }

            if (connectionLost)
            {
                var recentInEar = HasRecentInEarSignal(now);
                if (!recentInEar)
                {
                    await diagnosticLog.WriteAsync("automatic.connection-lost.suppressed", new
                    {
                        reason = "No recent in-ear AirPods signal.",
                        lastAirPodsWearState,
                        lastAirPodsStateAt
                    });
                    return;
                }

                var lostDecision = autoRelayPolicy.Evaluate(
                    AutoRelayTrigger.ConnectionLost,
                    now,
                    sessionLocked);
                await diagnosticLog.WriteAsync("automatic.connection-lost.evaluated", lostDecision);
                if (lostDecision.ShouldConnect)
                {
                    await EnsureConnectedAsync(automatic: true, showAutomaticPopup: true);
                    return;
                }
            }

            var hasActiveOutput = defaultAudio.GetRenderEndpoints().Any(endpoint => endpoint.IsActive);
            if (hasActiveOutput)
            {
                return;
            }

            var hasRecentInEarSignal = HasRecentInEarSignal(now);
            if (!hasRecentInEarSignal)
            {
                return;
            }

            var decision = autoRelayPolicy.Evaluate(
                AutoRelayTrigger.NoActiveAudioOutput,
                now,
                sessionLocked);
            await diagnosticLog.WriteAsync("automatic.no-output.evaluated", decision);
            if (decision.ShouldConnect)
            {
                await EnsureConnectedAsync(automatic: true);
            }
        }
        finally
        {
            automaticCheckRunning = false;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                sessionLocked = true;
                pendingEarMediaCancellation?.Cancel();
                earMediaPolicy.Reset();
                mediaSessionService.Reset();
                activeConnectionCancellation?.Cancel();
                popup.ClosePopup();
                await diagnosticLog.WriteAsync("session.locked");
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                sessionLocked = false;
                await diagnosticLog.WriteAsync("session.unlocked");
                var decision = autoRelayPolicy.Evaluate(
                    AutoRelayTrigger.SessionUnlocked,
                    DateTimeOffset.Now,
                    isSessionLocked: false);
                await diagnosticLog.WriteAsync("automatic.unlock.evaluated", decision);
                if (decision.ShouldConnect)
                {
                    await RunGuardedAsync(
                        "session.unlock.failed",
                        () => RunPresenceSensitiveRelayAsync(
                            AutoRelayTrigger.SessionUnlocked,
                            showAutomaticPopup: false));
                }
            }
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        Dispatcher.InvokeAsync(() =>
        {
            tray.SetState(state, settings.TargetDisplayName);
            settingsWindow.SetConnectionState(state, settings.TargetDisplayName);
        });

    private void CancelConnectionAndCoolDown()
    {
        activeConnectionCancellation?.Cancel();
        autoRelayPolicy.NotifyUserCancelled(DateTimeOffset.Now);
        tray.SetState(ConnectionState.CoolingDown, "用户已取消");
        settingsWindow.SetConnectionState(ConnectionState.CoolingDown, "用户已取消");
        _ = diagnosticLog.WriteAsync("automatic.user-cancelled", new
        {
            suppressedUntil = autoRelayPolicy.UserSuppressedUntil
        });
    }

    private void PauseOneHour()
    {
        activeConnectionCancellation?.Cancel();
        autoRelayPolicy.Pause(DateTimeOffset.Now, TimeSpan.FromHours(1));
        tray.SetState(ConnectionState.CoolingDown, "暂停 1 小时");
        settingsWindow.SetConnectionState(ConnectionState.CoolingDown, "暂停 1 小时");
        _ = diagnosticLog.WriteAsync("automatic.paused", new
        {
            suppressedUntil = autoRelayPolicy.UserSuppressedUntil
        });
    }

    private async Task ReleaseAndCoolDownAsync()
    {
        var target = settings.GetTarget();
        if (target is null)
        {
            return;
        }

        activeConnectionCancellation?.Cancel();
        autoRelayPolicy.NotifyUserCancelled(DateTimeOffset.Now);
        const string disconnectDetail = "自动接力已暂停 30 分钟；现在可从其他设备连接";
        tray.SetState(
            ConnectionState.CoolingDown,
            disconnectDetail);
        settingsWindow.SetConnectionState(
            ConnectionState.CoolingDown,
            disconnectDetail);
        popup.ClosePopup();
        await diagnosticLog.WriteAsync("connection.release-to-other-device", new
        {
            target.DisplayName,
            suppressedUntil = autoRelayPolicy.UserSuppressedUntil
        });
    }

    private void ShowSettings()
    {
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private async Task RunGuardedAsync(string failureEvent, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            await diagnosticLog.WriteAsync(failureEvent, new
            {
                exception = exception.GetType().FullName,
                exception.Message,
                exception.StackTrace
            });
            tray.SetState(ConnectionState.Failed, exception.Message);
        }
    }

    private void ExportDiagnostics()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 PodRelay 诊断",
            FileName = $"PodRelay-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            DefaultExt = ".zip",
            Filter = "ZIP 压缩包|*.zip"
        };
        if (dialog.ShowDialog(settingsWindow) != true)
        {
            return;
        }

        try
        {
            DiagnosticsExportService.Export(settingsStore.DirectoryPath, dialog.FileName);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                settingsWindow,
                $"导出失败：{exception.Message}",
                "PodRelay",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExitApplication()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        activeConnectionCancellation?.Cancel();
        pendingEarMediaCancellation?.Cancel();
        pendingEarMediaCancellation?.Dispose();
        healthTimer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        advertisementWatcher.Dispose();
        gameControllerWatcher.ControllerConnected -= OnGameControllerConnected;
        gameControllerWatcher.ControllerDisconnected -= OnGameControllerDisconnected;
        gameControllerWatcher.Dispose();
        hotkey.Dispose();
        tray.Dispose();
        instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        instanceMutex = null;
        popup.Close();
        settingsWindow.CloseForExit();
        Shutdown();
    }
}
