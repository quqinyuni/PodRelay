using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Media.Devices;

namespace PodRelay.Core.Devices;

public sealed class WindowsDeviceDiscovery
{
    private static readonly string[] BluetoothProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPaired",
        "System.Devices.Aep.IsPresent",
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.Aep.SignalStrength",
        "System.Devices.Aep.ContainerId"
    ];

    private static readonly string[] AudioProperties =
    [
        "System.Devices.InterfaceEnabled",
        "System.Devices.InterfaceClassGuid",
        "System.Devices.DeviceInstanceId",
        "System.Devices.ContainerId"
    ];

    public async Task<RadioState?> GetBluetoothRadioStateAsync()
    {
        var radios = await Radio.GetRadiosAsync();
        return radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth)?.State;
    }

    public async Task<IReadOnlyList<BluetoothDeviceSnapshot>> GetPairedBluetoothDevicesAsync()
    {
        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var deviceInformation = await DeviceInformation.FindAllAsync(
            selector,
            BluetoothProperties,
            DeviceInformationKind.DeviceInterface);

        var snapshots = new List<BluetoothDeviceSnapshot>(deviceInformation.Count);
        foreach (var information in deviceInformation)
        {
            BluetoothDevice? device = null;
            try
            {
                device = await BluetoothDevice.FromIdAsync(information.Id);
                var properties = CopyProperties(information);

                snapshots.Add(new BluetoothDeviceSnapshot(
                    information.Id,
                    information.Name,
                    device?.BluetoothAddress ?? ParseBluetoothAddress(properties),
                    GetNullableGuid(properties, "System.Devices.Aep.ContainerId"),
                    information.Pairing.IsPaired,
                    device?.ConnectionStatus == BluetoothConnectionStatus.Connected || GetBoolean(properties, "System.Devices.Aep.IsConnected"),
                    GetNullableBoolean(properties, "System.Devices.Aep.IsPresent"),
                    properties));
            }
            catch (Exception exception)
            {
                var properties = CopyProperties(information);
                properties["PodRelay.EnumerationError"] = exception.Message;

                snapshots.Add(new BluetoothDeviceSnapshot(
                    information.Id,
                    information.Name,
                    ParseBluetoothAddress(properties),
                    GetNullableGuid(properties, "System.Devices.Aep.ContainerId"),
                    information.Pairing.IsPaired,
                    GetBoolean(properties, "System.Devices.Aep.IsConnected"),
                    GetNullableBoolean(properties, "System.Devices.Aep.IsPresent"),
                    properties));
            }
            finally
            {
                device?.Dispose();
            }
        }

        return snapshots;
    }

    public async Task<IReadOnlyList<AudioEndpointSnapshot>> GetAudioRenderEndpointsAsync()
    {
        var endpoints = await DeviceInformation.FindAllAsync(
            MediaDevice.GetAudioRenderSelector(),
            AudioProperties,
            DeviceInformationKind.DeviceInterface);

        return endpoints.Select(endpoint =>
        {
            var properties = CopyProperties(endpoint);
            return new AudioEndpointSnapshot(
                endpoint.Id,
                endpoint.Name,
                endpoint.IsEnabled,
                null,
                GetString(properties, "System.Devices.DeviceInstanceId"),
                properties);
        }).ToArray();
    }

    private static ulong ParseBluetoothAddress(IReadOnlyDictionary<string, object?> properties)
    {
        var value = GetString(properties, "System.Devices.Aep.DeviceAddress");
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var compact = value.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return ulong.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out var address)
            ? address
            : 0;
    }

    private static bool GetBoolean(IReadOnlyDictionary<string, object?> properties, string key) =>
        GetNullableBoolean(properties, key) ?? false;

    private static bool? GetNullableBoolean(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties.TryGetValue(key, out var value) && value is bool boolean ? boolean : null;

    private static string? GetString(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static Guid? GetNullableGuid(IReadOnlyDictionary<string, object?> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            Guid guid => guid,
            string text when Guid.TryParse(text, out var guid) => guid,
            _ => null
        };
    }

    private static Dictionary<string, object?> CopyProperties(DeviceInformation information) =>
        information.Properties.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value);
}
