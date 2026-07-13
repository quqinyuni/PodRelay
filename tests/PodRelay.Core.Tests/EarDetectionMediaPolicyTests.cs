using PodRelay.Core.Discovery;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class EarDetectionMediaPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 12, 20, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void RemovingEitherWornPodRequestsPause()
    {
        var policy = new EarDetectionMediaPolicy();

        Assert.Equal(EarMediaAction.None, policy.Observe(2, true, true, Now));
        Assert.Equal(EarMediaAction.Pause, policy.Observe(1, true, true, Now.AddSeconds(1)));
    }

    [Fact]
    public void ReinsertionResumesOnlyAfterSuccessfulAutomaticPause()
    {
        var policy = new EarDetectionMediaPolicy();
        policy.Observe(1, true, true, Now);
        Assert.Equal(EarMediaAction.Pause, policy.Observe(0, true, true, Now.AddSeconds(1)));
        policy.NotifyPauseResult(true, Now.AddSeconds(1));

        Assert.Equal(EarMediaAction.Resume, policy.Observe(1, true, true, Now.AddSeconds(2)));
    }

    [Fact]
    public void FailedPauseDoesNotArmAutomaticResume()
    {
        var policy = new EarDetectionMediaPolicy();
        policy.Observe(1, true, true, Now);
        Assert.Equal(EarMediaAction.Pause, policy.Observe(0, true, true, Now.AddSeconds(1)));
        policy.NotifyPauseResult(false, Now.AddSeconds(1));

        Assert.Equal(EarMediaAction.None, policy.Observe(1, true, true, Now.AddSeconds(2)));
    }

    [Fact]
    public void DisabledOrDisconnectedPolicyNeverControlsMedia()
    {
        var policy = new EarDetectionMediaPolicy();
        policy.Observe(2, true, true, Now);

        Assert.Equal(EarMediaAction.None, policy.Observe(1, true, false, Now.AddSeconds(1)));
        Assert.Equal(EarMediaAction.None, policy.Observe(0, false, true, Now.AddSeconds(2)));
    }

    [Fact]
    public void ResumeArmExpires()
    {
        var policy = new EarDetectionMediaPolicy(TimeSpan.FromMinutes(10));
        policy.Observe(1, true, true, Now);
        policy.Observe(0, true, true, Now.AddSeconds(1));
        policy.NotifyPauseResult(true, Now.AddSeconds(1));

        Assert.Equal(EarMediaAction.None, policy.Observe(1, true, true, Now.AddMinutes(11)));
    }

    [Fact]
    public void SuccessfulResumeIgnoresBriefPartialSensorFallback()
    {
        var policy = new EarDetectionMediaPolicy(
            resumeSettlingWindow: TimeSpan.FromSeconds(3));
        policy.Observe(1, true, true, Now);
        policy.Observe(0, true, true, Now.AddSeconds(1));
        policy.NotifyPauseResult(true, Now.AddSeconds(1));
        Assert.Equal(EarMediaAction.Resume, policy.Observe(2, true, true, Now.AddSeconds(2)));
        policy.NotifyResumeResult(true, Now.AddSeconds(2));

        Assert.Equal(EarMediaAction.None, policy.Observe(1, true, true, Now.AddSeconds(4)));
        Assert.Equal(EarMediaAction.Pause, policy.Observe(0, true, true, Now.AddSeconds(4.1)));
    }

    [Fact]
    public void PartialRemovalAfterResumeSettlingWindowStillPauses()
    {
        var policy = new EarDetectionMediaPolicy(
            resumeSettlingWindow: TimeSpan.FromSeconds(3));
        policy.Observe(1, true, true, Now);
        policy.Observe(0, true, true, Now.AddSeconds(1));
        policy.NotifyPauseResult(true, Now.AddSeconds(1));
        policy.Observe(2, true, true, Now.AddSeconds(2));
        policy.NotifyResumeResult(true, Now.AddSeconds(2));

        Assert.Equal(EarMediaAction.Pause, policy.Observe(1, true, true, Now.AddSeconds(6)));
    }
}
