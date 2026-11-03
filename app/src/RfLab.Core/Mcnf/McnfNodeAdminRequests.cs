namespace RfLab.Core.Mcnf;

/// <summary>
/// Builds the standard node-administration requests every MCNF
/// command-response node implements.
/// </summary>
public static class McnfNodeAdminRequests
{
    /// <summary>
    /// The node-type information payload: implementor, platform,
    /// application, and communication-configuration bytes.
    /// </summary>
    public const int NodeTypeInfoPayloadLength = 4;

    public static McnfRequestFrame CreateNodeTypeInfoRequest() =>
        McnfRequestFrame.Create(
            McnfConstants.NodeAdminChannel,
            McnfConstants.FunctionNodeGetTypeInfo,
            [],
            responseLength: NodeTypeInfoPayloadLength + 2);

    public static McnfRequestFrame CreateBufferSizeRequest() =>
        McnfRequestFrame.Create(
            McnfConstants.NodeAdminChannel,
            McnfConstants.FunctionNodeGetBufferSize,
            [],
            responseLength: 3);

    public static McnfRequestFrame CreateErrorStatusRequest() =>
        McnfRequestFrame.Create(
            McnfConstants.NodeAdminChannel,
            McnfConstants.FunctionNodeGetErrorStatus,
            [],
            responseLength: 3);
}
