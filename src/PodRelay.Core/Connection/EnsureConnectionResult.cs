namespace PodRelay.Core.Connection;

public sealed record EnsureConnectionResult(
    ConnectionState State,
    string Message,
    ConnectionObservation? Observation,
    ReconnectRequestResult? ReconnectRequest,
    TimeSpan Elapsed,
    IReadOnlyList<ConnectionAttempt>? Attempts = null)
{
    public bool IsSuccess => State == ConnectionState.Connected;
}
