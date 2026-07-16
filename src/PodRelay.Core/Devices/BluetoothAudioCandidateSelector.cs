using PodRelay.Core.Connection;

namespace PodRelay.Core.Devices;

public sealed record BluetoothAudioCandidate(
    string Name,
    string Address,
    Guid ContainerId,
    string Label,
    ushort? AirPodsModelCode = null);

public static class BluetoothAudioCandidateSelector
{
    public static IReadOnlyList<BluetoothAudioCandidate> Select(
        IEnumerable<BluetoothDeviceSnapshot> devices,
        IEnumerable<Guid> knownAudioContainers,
        TargetDevice? savedTarget)
    {
        var audioContainers = knownAudioContainers.ToHashSet();
        if (savedTarget is not null)
        {
            audioContainers.Add(savedTarget.ContainerId);
        }

        var choices = devices
            .Where(device =>
                device.IsPaired &&
                device.ContainerId is not null &&
                (audioContainers.Contains(device.ContainerId.Value) ||
                 LooksLikeAudioDevice(device.Name)))
            .OrderByDescending(device => device.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
            .ThenBy(device => device.Name)
            .Select(device => new BluetoothAudioCandidate(
                device.Name,
                device.FormattedAddress,
                device.ContainerId!.Value,
                $"{device.Name}  ·  {device.FormattedAddress}",
                device.AirPodsModelCode))
            .ToList();

        if (savedTarget is not null && choices.All(choice => choice.ContainerId != savedTarget.ContainerId))
        {
            choices.Insert(0, new BluetoothAudioCandidate(
                savedTarget.DisplayName,
                savedTarget.BluetoothAddress,
                savedTarget.ContainerId,
                $"{savedTarget.DisplayName}  ·  {savedTarget.BluetoothAddress}  ·  已保存",
                savedTarget.AirPodsModelCode));
        }

        return choices;
    }

    private static bool LooksLikeAudioDevice(string name)
    {
        string[] keywords =
        [
            "AirPods", "Earbuds", "Earphones", "Headphones", "Headset", "Buds",
            "耳机", "耳麦", "MOONDROP", "Bose", "WH-", "WF-"
        ];
        return keywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
