namespace RfLab.Core.Tests;

public sealed class RfLabIdentityTests
{
    [Fact]
    public void ParseNodeTypeInfo_AcceptsTheCharacterizedIdentityBytes()
    {
        RfLabIdentity identity =
            RfLabIdentity.ParseNodeTypeInfo([0xAE, 0x70, 0x10, 0x80]);

        Assert.Equal("AE.70.10.80", identity.NodeType);
    }

    [Theory]
    [InlineData(new byte[] { 0xAE, 0x70, 0x10 })]
    [InlineData(new byte[] { 0xAE, 0x70, 0x10, 0x80, 0x00 })]
    [InlineData(new byte[] { 0xAF, 0x70, 0x10, 0x80 })]
    [InlineData(new byte[] { 0xAE, 0x63, 0x10, 0x80 })]
    [InlineData(new byte[] { 0xAE, 0x70, 0x11, 0x80 })]
    [InlineData(new byte[] { 0xAE, 0x70, 0x10, 0x81 })]
    public void ParseNodeTypeInfo_RejectsForeignNodeTypes(byte[] payload)
    {
        Assert.Throws<InvalidDataException>(
            () => RfLabIdentity.ParseNodeTypeInfo(payload));
    }

    [Fact]
    public void ProductIdentity_IsTheRfLabName()
    {
        Assert.Equal("RF-Lab", RfLabIdentity.ProductIdentity);
    }
}
