using PodRelay.Core.AutoRelay;
using PodRelay.Core.Connection;

namespace PodRelay.App.Settings;

public sealed record AppSettings
{
    public string? TargetAddress { get; init; }
    public Guid? TargetContainerId { get; init; }
    public string? TargetDisplayName { get; init; }
    public bool AutoRelayEnabled { get; init; }
    public bool ConnectOnUnlock { get; init; } = true;
    public bool ReconnectOnDisconnect { get; init; } = true;
    public int CooldownMinutes { get; init; } = 30;
    public bool PopupEnabled { get; init; } = true;
    public bool InEarMediaControlEnabled { get; init; } = true;
    public bool ConnectOnController { get; init; }
    public string? GameControllerId { get; init; }
    public string? GameControllerDisplayName { get; init; }
    public bool StartWithWindows { get; init; }
    public string HotkeyModifiers { get; init; } = "Control,Alt";
    public string HotkeyKey { get; init; } = "A";

    public TargetDevice? GetTarget() =>
        !string.IsNullOrWhiteSpace(TargetAddress) && TargetContainerId is not null
            ? new TargetDevice(TargetAddress, TargetContainerId.Value, TargetDisplayName ?? "AirPods")
            : null;

    public AutoRelaySettings GetAutoRelaySettings() => new(
        AutoRelayEnabled,
        ConnectOnUnlock,
        ReconnectOnDisconnect,
        TimeSpan.FromMinutes(Math.Clamp(CooldownMinutes, 1, 24 * 60)),
        [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)]);
}
