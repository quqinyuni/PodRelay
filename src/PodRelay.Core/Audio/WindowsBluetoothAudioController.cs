#nullable disable

using System.Runtime.InteropServices;
using System.Security;
using Vanara.PInvoke;
using PodRelay.Core.Devices;
using static Vanara.PInvoke.CoreAudio;

namespace PodRelay.Core.Audio;

/// <summary>
/// Requests a one-shot reconnect from the Windows Bluetooth audio kernel-streaming driver.
/// The topology traversal is adapted from PolarGoose/BluetoothDevicePairing (MIT, 2020).
/// </summary>
public sealed class WindowsBluetoothAudioController
{
    public IReadOnlyList<BluetoothAudioEndpoint> GetAllEndpoints() =>
        EnumerateBluetoothAudioEndpoints()
            .Select(endpoint => endpoint.Snapshot)
            .ToArray();

    public IReadOnlyList<BluetoothAudioEndpoint> GetEndpoints(Guid containerId) =>
        GetAllEndpoints()
            .Where(endpoint => endpoint.ContainerId == containerId)
            .ToArray();

    public IReadOnlyList<BluetoothAudioEndpoint> RequestReconnect(Guid containerId)
        => RequestOneShot(containerId, BluetoothAudioProperty.OneShotReconnect);

    public IReadOnlyList<BluetoothAudioEndpoint> RequestDisconnect(Guid containerId)
        => RequestOneShot(containerId, BluetoothAudioProperty.OneShotDisconnect);

    private static IReadOnlyList<BluetoothAudioEndpoint> RequestOneShot(
        Guid containerId,
        BluetoothAudioProperty propertyId)
    {
        EndpointControl[] endpoints = [];
        for (var attempt = 0; attempt < 3; attempt++)
        {
            endpoints = EnumerateBluetoothAudioEndpoints()
                .Where(endpoint => endpoint.Snapshot.ContainerId == containerId)
                .ToArray();
            if (endpoints.Length > 0)
            {
                break;
            }

            if (attempt < 2)
            {
                Thread.Sleep(100);
            }
        }

        if (endpoints.Length == 0)
        {
            return [];
        }

        var preferred = endpoints.Where(endpoint => endpoint.Snapshot.IsStereo).ToArray();
        var selected = preferred.Length > 0 ? preferred : endpoints;

        return selected.Select(endpoint =>
        {
            var property = new KsProperty(
                KsPropertySet.BluetoothAudio,
                propertyId,
                KsPropertyKind.Get);
            var bytesReturned = 0;
            var hresult = endpoint.KsControl.KsProperty(
                ref property,
                Marshal.SizeOf<KsProperty>(),
                IntPtr.Zero,
                0,
                ref bytesReturned);
            return endpoint.Snapshot with { LastControlHResult = hresult };
        }).ToArray();
    }

    private static IReadOnlyList<EndpointControl> EnumerateBluetoothAudioEndpoints()
    {
        var results = new List<EndpointControl>();
        IMMDeviceEnumerator enumerator;
        IMMDeviceCollection endpoints;
        uint endpointCount;
        try
        {
            enumerator = new IMMDeviceEnumerator();
            endpoints = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATEMASK_ALL);
            endpointCount = endpoints.GetCount();
        }
        catch (Exception exception) when (WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
        {
            return results;
        }

        for (uint index = 0; index < endpointCount; index++)
        {
            try
            {
                endpoints.Item(index, out var endpoint);
                if (endpoint is null)
                {
                    continue;
                }

                var topology = Activate<IDeviceTopology>(endpoint);
                if (topology is null)
                {
                    continue;
                }

                for (uint connectorIndex = 0; connectorIndex < topology.GetConnectorCount(); connectorIndex++)
                {
                    var connector = topology.GetConnector(connectorIndex);
                    if (connector is null)
                    {
                        continue;
                    }

                    IPart connectedPart;
                    try
                    {
                        connectedPart = (IPart)connector.GetConnectedTo();
                    }
                    catch (COMException)
                    {
                        continue;
                    }

                    if (connectedPart is null || connectedPart.GetTopologyObject() is not { } connectedTopology)
                    {
                        continue;
                    }

                    var connectedDeviceId = (string)connectedTopology.GetDeviceId();
                    if (string.IsNullOrWhiteSpace(connectedDeviceId))
                    {
                        continue;
                    }
                    if (!connectedDeviceId.StartsWith(@"{2}.\\?\bth", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var connectedDevice = enumerator.GetDevice(connectedDeviceId);
                    if (connectedDevice is null)
                    {
                        continue;
                    }

                    var ksControl = Activate<IKsControl>(connectedDevice);
                    if (ksControl is null)
                    {
                        continue;
                    }

                    var propertyStore = endpoint.OpenPropertyStore(STGM.STGM_READ);
                    if (propertyStore is null)
                    {
                        continue;
                    }
                    var name = (string)propertyStore.GetValue(DevicePropertyKeys.FriendlyName);
                    var containerId = (Guid)propertyStore.GetValue(Ole32.PROPERTYKEY.System.Devices.ContainerId);
                    var snapshot = new BluetoothAudioEndpoint(
                        endpoint.GetId(),
                        name,
                        containerId,
                        endpoint.GetState() == DEVICE_STATE.DEVICE_STATE_ACTIVE,
                        name.Contains("Stereo", StringComparison.OrdinalIgnoreCase));

                    results.Add(new EndpointControl(snapshot, ksControl));
                }
            }
            catch (Exception exception) when (WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
            {
                // DEVICE_STATEMASK_ALL intentionally includes unplugged and cached
                // endpoints. Windows can remove one between collection enumeration and
                // topology/property access; skip only that stale entry.
            }
        }

        return results;
    }

    private static T Activate<T>(IMMDevice device) where T : class
    {
        try
        {
            device.Activate(typeof(T).GUID, Ole32.CLSCTX.CLSCTX_ALL, null, out var instance);
            return instance as T;
        }
        catch (COMException exception) when (
            exception.HResult == unchecked((int)0x80004002) ||
            exception.HResult == unchecked((int)0x80070032))
        {
            // E_NOINTERFACE / ERROR_NOT_SUPPORTED: this render endpoint is not
            // backed by the Bluetooth kernel-streaming topology we need.
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private sealed record EndpointControl(BluetoothAudioEndpoint Snapshot, IKsControl KsControl);

    private static class DevicePropertyKeys
    {
        public static readonly Ole32.PROPERTYKEY FriendlyName =
            new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14u);
    }

    [Flags]
    private enum KsPropertyKind : uint
    {
        Get = 0x00000001
    }

    private enum BluetoothAudioProperty : uint
    {
        OneShotReconnect = 0,
        OneShotDisconnect = 1
    }

    private static class KsPropertySet
    {
        public static readonly Guid BluetoothAudio = new("7fa06c40-b8f6-4c7e-8556-e8c33a12e54d");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KsProperty(Guid set, BluetoothAudioProperty id, KsPropertyKind flags)
    {
        public Guid Set = set;
        public BluetoothAudioProperty Id = id;
        public KsPropertyKind Flags = flags;
    }

    [ComImport]
    [SuppressUnmanagedCodeSecurity]
    [Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IKsControl
    {
        [PreserveSig]
        int KsProperty(
            [In] ref KsProperty property,
            [In] int propertyLength,
            [In, Out] IntPtr propertyData,
            [In] int dataLength,
            [In, Out] ref int bytesReturned);

        [PreserveSig]
        int KsMethod(
            [In] ref KsProperty method,
            [In] int methodLength,
            [In, Out] IntPtr methodData,
            [In] int dataLength,
            [In, Out] ref int bytesReturned);

        [PreserveSig]
        int KsEvent(
            [In, Optional] ref KsProperty @event,
            [In] int eventLength,
            [In, Out] IntPtr eventData,
            [In] int dataLength,
            [In, Out] ref int bytesReturned);
    }
}
