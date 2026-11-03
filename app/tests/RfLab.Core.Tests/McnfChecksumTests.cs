namespace RfLab.Core.Tests;

public sealed class McnfChecksumTests
{
    [Fact]
    public void Compute_IsComplementOfByteSum()
    {
        Assert.Equal(0xFF, McnfChecksum.Compute([]));
        Assert.Equal(0xFE, McnfChecksum.Compute([0x01]));
        Assert.Equal(0x00, McnfChecksum.Compute([0xFF]));
        Assert.Equal(0xFE, McnfChecksum.Compute([0xFF, 0x02, 0x00]));
    }

    [Fact]
    public void Compute_MatchesCharacterizedCarrierFrame()
    {
        // Frame captured from the characterized reference implementation:
        // channel A5, N=08, R=02, T=00, function 10, f=10 MHz, a=10.
        ReadOnlySpan<byte> frameWithoutChecksum =
            [0xA5, 0x08, 0x02, 0x00, 0x10, 0x00, 0x98, 0x96, 0x80, 0x00, 0x0A];
        Assert.Equal(0x88, McnfChecksum.Compute(frameWithoutChecksum));
    }

    [Fact]
    public void IsValid_AcceptsMatchingTrailingChecksum()
    {
        Assert.True(McnfChecksum.IsValid([0x01, 0x02, 0xFC]));
    }

    [Fact]
    public void IsValid_RejectsMismatchedChecksum()
    {
        Assert.False(McnfChecksum.IsValid([0x01, 0x02, 0xFB]));
    }

    [Fact]
    public void IsValid_RejectsFramesShorterThanTwoBytes()
    {
        Assert.False(McnfChecksum.IsValid([]));
        Assert.False(McnfChecksum.IsValid([0xFF]));
    }
}
