namespace PodRelay.Core.Devices;

internal static class WindowsDeviceFailure
{
    private static readonly HashSet<int> RecoverableHResults =
    [
        unchecked((int)0x80070002), // ERROR_FILE_NOT_FOUND
        unchecked((int)0x8007048F), // ERROR_DEVICE_NOT_CONNECTED
        unchecked((int)0x80070490), // E_NOTFOUND / ERROR_NOT_FOUND
        unchecked((int)0x80010108), // RPC_E_DISCONNECTED
        unchecked((int)0x88890004)  // AUDCLNT_E_DEVICE_INVALIDATED
    ];

    public static bool IsRemovedOrInvalidated(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (RecoverableHResults.Contains(current.HResult))
            {
                return true;
            }
        }

        return false;
    }
}
