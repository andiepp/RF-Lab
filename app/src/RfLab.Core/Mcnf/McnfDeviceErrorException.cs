namespace RfLab.Core.Mcnf;

/// <summary>
/// Reports that an MCNF node completed an exchange and rejected the request
/// with an application error byte. The communication session itself remains
/// healthy.
/// </summary>
public sealed class McnfDeviceErrorException : InvalidOperationException
{
    public McnfDeviceErrorException(byte errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the application error byte reported by the node.</summary>
    public byte ErrorCode { get; }
}
