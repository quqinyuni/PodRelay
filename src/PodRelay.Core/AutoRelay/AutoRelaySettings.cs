namespace PodRelay.Core.AutoRelay;

public sealed record AutoRelaySettings(
    bool Enabled,
    bool ConnectOnUnlock,
    bool ReconnectOnDisconnect,
    TimeSpan UserCooldown,
    IReadOnlyList<TimeSpan> RetryDelays)
{
    public static AutoRelaySettings Default { get; } = new(
        Enabled: false,
        ConnectOnUnlock: true,
        ReconnectOnDisconnect: true,
        UserCooldown: TimeSpan.FromMinutes(30),
        RetryDelays: [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)]);
}

