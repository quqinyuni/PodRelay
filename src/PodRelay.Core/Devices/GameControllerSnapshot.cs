namespace PodRelay.Core.Devices;

public sealed record GameControllerSnapshot(
    string Id,
    string DisplayName,
    ushort VendorId,
    ushort ProductId,
    bool IsWireless)
{
    public string Label => $"{DisplayName}  ·  VID {VendorId:X4} / PID {ProductId:X4}";
}
