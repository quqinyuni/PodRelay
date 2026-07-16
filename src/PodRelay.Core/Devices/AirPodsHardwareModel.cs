using System.Globalization;

namespace PodRelay.Core.Devices;

public static class AirPodsHardwareModel
{
    private const string AppleProductIdMarker = "VID&0001004C_PID&";

    public static ushort? GetAdvertisementModelCode(IEnumerable<string> hardwareIds)
    {
        foreach (var hardwareId in hardwareIds)
        {
            var markerIndex = hardwareId.IndexOf(
                AppleProductIdMarker,
                StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var productIdStart = markerIndex + AppleProductIdMarker.Length;
            if (hardwareId.Length < productIdStart + 4 ||
                !ushort.TryParse(
                    hardwareId.AsSpan(productIdStart, 4),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var productId))
            {
                continue;
            }

            // Windows Bluetooth SDP hardware IDs print the product ID in host byte
            // order (for example 2024), while the Proximity Pairing frame carries
            // the same two bytes in network order (2420).
            return (ushort)((productId >> 8) | (productId << 8));
        }

        return null;
    }
}
