namespace RfLab.Core.Mcnf;

/// <summary>
/// Session-level framing limits and timing of one MCNF node connection.
/// </summary>
public sealed class McnfFramingOptions
{
    public McnfFramingOptions(
        TimeSpan totalExchangeTimeout,
        int nodeBufferSize)
    {
        if (totalExchangeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalExchangeTimeout));
        }

        if (nodeBufferSize < McnfConstants.HeaderSize + 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeBufferSize),
                nodeBufferSize,
                "The MCNF node buffer must hold at least a minimal framed request.");
        }

        TotalExchangeTimeout = totalExchangeTimeout;
        NodeBufferSize = nodeBufferSize;
    }

    /// <summary>Gets the total timeout of one complete exchange.</summary>
    public TimeSpan TotalExchangeTimeout { get; }

    /// <summary>
    /// Gets the node's message buffer size in bytes. Request frames and
    /// expected responses must both fit into this buffer.
    /// </summary>
    public int NodeBufferSize { get; }
}
