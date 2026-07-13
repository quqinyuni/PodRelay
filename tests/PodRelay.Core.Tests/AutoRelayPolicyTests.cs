using PodRelay.Core.AutoRelay;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class AutoRelayPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void LockedSessionNeverAutoConnects()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });

        var decision = policy.Evaluate(AutoRelayTrigger.TargetSeen, Now, isSessionLocked: true);

        Assert.False(decision.ShouldConnect);
        Assert.Contains("locked", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserCancellationSuppressesAutomaticButNotManualRelay()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });
        policy.NotifyUserCancelled(Now);

        var automatic = policy.Evaluate(AutoRelayTrigger.TargetSeen, Now.AddMinutes(1), isSessionLocked: false);
        var manual = policy.Evaluate(AutoRelayTrigger.Manual, Now.AddMinutes(1), isSessionLocked: false);

        Assert.False(automatic.ShouldConnect);
        Assert.True(manual.ShouldConnect);
    }

    [Fact]
    public void FailuresUseConfiguredThreeTenThirtySecondBackoff()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });

        policy.NotifyFailure(Now);
        Assert.Equal(TimeSpan.FromSeconds(3), policy.Evaluate(AutoRelayTrigger.TargetSeen, Now, false).Delay);

        policy.NotifyFailure(Now.AddSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.Evaluate(AutoRelayTrigger.TargetSeen, Now.AddSeconds(3), false).Delay);

        policy.NotifyFailure(Now.AddSeconds(13));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.Evaluate(AutoRelayTrigger.TargetSeen, Now.AddSeconds(13), false).Delay);
    }

    [Fact]
    public void SuccessResetsBackoff()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });
        policy.NotifyFailure(Now);
        policy.NotifySuccess();

        var decision = policy.Evaluate(AutoRelayTrigger.TargetSeen, Now, isSessionLocked: false);

        Assert.True(decision.ShouldConnect);
        Assert.Equal(TimeSpan.Zero, decision.Delay);
    }

    [Fact]
    public void ApplicationStartupUsesAutomaticRelayPolicy()
    {
        var enabled = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });
        var disabled = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = false });

        Assert.True(enabled.Evaluate(AutoRelayTrigger.ApplicationStarted, Now, false).ShouldConnect);
        Assert.False(disabled.Evaluate(AutoRelayTrigger.ApplicationStarted, Now, false).ShouldConnect);
    }

    [Fact]
    public void UnlockAndReconnectTogglesAreRespected()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with
        {
            Enabled = true,
            ConnectOnUnlock = false,
            ReconnectOnDisconnect = false
        });

        Assert.False(policy.Evaluate(AutoRelayTrigger.SessionUnlocked, Now, false).ShouldConnect);
        Assert.False(policy.Evaluate(AutoRelayTrigger.ConnectionLost, Now, false).ShouldConnect);
        Assert.True(policy.Evaluate(AutoRelayTrigger.TargetSeen, Now, false).ShouldConnect);
    }

    [Fact]
    public void OneHourPauseExpiresAtTheConfiguredBoundary()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });
        policy.Pause(Now, TimeSpan.FromHours(1));

        Assert.False(policy.Evaluate(AutoRelayTrigger.TargetSeen, Now.AddMinutes(59), false).ShouldConnect);
        Assert.True(policy.Evaluate(AutoRelayTrigger.TargetSeen, Now.AddHours(1), false).ShouldConnect);
    }

    [Fact]
    public void BoundControllerTriggerUsesLockAndCooldownProtection()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = true });

        Assert.True(policy.Evaluate(AutoRelayTrigger.ControllerConnected, Now, false).ShouldConnect);
        Assert.False(policy.Evaluate(AutoRelayTrigger.ControllerConnected, Now, true).ShouldConnect);

        policy.NotifyUserCancelled(Now);
        Assert.False(policy.Evaluate(AutoRelayTrigger.ControllerConnected, Now.AddMinutes(1), false).ShouldConnect);
    }

    [Fact]
    public void ExplicitControllerRelayWorksWithOfficeAutomationDisabled()
    {
        var policy = new AutoRelayPolicy(AutoRelaySettings.Default with { Enabled = false });

        Assert.True(policy.Evaluate(AutoRelayTrigger.ControllerConnected, Now, false).ShouldConnect);
        Assert.False(policy.Evaluate(AutoRelayTrigger.TargetSeen, Now, false).ShouldConnect);
    }
}
