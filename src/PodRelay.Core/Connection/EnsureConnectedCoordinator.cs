using System.Diagnostics;

namespace PodRelay.Core.Connection;

public sealed class EnsureConnectedCoordinator
{
    private readonly object sync = new();
    private readonly IConnectionPlatform platform;
    private readonly TimeSpan timeout;
    private readonly TimeSpan pollInterval;
    private Task<EnsureConnectionResult>? inFlight;

    public EnsureConnectedCoordinator(
        IConnectionPlatform platform,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        this.platform = platform;
        this.timeout = timeout ?? TimeSpan.FromSeconds(15);
        this.pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public event EventHandler<ConnectionState>? StateChanged;

    public Task<EnsureConnectionResult> EnsureConnectedAsync(
        TargetDevice target,
        CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            if (inFlight is not null)
            {
                return inFlight;
            }

            var completion = new TaskCompletionSource<EnsureConnectionResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = completion.Task;
            inFlight = operation;
            _ = ExecuteAndCompleteAsync(target, cancellationToken, completion);
            return operation;
        }
    }

    private async Task ExecuteAndCompleteAsync(
        TargetDevice target,
        CancellationToken cancellationToken,
        TaskCompletionSource<EnsureConnectionResult> completion)
    {
        var attempts = new List<ConnectionAttempt>();
        try
        {
            completion.TrySetResult(await ExecuteCoreAsync(target, cancellationToken, attempts));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            OnStateChanged(ConnectionState.Cancelled);
            completion.TrySetResult(new EnsureConnectionResult(
                ConnectionState.Cancelled,
                "Connection was cancelled.",
                null,
                null,
                TimeSpan.Zero,
                attempts));
        }
        catch (Exception exception)
        {
            OnStateChanged(ConnectionState.Failed);
            completion.TrySetResult(new EnsureConnectionResult(
                ConnectionState.Failed,
                exception.Message,
                null,
                null,
                TimeSpan.Zero,
                attempts));
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(inFlight, completion.Task))
                {
                    inFlight = null;
                }
            }
        }
    }

    private async Task<EnsureConnectionResult> ExecuteCoreAsync(
        TargetDevice target,
        CancellationToken cancellationToken,
        List<ConnectionAttempt> attempts)
    {
        var stopwatch = Stopwatch.StartNew();
        var observation = await platform.ObserveAsync(target, cancellationToken);
        attempts.Add(new ConnectionAttempt(
            "ObserveWindowsState",
            "Completed",
            DescribeObservation(observation),
            stopwatch.Elapsed));
        if (!observation.IsBluetoothOn)
        {
            return Result(ConnectionState.BluetoothOff, "Windows Bluetooth is off.", observation, null, stopwatch, attempts);
        }

        if (!observation.IsDeviceFound || !observation.IsPaired)
        {
            return Result(ConnectionState.DeviceUnavailable, "The selected paired device is unavailable.", observation, null, stopwatch, attempts);
        }

        if (observation.IsBluetoothConnected && observation.IsStereoEndpointActive)
        {
            attempts.Add(new ConnectionAttempt(
                "UseExistingConnection",
                "Succeeded",
                "Bluetooth and the Stereo endpoint were already active; reconnect was skipped.",
                stopwatch.Elapsed));
            return await SelectAndVerifyAsync(target, observation, null, stopwatch, attempts, cancellationToken);
        }

        OnStateChanged(ConnectionState.Connecting);
        var reconnect = await platform.RequestReconnectAsync(target, cancellationToken);
        attempts.Add(new ConnectionAttempt(
            "RequestBluetoothAudioReconnect",
            reconnect.WasAccepted ? "Accepted" : "Rejected",
            $"Requested {reconnect.RequestedEndpointCount} Bluetooth audio endpoint(s).",
            stopwatch.Elapsed,
            reconnect.HResults));
        if (!reconnect.WasAccepted)
        {
            return Result(ConnectionState.Failed, "Windows rejected the Bluetooth audio reconnect request.", observation, reconnect, stopwatch, attempts);
        }

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(pollInterval, cancellationToken);
            observation = await platform.ObserveAsync(target, cancellationToken);

            if (observation.IsBluetoothConnected && observation.IsStereoEndpointActive)
            {
                attempts.Add(new ConnectionAttempt(
                    "WaitForStereoEndpoint",
                    "Succeeded",
                    "Bluetooth connected and the Stereo render endpoint became ACTIVE.",
                    stopwatch.Elapsed));
                return await SelectAndVerifyAsync(target, observation, reconnect, stopwatch, attempts, cancellationToken);
            }

            if (observation.IsBluetoothConnected)
            {
                OnStateChanged(ConnectionState.AudioNotReady);
            }
        }

        attempts.Add(new ConnectionAttempt(
            "WaitForStereoEndpoint",
            "TimedOut",
            DescribeObservation(observation),
            stopwatch.Elapsed));
        return Result(
            observation.IsBluetoothConnected ? ConnectionState.AudioNotReady : ConnectionState.TimedOut,
            observation.IsBluetoothConnected
                ? "Bluetooth connected, but the stereo audio endpoint did not become active."
                : "Connection timed out. The earbuds may be asleep, in the closed case, or in use by another device; retry is safe.",
            observation,
            reconnect,
            stopwatch,
            attempts);
    }

    private async Task<EnsureConnectionResult> SelectAndVerifyAsync(
        TargetDevice target,
        ConnectionObservation observation,
        ReconnectRequestResult? reconnect,
        Stopwatch stopwatch,
        List<ConnectionAttempt> attempts,
        CancellationToken cancellationToken)
    {
        if (observation.StereoEndpointId is null)
        {
            return Result(ConnectionState.AudioNotReady, "No stereo render endpoint was found.", observation, reconnect, stopwatch, attempts);
        }

        if (!observation.IsStereoDefaultForAllRoles)
        {
            await platform.SetDefaultOutputAsync(observation.StereoEndpointId, cancellationToken);
            attempts.Add(new ConnectionAttempt(
                "SelectDefaultStereoOutput",
                "Requested",
                "Selected the AirPods Stereo endpoint for console, multimedia, and communications roles.",
                stopwatch.Elapsed));
            observation = await platform.ObserveAsync(target, cancellationToken);
        }
        else
        {
            attempts.Add(new ConnectionAttempt(
                "SelectDefaultStereoOutput",
                "Skipped",
                "The AirPods Stereo endpoint was already default for all output roles.",
                stopwatch.Elapsed));
        }

        if (!observation.IsFullyConnected)
        {
            return Result(ConnectionState.AudioNotReady, "The stereo endpoint is active but output routing could not be verified.", observation, reconnect, stopwatch, attempts);
        }

        attempts.Add(new ConnectionAttempt(
            "VerifySuccessInvariant",
            "Succeeded",
            DescribeObservation(observation),
            stopwatch.Elapsed));
        return Result(ConnectionState.Connected, $"{target.DisplayName} is connected and selected for audio.", observation, reconnect, stopwatch, attempts);
    }

    private EnsureConnectionResult Result(
        ConnectionState state,
        string message,
        ConnectionObservation observation,
        ReconnectRequestResult? reconnect,
        Stopwatch stopwatch,
        IReadOnlyList<ConnectionAttempt> attempts)
    {
        OnStateChanged(state);
        return new EnsureConnectionResult(state, message, observation, reconnect, stopwatch.Elapsed, attempts.ToArray());
    }

    private static string DescribeObservation(ConnectionObservation observation) =>
        $"BluetoothOn={observation.IsBluetoothOn}; DeviceFound={observation.IsDeviceFound}; " +
        $"Present={observation.IsDevicePresent}; Paired={observation.IsPaired}; " +
        $"BluetoothConnected={observation.IsBluetoothConnected}; StereoActive={observation.IsStereoEndpointActive}; " +
        $"StereoDefaultAllRoles={observation.IsStereoDefaultForAllRoles}.";

    private void OnStateChanged(ConnectionState state) => StateChanged?.Invoke(this, state);
}
