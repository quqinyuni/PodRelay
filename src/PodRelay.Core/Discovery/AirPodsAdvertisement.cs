namespace PodRelay.Core.Discovery;

public sealed record AirPodsAdvertisement(
    ulong BluetoothAddress,
    short SignalStrengthDbm,
    byte[] ManufacturerPayload,
    AirPodsPublicStatus PublicStatus,
    DateTimeOffset ReceivedAt);
