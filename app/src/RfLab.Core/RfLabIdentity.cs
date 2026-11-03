using RfLab.Core.Mcnf;

namespace RfLab.Core;

/// <summary>
/// The authoritative RF-Lab node identity read through the MCNF
/// node-administration type-information function.
/// </summary>
public sealed record RfLabIdentity
{
    public const byte ExpectedImplementor = 0xAE;
    public const byte ExpectedPlatform = 0x70;
    public const byte ExpectedApplicationId = 0x10;
    public const byte ExpectedConfiguration = 0x80;

    public const string ProductIdentity = "RF-Lab";

    private RfLabIdentity(string nodeType)
    {
        NodeType = nodeType;
    }

    /// <summary>
    /// Gets the formatted node-type bytes: implementor, platform,
    /// application, and communication configuration.
    /// </summary>
    public string NodeType { get; }

    /// <summary>
    /// Parses and verifies the node-type information payload against the
    /// characterized RF-Lab identity bytes.
    /// </summary>
    public static RfLabIdentity ParseNodeTypeInfo(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != McnfNodeAdminRequests.NodeTypeInfoPayloadLength
            || payload[0] != ExpectedImplementor
            || payload[1] != ExpectedPlatform
            || payload[2] != ExpectedApplicationId
            || payload[3] != ExpectedConfiguration)
        {
            throw new InvalidDataException(
                "The node-type information does not match the supported RF-Lab identity.");
        }

        return new RfLabIdentity(
            $"{payload[0]:X2}.{payload[1]:X2}.{payload[2]:X2}.{payload[3]:X2}");
    }
}
