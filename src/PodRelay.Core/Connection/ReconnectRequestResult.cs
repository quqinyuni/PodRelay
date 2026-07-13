namespace PodRelay.Core.Connection;

public sealed record ReconnectRequestResult(
    int RequestedEndpointCount,
    IReadOnlyList<int> HResults)
{
    public bool WasAccepted => RequestedEndpointCount > 0 && HResults.All(result => result >= 0);
}

