namespace RfLab.Core.Tests;

public sealed class McnfResponseFrameTests
{
    internal static byte[] BuildSuccessResponse(params byte[] payload)
    {
        var frame = new byte[payload.Length + 2];
        frame[0] = 0;
        payload.CopyTo(frame, 1);
        frame[^1] = McnfChecksum.Compute(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    [Fact]
    public void Parse_AcceptsSuccessResponseWithValidChecksum()
    {
        var response = McnfResponseFrame.Parse(BuildSuccessResponse(0x02, 0x9A));

        Assert.True(response.IsSuccess);
        Assert.Equal(0, response.ErrorCode);
        Assert.Equal(new byte[] { 0x02, 0x9A }, response.Payload.ToArray());
    }

    [Fact]
    public void Parse_RejectsSuccessResponseWithInvalidChecksum()
    {
        byte[] frame = BuildSuccessResponse(0x02, 0x9A);
        frame[^1] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => McnfResponseFrame.Parse(frame));
    }

    [Fact]
    public void Parse_AcceptsErrorResponseWithoutChecksumVerification()
    {
        // The characterized node firmware leaves the checksum position of an
        // error response unspecified.
        var response = McnfResponseFrame.Parse([0x03, 0x00, 0x00, 0x5A]);

        Assert.False(response.IsSuccess);
        Assert.Equal(3, response.ErrorCode);
        Assert.Equal(new byte[] { 0x00, 0x00 }, response.Payload.ToArray());
    }

    [Fact]
    public void Parse_RejectsFramesShorterThanErrorByteAndChecksum()
    {
        Assert.Throws<InvalidDataException>(() => McnfResponseFrame.Parse([]));
        Assert.Throws<InvalidDataException>(() => McnfResponseFrame.Parse([0x00]));
    }

    [Fact]
    public void Parse_AcceptsMinimalSuccessResponseWithoutPayload()
    {
        var response = McnfResponseFrame.Parse([0x00, 0xFF]);

        Assert.True(response.IsSuccess);
        Assert.Empty(response.Payload.ToArray());
    }
}
