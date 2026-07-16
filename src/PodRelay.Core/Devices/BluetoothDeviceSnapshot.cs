namespace PodRelay.Core.Devices;

public sealed record BluetoothDeviceSnapshot(
    string Id,
    string Name,
    ulong BluetoothAddress,
    Guid? ContainerId,
    bool IsPaired,
    bool IsConnected,
    bool? IsPresent,
    IReadOnlyDictionary<string, object?> Properties,
    ushort? AirPodsModelCode = null)
{
    public string FormattedAddress => string.Join(
        ":",
        Enumerable.Range(0, 6)
            .Reverse()
            .Select(offset => ((BluetoothAddress >> (offset * 8)) & 0xff).ToString("X2")));
}
