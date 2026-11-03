namespace RfLab.Core.Mcnf;

/// <summary>
/// Computes and verifies the MCNF frame checksum: the bitwise complement of
/// the modulo-256 sum of all preceding frame bytes.
/// </summary>
public static class McnfChecksum
{
    public static byte Compute(ReadOnlySpan<byte> bytes)
    {
        byte sum = 0;
        foreach (byte value in bytes)
        {
            sum = (byte)(sum + value);
        }

        return (byte)(0xFF - sum);
    }

    /// <summary>
    /// Verifies that the last byte of the frame is the checksum of all
    /// preceding bytes.
    /// </summary>
    public static bool IsValid(ReadOnlySpan<byte> frame) =>
        frame.Length >= 2
        && frame[^1] == Compute(frame[..^1]);
}
