namespace PodRelay.Core.Discovery;

public enum AirPodsWearState
{
    Unknown,
    InCase,
    OutOfCase,
    InEar
}

public sealed record AirPodsPublicStatus(
    ushort ModelCode,
    byte RawStatus,
    bool AnyPodInEar,
    bool HasCaseContext,
    AirPodsWearState WearState)
{
    public string ModelCodeHex => $"0x{ModelCode:X4}";
    public bool FirstPodInEar => (RawStatus & (1 << 1)) != 0;
    public bool SecondPodInEar => (RawStatus & (1 << 3)) != 0;
    public int InEarPodCount => (FirstPodInEar ? 1 : 0) + (SecondPodInEar ? 1 : 0);
}
