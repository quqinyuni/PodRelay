namespace PodRelay.Core.Connection;

public sealed record ConnectionAttempt(
    string Stage,
    string Outcome,
    string Detail,
    TimeSpan Elapsed,
    IReadOnlyList<int>? HResults = null);
