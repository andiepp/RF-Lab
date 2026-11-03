namespace RfLab.Core.Mcnf;

/// <summary>
/// One immutable framed MCNF request:
/// channel, body length N, response length R, execution-time byte T,
/// function, parameters, and the trailing checksum.
/// </summary>
public sealed class McnfRequestFrame
{
    private readonly byte[] frame;

    private McnfRequestFrame(byte[] frame, int responseLength)
    {
        this.frame = frame;
        ResponseLength = responseLength;
    }

    /// <summary>Gets the channel byte, including the sync nibble.</summary>
    public byte Channel => frame[0];

    /// <summary>Gets the function byte.</summary>
    public byte Function => frame[McnfConstants.HeaderSize];

    /// <summary>
    /// Gets the complete expected response length in bytes, including the
    /// leading error byte and the trailing checksum byte.
    /// </summary>
    public int ResponseLength { get; }

    /// <summary>Gets the complete frame length in bytes.</summary>
    public int FrameLength => frame.Length;

    /// <summary>Gets the bytes transmitted on the wire.</summary>
    public ReadOnlyMemory<byte> Bytes => frame;

    /// <summary>
    /// Creates a framed request for a node-administration, device, or
    /// gateway channel. The execution-time byte T is always zero; the
    /// characterized reference node ignores it.
    /// </summary>
    public static McnfRequestFrame Create(
        byte channel,
        byte function,
        ReadOnlySpan<byte> parameters,
        int responseLength)
    {
        if ((channel & McnfConstants.MessageSyncMask) != McnfConstants.MessageSync)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                "The MCNF channel byte must carry the sync nibble.");
        }

        bool isFramedChannel =
            channel == McnfConstants.NodeAdminChannel
            || (channel >= McnfConstants.DeviceChannelOffset
                && channel <= McnfConstants.DeviceChannelLast)
            || (channel >= McnfConstants.GatewayChannelOffset
                && channel <= McnfConstants.GatewayChannelLast);
        if (!isFramedChannel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                "The MCNF channel byte is not a framed node, device, or gateway channel.");
        }

        // N counts function, parameters, and the trailing checksum.
        int bodyLength = 1 + parameters.Length + 1;
        if (bodyLength > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                parameters.Length,
                "The MCNF request body exceeds the one-byte length field.");
        }

        if (responseLength is < 2 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseLength),
                responseLength,
                "The MCNF response length must be between 2 and 255 bytes.");
        }

        var frame = new byte[McnfConstants.HeaderSize + bodyLength];
        frame[0] = channel;
        frame[1] = (byte)bodyLength;
        frame[2] = (byte)responseLength;
        frame[3] = 0;
        frame[4] = function;
        parameters.CopyTo(frame.AsSpan(McnfConstants.HeaderSize + 1));
        frame[^1] = McnfChecksum.Compute(frame.AsSpan(0, frame.Length - 1));

        return new McnfRequestFrame(frame, responseLength);
    }
}
