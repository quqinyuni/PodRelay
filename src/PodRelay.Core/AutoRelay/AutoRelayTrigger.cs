namespace PodRelay.Core.AutoRelay;

public enum AutoRelayTrigger
{
    Manual,
    ApplicationStarted,
    SessionUnlocked,
    TargetSeen,
    ControllerConnected,
    NoActiveAudioOutput,
    ConnectionLost
}
