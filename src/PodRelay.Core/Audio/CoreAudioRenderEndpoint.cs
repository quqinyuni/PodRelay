namespace PodRelay.Core.Audio;

public sealed record CoreAudioRenderEndpoint(
    string Id,
    string Name,
    Guid ContainerId,
    string State,
    bool IsConsoleDefault,
    bool IsMultimediaDefault,
    bool IsCommunicationsDefault)
{
    public bool IsActive => State == "Active";
}

