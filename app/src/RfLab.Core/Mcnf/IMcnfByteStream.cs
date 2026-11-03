namespace RfLab.Core.Mcnf;

/// <summary>
/// Transport-neutral byte-stream contract used by the MCNF session.
/// </summary>
public interface IMcnfByteStream : IAsyncDisposable
{
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
