namespace PodRelay.Core.Discovery;

public sealed class AdvertisementDuplicateGate
{
    private readonly object sync = new();
    private readonly TimeSpan duplicateWindow;
    private DateTimeOffset lastAcceptedAt = DateTimeOffset.MinValue;
    private string? lastKey;

    public AdvertisementDuplicateGate(TimeSpan duplicateWindow)
    {
        if (duplicateWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duplicateWindow));
        }

        this.duplicateWindow = duplicateWindow;
    }

    public bool TryAccept(DateTimeOffset receivedAt, string? key = null)
    {
        lock (sync)
        {
            if (key == lastKey && receivedAt - lastAcceptedAt < duplicateWindow)
            {
                return false;
            }

            lastAcceptedAt = receivedAt;
            lastKey = key;
            return true;
        }
    }
}
