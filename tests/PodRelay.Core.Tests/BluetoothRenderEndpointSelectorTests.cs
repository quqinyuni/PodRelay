using PodRelay.Core.Audio;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class BluetoothRenderEndpointSelectorTests
{
    private static readonly Guid ContainerId = Guid.NewGuid();

    [Fact]
    public void Windows10PrefersA2dpOverActiveHandsFreeEndpoint()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("hands-free", active: true, BluetoothAudioProfile.HandsFree, AudioEndpointFormFactor.Headset),
            Endpoint("a2dp", active: false, BluetoothAudioProfile.A2dp, AudioEndpointFormFactor.Headphones)
        ];

        var selected = BluetoothRenderEndpointSelector.Select(endpoints);

        Assert.Equal("a2dp", selected?.Id);
    }

    [Fact]
    public void Windows11AcceptsSingleUnifiedEndpointWithoutStereoName()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            new(
                "unified",
                "耳机 (AirPods Pro2)",
                ContainerId,
                IsActive: true,
                BluetoothAudioProfile.A2dp,
                AudioEndpointFormFactor.Headphones)
        ];

        var selected = BluetoothRenderEndpointSelector.Select(endpoints);

        Assert.Equal("unified", selected?.Id);
    }

    [Fact]
    public void SingleVendorEndpointWorksWhenMetadataIsMissing()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint(
                "vendor-unified",
                active: false,
                BluetoothAudioProfile.Unknown,
                AudioEndpointFormFactor.Unknown)
        ];

        var selected = BluetoothRenderEndpointSelector.Select(endpoints);

        Assert.Equal("vendor-unified", selected?.Id);
    }

    [Fact]
    public void HeadphoneFormFactorIsLanguageIndependentFallback()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            new(
                "casque",
                "Casque (AirPods)",
                ContainerId,
                IsActive: false,
                BluetoothAudioProfile.Unknown,
                AudioEndpointFormFactor.Headphones),
            new(
                "mains-libres",
                "Kit mains libres (AirPods)",
                ContainerId,
                IsActive: true,
                BluetoothAudioProfile.Unknown,
                AudioEndpointFormFactor.Headset)
        ];

        var selected = BluetoothRenderEndpointSelector.Select(endpoints);

        Assert.Equal("casque", selected?.Id);
    }

    [Fact]
    public void AmbiguousInactiveVendorEndpointsAreNotGuessed()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("one", active: false, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Unknown),
            Endpoint("two", active: false, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Unknown)
        ];

        Assert.Null(BluetoothRenderEndpointSelector.Select(endpoints));
    }

    [Fact]
    public void ExactlyOneActiveVendorEndpointIsSafeFallback()
    {
        BluetoothAudioEndpoint[] endpoints =
        [
            Endpoint("inactive", active: false, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Unknown),
            Endpoint("active", active: true, BluetoothAudioProfile.Unknown, AudioEndpointFormFactor.Unknown)
        ];

        var selected = BluetoothRenderEndpointSelector.Select(endpoints);

        Assert.Equal("active", selected?.Id);
    }

    private static BluetoothAudioEndpoint Endpoint(
        string id,
        bool active,
        BluetoothAudioProfile profile,
        AudioEndpointFormFactor formFactor) =>
        new(id, id, ContainerId, active, profile, formFactor);
}
