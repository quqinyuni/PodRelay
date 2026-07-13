using PodRelay.Core.Connection;
using PodRelay.Core.Devices;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class BluetoothAudioCandidateSelectorTests
{
    private static readonly IReadOnlyDictionary<string, object?> NoProperties =
        new Dictionary<string, object?>();

    [Fact]
    public void SavedTargetRemainsWhenOnlyAnotherAudioEndpointIsCurrentlyVisible()
    {
        var airPodsContainer = Guid.NewGuid();
        var hecateContainer = Guid.NewGuid();
        var savedTarget = new TargetDevice("02:00:00:00:00:01", airPodsContainer, "AirPods Pro2");
        BluetoothDeviceSnapshot[] pairedDevices =
        [
            Device("AirPods Pro2", 0x40B3FA1C468E, airPodsContainer),
            Device("HECATE G4 S PRO", 0x52592000A7DF, hecateContainer)
        ];

        var choices = BluetoothAudioCandidateSelector.Select(
            pairedDevices,
            [hecateContainer],
            savedTarget);

        Assert.Equal(2, choices.Count);
        Assert.Equal(airPodsContainer, choices[0].ContainerId);
        Assert.Contains(choices, choice => choice.ContainerId == hecateContainer);
    }

    [Fact]
    public void SavedTargetGetsFallbackEntryWhenWindowsTemporarilyOmitsDevice()
    {
        var airPodsContainer = Guid.NewGuid();
        var savedTarget = new TargetDevice("02:00:00:00:00:01", airPodsContainer, "AirPods Pro2");

        var choices = BluetoothAudioCandidateSelector.Select([], [], savedTarget);

        var choice = Assert.Single(choices);
        Assert.Equal(airPodsContainer, choice.ContainerId);
        Assert.Contains("已保存", choice.Label);
    }

    private static BluetoothDeviceSnapshot Device(string name, ulong address, Guid containerId) =>
        new(
            $"Bluetooth#{address:X12}",
            name,
            address,
            containerId,
            IsPaired: true,
            IsConnected: false,
            IsPresent: true,
            NoProperties);
}
