namespace PodRelay.Core.Audio;

public static class BluetoothRenderEndpointSelector
{
    public static BluetoothAudioEndpoint? Select(
        IReadOnlyCollection<BluetoothAudioEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count == 0)
        {
            return null;
        }

        // Windows 10 normally exposes separate A2DP and hands-free endpoints.
        // Windows 11 normally exposes one unified render endpoint. Transport
        // metadata and form factor are stable across display languages, unlike
        // endpoint friendly names such as "Stereo" or "耳机".
        var highQuality = endpoints
            .Where(endpoint => endpoint.IsHighQualityRender)
            .OrderByDescending(endpoint => endpoint.IsActive)
            .ThenBy(endpoint => endpoint.Id, StringComparer.Ordinal)
            .ToArray();
        if (highQuality.Length > 0)
        {
            return highQuality[0];
        }

        if (endpoints.Count == 1)
        {
            return endpoints.First();
        }

        // Vendor drivers sometimes omit both profile and form-factor metadata.
        // An unambiguous active endpoint is still safe to observe and route.
        var active = endpoints.Where(endpoint => endpoint.IsActive).ToArray();
        return active.Length == 1 ? active[0] : null;
    }
}
