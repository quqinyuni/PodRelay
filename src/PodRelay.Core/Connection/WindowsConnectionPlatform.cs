using PodRelay.Core.Audio;
using PodRelay.Core.Devices;
using Windows.Devices.Radios;

namespace PodRelay.Core.Connection;

public sealed class WindowsConnectionPlatform : IConnectionPlatform
{
    private readonly WindowsDeviceDiscovery discovery = new();
    private readonly WindowsBluetoothAudioController bluetoothAudio = new();
    private readonly WindowsDefaultAudioController defaultAudio = new();

    public async Task<ConnectionObservation> ObserveAsync(
        TargetDevice target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var radioState = await discovery.GetBluetoothRadioStateAsync();
        var devices = await discovery.GetPairedBluetoothDevicesAsync();
        var device = devices.SingleOrDefault(candidate =>
            candidate.FormattedAddress.Equals(target.BluetoothAddress, StringComparison.OrdinalIgnoreCase));
        var audioEndpoint = BluetoothRenderEndpointSelector.Select(
            bluetoothAudio.GetEndpoints(target.ContainerId));
        var routedAudioEndpoint = audioEndpoint is null
            ? null
            : defaultAudio.GetRenderEndpoints().SingleOrDefault(endpoint => endpoint.Id == audioEndpoint.Id);

        return new ConnectionObservation(
            radioState == RadioState.On,
            device is not null,
            device?.IsPresent == true,
            device?.IsPaired == true,
            device?.IsConnected == true,
            audioEndpoint?.Id,
            audioEndpoint?.IsActive == true,
            routedAudioEndpoint is not null &&
                routedAudioEndpoint.IsConsoleDefault &&
                routedAudioEndpoint.IsMultimediaDefault &&
                routedAudioEndpoint.IsCommunicationsDefault);
    }

    public Task<ReconnectRequestResult> RequestReconnectAsync(
        TargetDevice target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = bluetoothAudio.RequestReconnect(target.ContainerId);
        return Task.FromResult(new ReconnectRequestResult(
            requested.Count,
            requested.Select(endpoint => endpoint.LastControlHResult ?? int.MinValue).ToArray()));
    }

    public Task SetDefaultOutputAsync(string endpointId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        defaultAudio.SetDefaultForAllRoles(endpointId);
        return Task.CompletedTask;
    }
}
