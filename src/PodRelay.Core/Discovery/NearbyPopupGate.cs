namespace PodRelay.Core.Discovery;

public sealed class NearbyPopupGate
{
    private readonly TimeSpan episodeGap;
    private DateTimeOffset lastSeenAt = DateTimeOffset.MinValue;
    private bool shownInCurrentEpisode;

    public NearbyPopupGate(TimeSpan episodeGap)
    {
        if (episodeGap <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeGap));
        }

        this.episodeGap = episodeGap;
    }

    public bool ShouldShow(
        DateTimeOffset seenAt,
        bool isTargetConnected,
        bool isUserSuppressed)
    {
        if (lastSeenAt == DateTimeOffset.MinValue || seenAt - lastSeenAt > episodeGap)
        {
            shownInCurrentEpisode = false;
        }

        lastSeenAt = seenAt;
        if (isTargetConnected)
        {
            // A later disconnected advertisement should start one actionable popup,
            // even if the earbuds kept advertising while connected.
            shownInCurrentEpisode = false;
            return false;
        }

        if (isUserSuppressed || shownInCurrentEpisode)
        {
            return false;
        }

        shownInCurrentEpisode = true;
        return true;
    }

    public void Reset()
    {
        lastSeenAt = DateTimeOffset.MinValue;
        shownInCurrentEpisode = false;
    }
}
