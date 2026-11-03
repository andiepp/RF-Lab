namespace RfLab.Core.Mcnf;

/// <summary>
/// Reports a failed MCNF exchange. When the request bytes may have reached
/// the node before the failure, the node may have executed the function even
/// though no response was established.
/// </summary>
public sealed class McnfExchangeException : IOException
{
    public McnfExchangeException(
        string message,
        bool executionMayHaveOccurred,
        Exception innerException)
        : base(message, innerException)
    {
        ExecutionMayHaveOccurred = executionMayHaveOccurred;
    }

    public bool ExecutionMayHaveOccurred { get; }
}
