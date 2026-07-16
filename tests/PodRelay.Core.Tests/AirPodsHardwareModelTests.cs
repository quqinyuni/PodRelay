using PodRelay.Core.Devices;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class AirPodsHardwareModelTests
{
    [Theory]
    [InlineData("BTHENUM\\{0000110B-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&2024", 0x2420)]
    [InlineData("bthenum\\service_vid&0001004c_pid&2014", 0x1420)]
    public void ConvertsAppleBluetoothProductIdToAdvertisementModelCode(
        string hardwareId,
        ushort expected)
    {
        Assert.Equal(expected, AirPodsHardwareModel.GetAdvertisementModelCode([hardwareId]));
    }

    [Theory]
    [InlineData("BTHENUM\\SERVICE_VID&0001006D_PID&2024")]
    [InlineData("BTHENUM\\SERVICE_VID&0001004C_PID&XYZ1")]
    public void RejectsNonAppleOrMalformedProductIds(string hardwareId)
    {
        Assert.Null(AirPodsHardwareModel.GetAdvertisementModelCode([hardwareId]));
    }
}
