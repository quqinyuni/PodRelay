namespace PodRelay.Core.Audio;

public sealed record BluetoothAudioEndpoint(
    string Id,
    string Name,
    Guid ContainerId,
    bool IsActive,
    BluetoothAudioProfile Profile,
    AudioEndpointFormFactor FormFactor,
    int? LastControlHResult = null)
{
    public bool IsHighQualityRender =>
        Profile == BluetoothAudioProfile.A2dp ||
        FormFactor == AudioEndpointFormFactor.Headphones;
}

public enum BluetoothAudioProfile
{
    Unknown,
    A2dp,
    HandsFree
}

// Values are defined by the Windows EndpointFormFactor enumeration.
public enum AudioEndpointFormFactor
{
    RemoteNetworkDevice = 0,
    Speakers = 1,
    LineLevel = 2,
    Headphones = 3,
    Microphone = 4,
    Headset = 5,
    Handset = 6,
    UnknownDigitalPassthrough = 7,
    Spdif = 8,
    DigitalAudioDisplayDevice = 9,
    Unknown = 10
}
