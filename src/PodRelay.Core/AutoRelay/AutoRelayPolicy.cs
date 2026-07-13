namespace PodRelay.Core.AutoRelay;

public sealed class AutoRelayPolicy
{
    private readonly AutoRelaySettings settings;
    private DateTimeOffset userSuppressedUntil = DateTimeOffset.MinValue;
    private DateTimeOffset retryNotBefore = DateTimeOffset.MinValue;
    private int consecutiveFailures;

    public AutoRelayPolicy(AutoRelaySettings settings)
    {
        this.settings = settings;
    }

    public DateTimeOffset UserSuppressedUntil => userSuppressedUntil;

    public AutoRelayDecision Evaluate(
        AutoRelayTrigger trigger,
        DateTimeOffset now,
        bool isSessionLocked)
    {
        if (isSessionLocked)
        {
            return new AutoRelayDecision(false, TimeSpan.Zero, "The Windows session is locked.");
        }

        if (trigger == AutoRelayTrigger.Manual)
        {
            return new AutoRelayDecision(true, TimeSpan.Zero, "Manual connection request.");
        }

        if (!settings.Enabled && trigger != AutoRelayTrigger.ControllerConnected)
        {
            return new AutoRelayDecision(false, TimeSpan.Zero, "Automatic relay is disabled.");
        }

        if (now < userSuppressedUntil)
        {
            return new AutoRelayDecision(false, userSuppressedUntil - now, "Automatic relay is in user cooldown.");
        }

        if (trigger == AutoRelayTrigger.SessionUnlocked && !settings.ConnectOnUnlock)
        {
            return new AutoRelayDecision(false, TimeSpan.Zero, "Connect on unlock is disabled.");
        }

        if (trigger == AutoRelayTrigger.ConnectionLost && !settings.ReconnectOnDisconnect)
        {
            return new AutoRelayDecision(false, TimeSpan.Zero, "Reconnect on disconnect is disabled.");
        }

        if (now < retryNotBefore)
        {
            return new AutoRelayDecision(false, retryNotBefore - now, "Reconnect backoff is active.");
        }

        return new AutoRelayDecision(true, TimeSpan.Zero, $"Automatic relay trigger: {trigger}.");
    }

    public void NotifyUserCancelled(DateTimeOffset now) =>
        userSuppressedUntil = now + settings.UserCooldown;

    public void Pause(DateTimeOffset now, TimeSpan duration) =>
        userSuppressedUntil = now + duration;

    public void NotifySuccess()
    {
        consecutiveFailures = 0;
        retryNotBefore = DateTimeOffset.MinValue;
    }

    public void NotifyFailure(DateTimeOffset now)
    {
        var delays = settings.RetryDelays.Count == 0
            ? [TimeSpan.FromSeconds(30)]
            : settings.RetryDelays;
        var delay = delays[Math.Min(consecutiveFailures, delays.Count - 1)];
        consecutiveFailures++;
        retryNotBefore = now + delay;
    }
}
