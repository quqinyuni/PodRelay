namespace PodRelay.Core.Connection;

public sealed record ConnectionObservation(
    bool IsBluetoothOn,
    bool IsDeviceFound,
    bool IsDevicePresent,
    bool IsPaired,
    bool IsBluetoothConnected,
    string? AudioEndpointId,
    bool IsAudioEndpointActive,
    bool IsAudioDefaultForAllRoles)
{
    public bool IsFullyConnected =>
        IsBluetoothConnected && IsAudioEndpointActive && IsAudioDefaultForAllRoles;
}
