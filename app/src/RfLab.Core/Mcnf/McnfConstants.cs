namespace RfLab.Core.Mcnf;

/// <summary>
/// Wire-level constants of the MCNF command-response protocol, as
/// characterized from the MCNF_SystemLib reference implementation and the
/// Arduino node firmware.
/// </summary>
public static class McnfConstants
{
    /// <summary>Sync pattern carried in the high nibble of the channel byte.</summary>
    public const byte MessageSync = 0xA0;

    /// <summary>Mask extracting the sync nibble of the channel byte.</summary>
    public const byte MessageSyncMask = 0xF0;

    /// <summary>Framed-message header size: channel, N, R, T.</summary>
    public const int HeaderSize = 4;

    /// <summary>Single-byte connectivity-test message.</summary>
    public const byte ConnectivityTestChannel = 0xA1;

    /// <summary>Single-byte response of a command-response-pattern node.</summary>
    public const byte ConnectivityTestResponse = 0x21;

    /// <summary>Node-administration channel byte.</summary>
    public const byte NodeAdminChannel = 0xA4;

    /// <summary>Channel byte of the first device channel.</summary>
    public const byte DeviceChannelOffset = 0xA5;

    /// <summary>Channel byte of the last device channel.</summary>
    public const byte DeviceChannelLast = 0xA8;

    /// <summary>Channel byte of the first gateway channel.</summary>
    public const byte GatewayChannelOffset = 0xAA;

    /// <summary>Channel byte of the last gateway channel.</summary>
    public const byte GatewayChannelLast = 0xAD;

    /// <summary>Standard function: write device configuration.</summary>
    public const byte FunctionWriteConfiguration = 200;

    /// <summary>Standard function: read device configuration.</summary>
    public const byte FunctionReadConfiguration = 201;

    /// <summary>Node-administration function: read node type information.</summary>
    public const byte FunctionNodeGetTypeInfo = 220;

    /// <summary>Node-administration function: read the reported buffer size.</summary>
    public const byte FunctionNodeGetBufferSize = 221;

    /// <summary>Node-administration function: read and reset the error status.</summary>
    public const byte FunctionNodeGetErrorStatus = 222;

    /// <summary>
    /// Returns the device channel byte for a zero-based device channel index.
    /// </summary>
    public static byte DeviceChannel(int deviceChannelIndex)
    {
        if (deviceChannelIndex is < 0 or > DeviceChannelLast - DeviceChannelOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deviceChannelIndex),
                deviceChannelIndex,
                "The MCNF device channel index must be between 0 and 3.");
        }

        return (byte)(DeviceChannelOffset + deviceChannelIndex);
    }
}
