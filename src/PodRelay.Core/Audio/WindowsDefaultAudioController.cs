#nullable disable

using System.Runtime.InteropServices;
using PodRelay.Core.Devices;
using Vanara.PInvoke;
using static Vanara.PInvoke.CoreAudio;

namespace PodRelay.Core.Audio;

/// <summary>
/// Reads Core Audio render endpoints and changes the system defaults.
/// Windows exposes default selection to its own UI through the undocumented
/// IPolicyConfig COM contract, so the setter is kept isolated here.
/// </summary>
public sealed class WindowsDefaultAudioController
{
    public IReadOnlyList<CoreAudioRenderEndpoint> GetRenderEndpoints()
    {
        var results = new List<CoreAudioRenderEndpoint>();
        IMMDeviceEnumerator enumerator;
        IMMDeviceCollection collection;
        uint endpointCount;
        try
        {
            enumerator = new IMMDeviceEnumerator();
            collection = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATEMASK_ALL);
            endpointCount = collection.GetCount();
        }
        catch (Exception exception) when (WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
        {
            return results;
        }

        var defaults = new Dictionary<AudioRole, string>
        {
            [AudioRole.Console] = TryGetDefaultId(enumerator, ERole.eConsole),
            [AudioRole.Multimedia] = TryGetDefaultId(enumerator, ERole.eMultimedia),
            [AudioRole.Communications] = TryGetDefaultId(enumerator, ERole.eCommunications)
        };

        for (uint index = 0; index < endpointCount; index++)
        {
            try
            {
                collection.Item(index, out var endpoint);
                if (endpoint is null)
                {
                    continue;
                }

                var id = endpoint.GetId();
                var store = endpoint.OpenPropertyStore(STGM.STGM_READ);
                if (string.IsNullOrWhiteSpace(id) || store is null)
                {
                    continue;
                }

                var name = TryGetProperty(store, DevicePropertyKeys.FriendlyName)?.ToString() ?? id;
                var containerValue = TryGetProperty(store, Ole32.PROPERTYKEY.System.Devices.ContainerId);
                var containerId = containerValue is Guid guid ? guid : Guid.Empty;
                results.Add(new CoreAudioRenderEndpoint(
                    id,
                    name,
                    containerId,
                    FormatState(endpoint.GetState()),
                    id == defaults[AudioRole.Console],
                    id == defaults[AudioRole.Multimedia],
                    id == defaults[AudioRole.Communications]));
            }
            catch (Exception exception) when (WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
            {
                // Ignore a stale endpoint without hiding the remaining render devices.
            }
        }

        return results;
    }

    public void SetDefaultForAllRoles(string endpointId)
    {
        var endpoint = GetRenderEndpoints().SingleOrDefault(candidate => candidate.Id == endpointId)
            ?? throw new ArgumentException($"Unknown render endpoint '{endpointId}'.", nameof(endpointId));
        if (!endpoint.IsActive)
        {
            throw new InvalidOperationException($"Render endpoint '{endpoint.Name}' is not active.");
        }

        var policyType = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))
            ?? throw new InvalidOperationException("Windows did not expose the audio policy configuration class.");
        var policy = (IPolicyConfig)(Activator.CreateInstance(policyType)
            ?? throw new InvalidOperationException("Windows did not create the audio policy configuration object."));
        Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, AudioRole.Console));
        Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, AudioRole.Multimedia));
        Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, AudioRole.Communications));
    }

    private static string TryGetDefaultId(IMMDeviceEnumerator enumerator, ERole role)
    {
        try
        {
            return enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, role)?.GetId();
        }
        catch (Exception exception) when (
            exception is COMException || WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
        {
            return null;
        }
    }

    private static string FormatState(DEVICE_STATE state) => state switch
    {
        DEVICE_STATE.DEVICE_STATE_ACTIVE => "Active",
        DEVICE_STATE.DEVICE_STATE_DISABLED => "Disabled",
        DEVICE_STATE.DEVICE_STATE_NOTPRESENT => "NotPresent",
        DEVICE_STATE.DEVICE_STATE_UNPLUGGED => "Unplugged",
        _ => state.ToString()
    };

    private static object TryGetProperty(PropSys.IPropertyStore store, Ole32.PROPERTYKEY key)
    {
        try
        {
            return store.GetValue(key);
        }
        catch (Exception exception) when (
            exception is COMException || WindowsDeviceFailure.IsRemovedOrInvalidated(exception))
        {
            return null;
        }
    }

    private static class DevicePropertyKeys
    {
        public static readonly Ole32.PROPERTYKEY FriendlyName =
            new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14u);
    }

    private enum AudioRole : uint
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();

        [PreserveSig]
        int SetDefaultEndpoint(
            [In, MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [In, MarshalAs(UnmanagedType.U4)] AudioRole role);

        [PreserveSig] int SetEndpointVisibility();
    }
}
