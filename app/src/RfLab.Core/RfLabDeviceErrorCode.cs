using RfLab.Core.Mcnf;

namespace RfLab.Core;

/// <summary>
/// RF-Lab application error codes extending the standard MCNF node codes.
/// </summary>
public static class RfLabDeviceErrorCode
{
    public const byte Si5351Disconnected = 0x10;
    public const byte Si5351Channel = 0x11;

    /// <summary>
    /// Returns a sanitized name for an RF-Lab response error byte.
    /// </summary>
    public static string Describe(byte errorCode) =>
        errorCode switch
        {
            Si5351Disconnected => "Si5351Disconnected",
            Si5351Channel => "Si5351Channel",
            <= 14 => ((McnfNodeErrorCode)errorCode).ToString(),
            _ => $"Unknown(0x{errorCode:X2})"
        };
}
