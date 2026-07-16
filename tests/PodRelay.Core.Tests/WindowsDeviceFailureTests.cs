using System.Runtime.InteropServices;
using PodRelay.Core.Devices;
using Xunit;

namespace PodRelay.Core.Tests;

public sealed class WindowsDeviceFailureTests
{
    [Theory]
    [InlineData(unchecked((int)0x80070002))]
    [InlineData(unchecked((int)0x8007048F))]
    [InlineData(unchecked((int)0x80070490))]
    [InlineData(unchecked((int)0x80010108))]
    [InlineData(unchecked((int)0x88890004))]
    public void RemovedOrInvalidatedEndpointErrorsAreRecoverable(int hresult)
    {
        var exception = new COMException("Endpoint disappeared.", hresult);

        Assert.True(WindowsDeviceFailure.IsRemovedOrInvalidated(exception));
    }

    [Fact]
    public void WrappedFileNotFoundErrorIsRecoverable()
    {
        var exception = new InvalidOperationException(
            "Outer failure.",
            new FileNotFoundException("Endpoint disappeared."));

        Assert.True(WindowsDeviceFailure.IsRemovedOrInvalidated(exception));
    }

    [Fact]
    public void ProgrammingErrorsAreNotHidden()
    {
        Assert.False(WindowsDeviceFailure.IsRemovedOrInvalidated(
            new InvalidOperationException("Unexpected failure.")));
    }
}
