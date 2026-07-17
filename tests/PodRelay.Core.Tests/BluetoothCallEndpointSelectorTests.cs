using PodRelay.Core.Audio;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class BluetoothCallEndpointSelectorTests
{
    private static readonly Guid ContainerId = Guid.NewGuid();

    [Fact]
    public void SelectsHandsFreeProfileInsteadOfA2dp()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("a2dp", true, BluetoothAudioProfile.A2dp, AudioEndpointFormFactor.Headphones),
            Endpoint("hands-free", true, BluetoothAudioProfile.HandsFree, AudioEndpointFormFactor.Headset)
        ];

        var selected = BluetoothCallEndpointSelector.Select(endpoints);

        Assert.Equal("hands-free", selected?.Id);
    }

    [Fact]
    public void HeadsetFormFactorIsLanguageIndependentFallback()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("headphones", true, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Headphones),
            Endpoint("headset", true, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Headset)
        ];

        Assert.Equal("headset", BluetoothCallEndpointSelector.Select(endpoints)?.Id);
    }

    [Fact]
    public void UnifiedHighQualityEndpointIsNotMistakenForSeparateCallEndpoint()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("unified", true, BluetoothAudioProfile.A2dp, AudioEndpointFormFactor.Headphones)
        ];

        Assert.Null(BluetoothCallEndpointSelector.Select(endpoints));
    }

    private static BluetoothAudioEndpoint Endpoint(
        string id,
        bool active,
        BluetoothAudioProfile profile,
        AudioEndpointFormFactor formFactor) =>
        new(id, id, ContainerId, active, profile, formFactor);
}
