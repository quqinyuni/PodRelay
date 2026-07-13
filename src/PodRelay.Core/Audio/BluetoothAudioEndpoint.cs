namespace PodRelay.Core.Audio;

public sealed record BluetoothAudioEndpoint(
    string Id,
    string Name,
    Guid ContainerId,
    bool IsActive,
    bool IsStereo,
    int? LastControlHResult = null);
