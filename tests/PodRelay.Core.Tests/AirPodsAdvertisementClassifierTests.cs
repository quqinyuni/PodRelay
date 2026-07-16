using PodRelay.Core.Discovery;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class AirPodsAdvertisementClassifierTests
{
    [Theory]
    [InlineData(0x19)]
    [InlineData(0x13)]
    public void AcceptsKnownAirPodsProximityFrames(byte lengthByte)
    {
        Assert.True(AirPodsAdvertisementClassifier.IsAirPodsFrame(
            AirPodsAdvertisementClassifier.AppleCompanyId,
            [0x07, lengthByte, 0x01, 0x14, 0x20, 0x00, 0x75, 0xAA, 0x30, 0x01, 0x00]));
    }

    [Fact]
    public void RejectsOtherAppleContinuityFrames()
    {
        Assert.False(AirPodsAdvertisementClassifier.IsAirPodsFrame(
            AirPodsAdvertisementClassifier.AppleCompanyId,
            [0x10, 0x05, 0x01]));
    }

    [Fact]
    public void RejectsNonAppleManufacturer()
    {
        Assert.False(AirPodsAdvertisementClassifier.IsAirPodsFrame(
            0x1234,
            [0x07, 0x19, 0x01]));
    }

    [Theory]
    [InlineData(0x04, AirPodsWearState.InCase)]
    [InlineData(0x40, AirPodsWearState.InCase)]
    [InlineData(0x00, AirPodsWearState.OutOfCase)]
    [InlineData(0x02, AirPodsWearState.InEar)]
    [InlineData(0x08, AirPodsWearState.InEar)]
    [InlineData(0x12, AirPodsWearState.InEar)]
    public void DecodesPublicWearState(byte rawStatus, AirPodsWearState expected)
    {
        var decoded = AirPodsAdvertisementClassifier.TryDecodePublicStatus(
            AirPodsAdvertisementClassifier.AppleCompanyId,
            [0x07, 0x19, 0x01, 0x14, 0x20, rawStatus, 0x75, 0xAA, 0x30, 0x01, 0x00],
            out var status);

        Assert.True(decoded);
        Assert.NotNull(status);
        Assert.Equal(0x1420, status.ModelCode);
        Assert.Equal(expected, status.WearState);
    }

    [Theory]
    [InlineData(0x00, 0)]
    [InlineData(0x02, 1)]
    [InlineData(0x08, 1)]
    [InlineData(0x0A, 2)]
    [InlineData(0x0B, 2)]
    public void DecodesIndependentInEarBits(byte rawStatus, int expectedCount)
    {
        AirPodsAdvertisementClassifier.TryDecodePublicStatus(
            AirPodsAdvertisementClassifier.AppleCompanyId,
            [0x07, 0x19, 0x01, 0x24, 0x20, rawStatus, 0x75, 0xAA, 0x30, 0x01, 0x00],
            out var status);

        Assert.NotNull(status);
        Assert.Equal(expectedCount, status.InEarPodCount);
    }

    [Theory]
    [InlineData("AirPods Pro2", 0x1420, true)]
    [InlineData("AirPods Pro2", 0x2420, true)]
    [InlineData("AirPods Pro2", 0x0E20, false)]
    [InlineData("AirPods Pro", 0x0E20, true)]
    public void FiltersLikelyTargetModel(string name, ushort modelCode, bool expected)
    {
        Assert.Equal(expected, AirPodsAdvertisementClassifier.IsLikelyTargetModel(name, modelCode));
    }

    [Theory]
    [InlineData(0x2420, true)]
    [InlineData(0x1420, false)]
    public void BoundModelRejectsAnotherAirPodsPro2Variant(ushort observedModelCode, bool expected)
    {
        Assert.Equal(expected, AirPodsAdvertisementClassifier.IsLikelyTargetModel(
            "AirPods Pro2",
            observedModelCode,
            targetModelCode: 0x2420));
    }

    [Theory]
    [InlineData(AirPodsWearState.Unknown, AirPodsPresenceAction.Ignore)]
    [InlineData(AirPodsWearState.InCase, AirPodsPresenceAction.Ignore)]
    [InlineData(AirPodsWearState.OutOfCase, AirPodsPresenceAction.Prompt)]
    [InlineData(AirPodsWearState.InEar, AirPodsPresenceAction.AutoConnect)]
    public void PresencePolicyPreventsInCasePopupAndConnect(
        AirPodsWearState state,
        AirPodsPresenceAction expected)
    {
        Assert.Equal(expected, AirPodsPresencePolicy.Evaluate(state));
    }

    [Fact]
    public void PresenceSensitiveTriggersRequireFreshInEarEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 12, 18, 0, 0, TimeSpan.FromHours(8));

        Assert.True(AirPodsPresencePolicy.HasRecentInEarSignal(
            AirPodsWearState.InEar, now.AddSeconds(-29), now, TimeSpan.FromSeconds(30)));
        Assert.False(AirPodsPresencePolicy.HasRecentInEarSignal(
            AirPodsWearState.InEar, now.AddSeconds(-31), now, TimeSpan.FromSeconds(30)));
        Assert.False(AirPodsPresencePolicy.HasRecentInEarSignal(
            AirPodsWearState.InCase, now, now, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void DuplicateGateRaisesOnlyOncePerWindow()
    {
        var gate = new AdvertisementDuplicateGate(TimeSpan.FromSeconds(10));
        var first = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.True(gate.TryAccept(first));
        Assert.False(gate.TryAccept(first.AddSeconds(9)));
        Assert.True(gate.TryAccept(first.AddSeconds(10)));
    }

    [Fact]
    public void DuplicateGateAllowsImmediateWearStateChange()
    {
        var gate = new AdvertisementDuplicateGate(TimeSpan.FromSeconds(10));
        var first = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.True(gate.TryAccept(first, "1420:04"));
        Assert.False(gate.TryAccept(first.AddMilliseconds(100), "1420:04"));
        Assert.True(gate.TryAccept(first.AddMilliseconds(200), "1420:02"));
    }

    [Fact]
    public void NearbyPopupShowsOnlyOnceForContinuousDisconnectedAdvertisements()
    {
        var gate = new NearbyPopupGate(TimeSpan.FromSeconds(30));
        var first = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.True(gate.ShouldShow(first, isTargetConnected: false, isUserSuppressed: false));
        Assert.False(gate.ShouldShow(first.AddSeconds(10), false, false));
        Assert.False(gate.ShouldShow(first.AddSeconds(20), false, false));
        Assert.True(gate.ShouldShow(first.AddSeconds(51), false, false));
    }

    [Fact]
    public void NearbyPopupRespectsUserSuppressionAndResetsAfterConnectedState()
    {
        var gate = new NearbyPopupGate(TimeSpan.FromSeconds(30));
        var first = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.False(gate.ShouldShow(first, isTargetConnected: false, isUserSuppressed: true));
        Assert.True(gate.ShouldShow(first.AddSeconds(10), false, false));
        Assert.False(gate.ShouldShow(first.AddSeconds(20), true, false));
        Assert.True(gate.ShouldShow(first.AddSeconds(25), false, false));
    }
}
