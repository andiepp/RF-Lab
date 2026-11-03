namespace RfLab.Core.Tests;

public sealed class McnfStandardRequestsTests
{
    [Fact]
    public void NodeTypeInfoRequest_MatchesCharacterizedFrame()
    {
        var frame = McnfNodeAdminRequests.CreateNodeTypeInfoRequest();
        Assert.Equal(new byte[] { 0xA4, 0x02, 0x06, 0x00, 0xDC, 0x77 }, frame.Bytes.ToArray());
        Assert.Equal(6, frame.ResponseLength);
    }

    [Fact]
    public void BufferSizeRequest_MatchesCharacterizedFrame()
    {
        var frame = McnfNodeAdminRequests.CreateBufferSizeRequest();
        Assert.Equal(0xA4, frame.Channel);
        Assert.Equal(221, frame.Function);
        Assert.Equal(3, frame.ResponseLength);
    }

    [Fact]
    public void ErrorStatusRequest_MatchesCharacterizedFrame()
    {
        var frame = McnfNodeAdminRequests.CreateErrorStatusRequest();
        Assert.Equal(0xA4, frame.Channel);
        Assert.Equal(222, frame.Function);
        Assert.Equal(3, frame.ResponseLength);
    }

    [Fact]
    public void ReadConfigurationRequest_MatchesCharacterizedFrame()
    {
        var frame = McnfStandardDeviceRequests.CreateReadConfigurationRequest(
            deviceChannel: 0xA5,
            deviceNumber: 1,
            configurationByteSize: 4);

        Assert.Equal(
            new byte[] { 0xA5, 0x06, 0x06, 0x00, 0xC9, 0x01, 0x00, 0x00, 0x00, 0x84 },
            frame.Bytes.ToArray());
        Assert.Equal(6, frame.ResponseLength);
    }

    [Fact]
    public void ReadConfigurationRequest_EncodesDeviceNumberLowByteFirst()
    {
        var frame = McnfStandardDeviceRequests.CreateReadConfigurationRequest(
            deviceChannel: 0xA6,
            deviceNumber: 0x0201,
            configurationByteSize: 3);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(0x01, bytes[5]);
        Assert.Equal(0x02, bytes[6]);
        Assert.Equal(5, frame.ResponseLength);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(254)]
    public void ReadConfigurationRequest_RejectsInvalidConfigurationSizes(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => McnfStandardDeviceRequests.CreateReadConfigurationRequest(0xA5, 1, size));
    }
}
