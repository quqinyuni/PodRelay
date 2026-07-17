namespace PodRelay.Core.Audio;

public static class BluetoothCallEndpointSelector
{
    public static BluetoothAudioEndpoint? Select(
        IReadOnlyCollection<BluetoothAudioEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints
            .Where(endpoint =>
                endpoint.Profile == BluetoothAudioProfile.HandsFree ||
                endpoint.FormFactor == AudioEndpointFormFactor.Headset)
            .OrderByDescending(endpoint => endpoint.IsActive)
            .ThenBy(endpoint => endpoint.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
