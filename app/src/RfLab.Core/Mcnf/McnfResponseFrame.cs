namespace RfLab.Core.Mcnf;

/// <summary>
/// One parsed framed MCNF response: the leading error byte, the payload,
/// and the trailing checksum byte.
/// </summary>
/// <remarks>
/// The characterized node firmware computes the response checksum only for
/// successful responses; an error response carries an unspecified byte in
/// the checksum position. The checksum is therefore verified only when the
/// error byte reports success.
/// </remarks>
public sealed class McnfResponseFrame
{
    private readonly byte[] payload;

    private McnfResponseFrame(byte errorCode, byte[] payload)
    {
        ErrorCode = errorCode;
        this.payload = payload;
    }

    /// <summary>Gets the application error byte; zero reports success.</summary>
    public byte ErrorCode { get; }

    /// <summary>Gets whether the response reports success.</summary>
    public bool IsSuccess => ErrorCode == 0;

    /// <summary>Gets the payload between the error byte and the checksum.</summary>
    public ReadOnlyMemory<byte> Payload => payload;

    public static McnfResponseFrame Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2)
        {
            throw new InvalidDataException(
                "The MCNF response frame is shorter than the error byte and checksum.");
        }

        byte errorCode = frame[0];
        if (errorCode == 0 && !McnfChecksum.IsValid(frame))
        {
            throw new InvalidDataException(
                "The MCNF response frame failed checksum verification.");
        }

        return new McnfResponseFrame(errorCode, frame[1..^1].ToArray());
    }
}
