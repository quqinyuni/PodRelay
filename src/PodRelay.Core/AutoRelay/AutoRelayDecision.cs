namespace PodRelay.Core.AutoRelay;

public sealed record AutoRelayDecision(bool ShouldConnect, TimeSpan Delay, string Reason);

