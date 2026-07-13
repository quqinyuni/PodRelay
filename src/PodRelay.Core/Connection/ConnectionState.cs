namespace PodRelay.Core.Connection;

public enum ConnectionState
{
    Waiting,
    BluetoothOff,
    DeviceUnavailable,
    Connecting,
    AudioNotReady,
    Connected,
    CoolingDown,
    TimedOut,
    Failed,
    Cancelled
}
