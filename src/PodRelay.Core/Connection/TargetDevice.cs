namespace PodRelay.Core.Connection;

public sealed record TargetDevice(
    string BluetoothAddress,
    Guid ContainerId,
    string DisplayName,
    ushort? AirPodsModelCode = null);

