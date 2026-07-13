using PodRelay.Core.Connection;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class EnsureConnectedCoordinatorTests
{
    private static readonly TargetDevice Target =
        new("02:00:00:00:00:01", Guid.NewGuid(), "AirPods Pro2");

    [Fact]
    public async Task ConcurrentCallsShareOneReconnectOperation()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: false, active: false, isDefault: false),
            BlockReconnect = true
        };
        var coordinator = new EnsureConnectedCoordinator(
            platform,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));

        var first = coordinator.EnsureConnectedAsync(Target);
        var second = coordinator.EnsureConnectedAsync(Target);

        Assert.Same(first, second);
        platform.Observation = Observation(connected: true, active: true, isDefault: false);
        platform.ReleaseReconnect();
        var result = await first;

        Assert.True(result.IsSuccess);
        Assert.Equal(1, platform.ReconnectCalls);
        Assert.Equal(1, platform.SetDefaultCalls);
    }

    [Fact]
    public async Task FullyConnectedCallIsIdempotent()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: true, active: true, isDefault: true)
        };
        var coordinator = new EnsureConnectedCoordinator(platform);

        var result = await coordinator.EnsureConnectedAsync(Target);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, platform.ReconnectCalls);
        Assert.Equal(0, platform.SetDefaultCalls);
        Assert.Contains(result.Attempts!, attempt => attempt.Stage == "UseExistingConnection");
        Assert.Contains(result.Attempts!, attempt => attempt.Stage == "VerifySuccessInvariant");
    }

    [Fact]
    public async Task ConnectedEndpointIsSelectedWhenRoutingIsWrong()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: true, active: true, isDefault: false)
        };
        var coordinator = new EnsureConnectedCoordinator(platform);

        var result = await coordinator.EnsureConnectedAsync(Target);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, platform.ReconnectCalls);
        Assert.Equal(1, platform.SetDefaultCalls);
    }

    [Fact]
    public async Task BluetoothOffReturnsSpecificStateWithoutReconnect()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: false, active: false, isDefault: false) with
            {
                IsBluetoothOn = false
            }
        };
        var coordinator = new EnsureConnectedCoordinator(platform);

        var result = await coordinator.EnsureConnectedAsync(Target);

        Assert.Equal(ConnectionState.BluetoothOff, result.State);
        Assert.Equal(0, platform.ReconnectCalls);
    }

    [Fact]
    public async Task ConnectedWithoutStereoTimesOutAsAudioNotReady()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: true, active: false, isDefault: false)
        };
        var coordinator = new EnsureConnectedCoordinator(
            platform,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(2));

        var result = await coordinator.EnsureConnectedAsync(Target);

        Assert.Equal(ConnectionState.AudioNotReady, result.State);
        Assert.Equal(1, platform.ReconnectCalls);
    }

    [Fact]
    public async Task DisconnectedDeviceTimesOutWithRetryableOccupiedHintAndAttemptHistory()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: false, active: false, isDefault: false)
        };
        var coordinator = new EnsureConnectedCoordinator(
            platform,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(2));

        var result = await coordinator.EnsureConnectedAsync(Target);

        Assert.Equal(ConnectionState.TimedOut, result.State);
        Assert.Contains("another device", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Attempts!, attempt =>
            attempt.Stage == "RequestBluetoothAudioReconnect" && attempt.Outcome == "Accepted");
        Assert.Contains(result.Attempts!, attempt =>
            attempt.Stage == "WaitForStereoEndpoint" && attempt.Outcome == "TimedOut");
    }

    [Fact]
    public async Task CancellationStopsAnInFlightReconnectAndAllowsRetry()
    {
        var platform = new FakePlatform
        {
            Observation = Observation(connected: false, active: false, isDefault: false),
            BlockReconnect = true
        };
        var coordinator = new EnsureConnectedCoordinator(platform);
        using var cancellation = new CancellationTokenSource();

        var cancelledCall = coordinator.EnsureConnectedAsync(Target, cancellation.Token);
        cancellation.Cancel();
        var cancelledResult = await cancelledCall;

        Assert.Equal(ConnectionState.Cancelled, cancelledResult.State);

        platform.ReleaseReconnect();
        platform.Observation = Observation(connected: true, active: true, isDefault: true);
        var retryResult = await coordinator.EnsureConnectedAsync(Target);

        Assert.True(retryResult.IsSuccess);
    }

    private static ConnectionObservation Observation(bool connected, bool active, bool isDefault) =>
        new(
            IsBluetoothOn: true,
            IsDeviceFound: true,
            IsDevicePresent: true,
            IsPaired: true,
            IsBluetoothConnected: connected,
            StereoEndpointId: "stereo-endpoint",
            IsStereoEndpointActive: active,
            IsStereoDefaultForAllRoles: isDefault);

    private sealed class FakePlatform : IConnectionPlatform
    {
        private readonly TaskCompletionSource reconnectRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public required ConnectionObservation Observation { get; set; }
        public bool BlockReconnect { get; init; }
        public int ReconnectCalls { get; private set; }
        public int SetDefaultCalls { get; private set; }

        public Task<ConnectionObservation> ObserveAsync(TargetDevice target, CancellationToken cancellationToken) =>
            Task.FromResult(Observation);

        public async Task<ReconnectRequestResult> RequestReconnectAsync(
            TargetDevice target,
            CancellationToken cancellationToken)
        {
            ReconnectCalls++;
            if (BlockReconnect)
            {
                await reconnectRelease.Task.WaitAsync(cancellationToken);
            }

            return new ReconnectRequestResult(1, [0]);
        }

        public Task SetDefaultOutputAsync(string endpointId, CancellationToken cancellationToken)
        {
            SetDefaultCalls++;
            Observation = Observation with { IsStereoDefaultForAllRoles = true };
            return Task.CompletedTask;
        }

        public void ReleaseReconnect() => reconnectRelease.TrySetResult();
    }
}
