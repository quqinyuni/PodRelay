namespace PodRelay.Core.Audio;

public sealed record CoreAudioCaptureEndpoint(
    string Id,
    string Name,
    Guid ContainerId,
    string State,
    bool IsConsoleDefault,
    bool IsMultimediaDefault,
    bool IsCommunicationsDefault,
    bool HasActiveSession)
{
    public bool IsActive => State == "Active";

    public bool IsDefaultForAllRoles =>
        IsConsoleDefault && IsMultimediaDefault && IsCommunicationsDefault;
}
