namespace PodRelay.Core.Discovery;

public enum AirPodsPresenceAction
{
    Ignore,
    Prompt,
    AutoConnect
}

public static class AirPodsPresencePolicy
{
    public static AirPodsPresenceAction Evaluate(AirPodsWearState state) => state switch
    {
        AirPodsWearState.InCase => AirPodsPresenceAction.Ignore,
        AirPodsWearState.OutOfCase => AirPodsPresenceAction.Prompt,
        AirPodsWearState.InEar => AirPodsPresenceAction.AutoConnect,
        _ => AirPodsPresenceAction.Ignore
    };

    public static bool HasRecentInEarSignal(
        AirPodsWearState state,
        DateTimeOffset observedAt,
        DateTimeOffset now,
        TimeSpan maximumAge) =>
        maximumAge >= TimeSpan.Zero &&
        state == AirPodsWearState.InEar &&
        observedAt <= now &&
        now - observedAt <= maximumAge;
}
