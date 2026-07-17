namespace PodRelay.Core.Audio;

public sealed class CallAudioModePolicy(TimeSpan restoreDelay)
{
    private DateTimeOffset? captureInactiveSince;

    public bool OwnsCallMode { get; private set; }

    public CallAudioModeDecision Evaluate(
        bool enabled,
        bool hasActiveCaptureSession,
        bool hasSeparateRenderProfiles,
        DateTimeOffset now)
    {
        if (!hasSeparateRenderProfiles)
        {
            captureInactiveSince = null;
            OwnsCallMode = false;
            return new CallAudioModeDecision(CallAudioModeAction.None, "Windows is managing a unified Bluetooth endpoint.");
        }

        if (hasActiveCaptureSession)
        {
            captureInactiveSince = null;
            if (!enabled)
            {
                return new CallAudioModeDecision(CallAudioModeAction.None, "Automatic call audio mode is disabled.");
            }

            return OwnsCallMode
                ? new CallAudioModeDecision(CallAudioModeAction.None, "Call audio mode is already selected.")
                : new CallAudioModeDecision(CallAudioModeAction.EnterCallMode, "The AirPods microphone has an active capture session.");
        }

        if (!OwnsCallMode)
        {
            captureInactiveSince = null;
            return new CallAudioModeDecision(CallAudioModeAction.None, "The AirPods microphone is idle.");
        }

        captureInactiveSince ??= now;
        if (now - captureInactiveSince < restoreDelay)
        {
            return new CallAudioModeDecision(CallAudioModeAction.None, "Waiting for the microphone release grace period.");
        }

        return new CallAudioModeDecision(CallAudioModeAction.RestoreHighQuality, "The AirPods microphone capture session ended.");
    }

    public void NotifySucceeded(CallAudioModeAction action)
    {
        if (action == CallAudioModeAction.EnterCallMode)
        {
            OwnsCallMode = true;
        }
        else if (action == CallAudioModeAction.RestoreHighQuality)
        {
            OwnsCallMode = false;
            captureInactiveSince = null;
        }
    }
}

public sealed record CallAudioModeDecision(CallAudioModeAction Action, string Reason);

public enum CallAudioModeAction
{
    None,
    EnterCallMode,
    RestoreHighQuality
}
