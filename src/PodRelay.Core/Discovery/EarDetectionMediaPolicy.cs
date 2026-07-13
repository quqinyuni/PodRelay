namespace PodRelay.Core.Discovery;

public enum EarMediaAction
{
    None,
    Pause,
    Resume
}

public sealed class EarDetectionMediaPolicy
{
    private readonly TimeSpan resumeWindow;
    private readonly TimeSpan resumeSettlingWindow;
    private int? previousInEarCount;
    private bool resumeArmed;
    private DateTimeOffset pausedAt;
    private DateTimeOffset resumeSettlingUntil;

    public EarDetectionMediaPolicy(
        TimeSpan? resumeWindow = null,
        TimeSpan? resumeSettlingWindow = null)
    {
        this.resumeWindow = resumeWindow ?? TimeSpan.FromMinutes(10);
        this.resumeSettlingWindow = resumeSettlingWindow ?? TimeSpan.FromSeconds(3);
    }

    public EarMediaAction Observe(
        int inEarCount,
        bool isWindowsAirPodsConnectionRecent,
        bool enabled,
        DateTimeOffset now)
    {
        inEarCount = Math.Clamp(inEarCount, 0, 2);
        var previous = previousInEarCount;

        if (!enabled || !isWindowsAirPodsConnectionRecent)
        {
            previousInEarCount = inEarCount;
            resumeArmed = false;
            return EarMediaAction.None;
        }

        if (previous is null || previous == inEarCount)
        {
            previousInEarCount = inEarCount;
            return EarMediaAction.None;
        }

        // AirPods sometimes report the newly inserted pod and then briefly fall
        // back to one in-ear bit. Do not immediately undo a successful resume.
        // A real transition to zero pods still pauses without this grace period.
        if (inEarCount < previous &&
            inEarCount > 0 &&
            now < resumeSettlingUntil)
        {
            return EarMediaAction.None;
        }

        previousInEarCount = inEarCount;
        if (inEarCount < previous && previous > 0)
        {
            return EarMediaAction.Pause;
        }

        if (inEarCount > previous && resumeArmed)
        {
            if (now - pausedAt <= resumeWindow)
            {
                return EarMediaAction.Resume;
            }

            resumeArmed = false;
        }

        return EarMediaAction.None;
    }

    public void NotifyPauseResult(bool paused, DateTimeOffset now)
    {
        resumeArmed = paused;
        if (paused)
        {
            pausedAt = now;
        }
    }

    public void NotifyResumeResult(bool resumed, DateTimeOffset now)
    {
        resumeArmed = false;
        resumeSettlingUntil = resumed ? now + resumeSettlingWindow : DateTimeOffset.MinValue;
    }

    public void Reset()
    {
        previousInEarCount = null;
        resumeArmed = false;
        resumeSettlingUntil = DateTimeOffset.MinValue;
    }
}
