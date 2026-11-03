namespace RfLab.Core.Tests;

public sealed class McnfRequestFrameTests
{
    [Fact]
    public void Create_BuildsCharacterizedCarrierFrame()
    {
        var frame = McnfRequestFrame.Create(
            0xA5,
            0x10,
            [0x00, 0x98, 0x96, 0x80, 0x00, 0x0A],
            responseLength: 2);

        Assert.Equal(
            new byte[] { 0xA5, 0x08, 0x02, 0x00, 0x10, 0x00, 0x98, 0x96, 0x80, 0x00, 0x0A, 0x88 },
            frame.Bytes.ToArray());
        Assert.Equal(0xA5, frame.Channel);
        Assert.Equal(0x10, frame.Function);
        Assert.Equal(2, frame.ResponseLength);
        Assert.Equal(12, frame.FrameLength);
    }

    [Fact]
    public void Create_BuildsParameterlessFrameWithMinimalBody()
    {
        var frame = McnfRequestFrame.Create(0xA4, 220, [], responseLength: 6);

        // N counts function and checksum only.
        Assert.Equal(new byte[] { 0xA4, 0x02, 0x06, 0x00, 0xDC, 0x77 }, frame.Bytes.ToArray());
    }

    [Fact]
    public void Create_AlwaysWritesZeroExecutionTime()
    {
        var frame = McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: 4);
        Assert.Equal(0, frame.Bytes.ToArray()[3]);
    }

    [Theory]
    [InlineData(0x25)]
    [InlineData(0xB5)]
    [InlineData(0x05)]
    public void Create_RejectsChannelsWithoutSyncNibble(byte channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => McnfRequestFrame.Create(channel, 0x10, [], responseLength: 2));
    }

    [Theory]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA3)]
    [InlineData(0xA9)]
    [InlineData(0xAE)]
    [InlineData(0xAF)]
    public void Create_RejectsUnframedChannels(byte channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => McnfRequestFrame.Create(channel, 0x10, [], responseLength: 2));
    }

    [Theory]
    [InlineData(0xA4)]
    [InlineData(0xA5)]
    [InlineData(0xA8)]
    [InlineData(0xAA)]
    [InlineData(0xAD)]
    public void Create_AcceptsFramedChannels(byte channel)
    {
        var frame = McnfRequestFrame.Create(channel, 0x01, [], responseLength: 2);
        Assert.Equal(channel, frame.Channel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    public void Create_RejectsResponseLengthsOutsideTheLengthField(int responseLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => McnfRequestFrame.Create(0xA5, 0x10, [], responseLength));
    }

    [Fact]
    public void Create_RejectsParametersExceedingTheBodyLengthField()
    {
        var parameters = new byte[254];
        Assert.Throws<ArgumentOutOfRangeException>(
            () => McnfRequestFrame.Create(0xA5, 0x10, parameters, responseLength: 2));
    }

    [Fact]
    public void DeviceChannel_MapsZeroBasedIndexOntoChannelBytes()
    {
        Assert.Equal(0xA5, McnfConstants.DeviceChannel(0));
        Assert.Equal(0xA8, McnfConstants.DeviceChannel(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => McnfConstants.DeviceChannel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => McnfConstants.DeviceChannel(4));
    }
}
