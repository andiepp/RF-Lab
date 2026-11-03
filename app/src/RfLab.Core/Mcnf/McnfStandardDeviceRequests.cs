namespace RfLab.Core.Mcnf;

/// <summary>
/// Builds the standard device-channel requests shared by MCNF applications.
/// </summary>
public static class McnfStandardDeviceRequests
{
    /// <summary>
    /// Builds the read-configuration handshake request. The response payload
    /// carries the standard three bytes — variable-set count, active
    /// variable set, capabilities — followed by application-specific
    /// configuration bytes.
    /// </summary>
    public static McnfRequestFrame CreateReadConfigurationRequest(
        byte deviceChannel,
        ushort deviceNumber,
        int configurationByteSize)
    {
        if (configurationByteSize is < 3 or > byte.MaxValue - 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configurationByteSize),
                configurationByteSize,
                "The MCNF configuration must carry at least the standard three bytes.");
        }

        ReadOnlySpan<byte> parameters =
        [
            (byte)(deviceNumber % 256),
            (byte)(deviceNumber / 256),
            0,
            0
        ];

        return McnfRequestFrame.Create(
            deviceChannel,
            McnfConstants.FunctionReadConfiguration,
            parameters,
            responseLength: configurationByteSize + 2);
    }
}
