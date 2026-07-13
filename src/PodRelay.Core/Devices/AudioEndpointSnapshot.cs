namespace PodRelay.Core.Devices;

public sealed record AudioEndpointSnapshot(
    string Id,
    string Name,
    bool IsEnabled,
    bool? IsPresent,
    string? DeviceInstanceId,
    IReadOnlyDictionary<string, object?> Properties);

