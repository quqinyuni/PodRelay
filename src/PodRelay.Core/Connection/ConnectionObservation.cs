namespace PodRelay.Core.Connection;

public sealed record ConnectionObservation(
    bool IsBluetoothOn,
    bool IsDeviceFound,
    bool IsDevicePresent,
    bool IsPaired,
    bool IsBluetoothConnected,
    string? StereoEndpointId,
    bool IsStereoEndpointActive,
    bool IsStereoDefaultForAllRoles)
{
    public bool IsFullyConnected =>
        IsBluetoothConnected && IsStereoEndpointActive && IsStereoDefaultForAllRoles;
}
