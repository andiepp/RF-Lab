namespace RfLab.Core.Mcnf;

/// <summary>
/// Standard MCNF node error codes returned in the first byte of a framed
/// response. Applications may define additional codes above this range.
/// </summary>
public enum McnfNodeErrorCode : byte
{
    None = 0,
    Checksum = 1,
    UnknownId = 2,
    UnknownFunction = 3,
    VariableIndex = 4,
    VariableData = 5,
    MessageType = 6,
    BufferOverflow = 7,
    SyncMissing = 8,
    WriteToPort = 9,
    ConnectionLost = 10,
    ReceiveTimeout = 11,
    PermanentStorageMissing = 12,
    GatewayTimeout = 13,
    GatewayReceive = 14
}
