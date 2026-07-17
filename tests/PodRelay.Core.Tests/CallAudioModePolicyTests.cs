using PodRelay.Core.Audio;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class CallAudioModePolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void ActiveCaptureRequestsCallModeOnce()
    {
        var policy = new CallAudioModePolicy(TimeSpan.FromSeconds(2));

        var decision = policy.Evaluate(true, true, true, Now);
        policy.NotifySucceeded(decision.Action);

        Assert.Equal(CallAudioModeAction.EnterCallMode, decision.Action);
        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, true, true, Now.AddSeconds(1)).Action);
    }

    [Fact]
    public void IdleCaptureRestoresHighQualityAfterGracePeriod()
    {
        var policy = new CallAudioModePolicy(TimeSpan.FromSeconds(2));
        var enter = policy.Evaluate(true, true, true, Now);
        policy.NotifySucceeded(enter.Action);

        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, false, true, Now.AddSeconds(1)).Action);
        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, false, true, Now.AddSeconds(2.9)).Action);
        Assert.Equal(
            CallAudioModeAction.RestoreHighQuality,
            policy.Evaluate(true, false, true, Now.AddSeconds(3)).Action);
    }

    [Fact]
    public void BriefSessionRebuildCancelsPendingRestore()
    {
        var policy = new CallAudioModePolicy(TimeSpan.FromSeconds(2));
        var enter = policy.Evaluate(true, true, true, Now);
        policy.NotifySucceeded(enter.Action);
        policy.Evaluate(true, false, true, Now.AddSeconds(1));

        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, true, true, Now.AddSeconds(2)).Action);
        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, false, true, Now.AddSeconds(3)).Action);
    }

    [Fact]
    public void UnifiedEndpointIsLeftToWindows()
    {
        var policy = new CallAudioModePolicy(TimeSpan.FromSeconds(2));

        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(true, true, false, Now).Action);
        Assert.False(policy.OwnsCallMode);
    }

    [Fact]
    public void DisablingDuringCallWaitsUntilCaptureEndsThenRestores()
    {
        var policy = new CallAudioModePolicy(TimeSpan.FromSeconds(2));
        var enter = policy.Evaluate(true, true, true, Now);
        policy.NotifySucceeded(enter.Action);

        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(false, true, true, Now.AddSeconds(1)).Action);
        Assert.Equal(
            CallAudioModeAction.None,
            policy.Evaluate(false, false, true, Now.AddSeconds(2)).Action);
        Assert.Equal(
            CallAudioModeAction.RestoreHighQuality,
            policy.Evaluate(false, false, true, Now.AddSeconds(4)).Action);
    }
}
