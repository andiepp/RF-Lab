namespace RfLab.Core.Tests;

/// <summary>
/// Replays scripted response byte blocks in order and records every write.
/// </summary>
internal sealed class ScriptedMcnfByteStream : IMcnfByteStream
{
    private readonly Queue<byte[]> responses;
    private byte[]? currentResponse;
    private int currentOffset;

    public ScriptedMcnfByteStream(params byte[][] responses)
    {
        this.responses = new Queue<byte[]>(responses);
    }

    public List<byte[]> Writes { get; } = [];

    public bool Disposed { get; private set; }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        Writes.Add(bytes.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (currentResponse is null || currentOffset >= currentResponse.Length)
        {
            if (!responses.TryDequeue(out currentResponse))
            {
                return ValueTask.FromResult(0);
            }

            currentOffset = 0;
        }

        int count = Math.Min(buffer.Length, currentResponse.Length - currentOffset);
        currentResponse.AsSpan(currentOffset, count).CopyTo(buffer.Span);
        currentOffset += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A byte stream whose reads block until cancellation.
/// </summary>
internal sealed class PendingMcnfByteStream : IMcnfByteStream
{
    public bool Disposed { get; private set; }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
