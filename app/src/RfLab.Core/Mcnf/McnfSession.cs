namespace RfLab.Core.Mcnf;

/// <summary>
/// The direction of bytes reported to an MCNF trace.
/// </summary>
public enum McnfTraceDirection
{
    Transmit = 0,
    Receive = 1,

    /// <summary>Text the node printed before its response (see <see cref="McnfSession.SkipsLeadingText"/>).</summary>
    Text = 2
}

/// <summary>
/// One serialized command-response session with an MCNF node. Every exchange
/// transmits one framed request and reads the fixed-length response through
/// one gate; nothing overlaps on the wire. Any failure after transmission
/// began faults the session, because the node may have executed the function
/// and the wire may be out of step.
/// </summary>
public sealed class McnfSession : IAsyncDisposable
{
    private readonly IMcnfByteStream stream;
    private readonly McnfFramingOptions options;
    private readonly SemaphoreSlim exchangeGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object disposalLock = new();
    private int state = (int)McnfSessionState.Open;
    private Task? disposalTask;

    public McnfSession(IMcnfByteStream stream, McnfFramingOptions options)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Receives each request and each response (a partial one when the
    /// exchange failed), for a trace view. It is called on the exchanging
    /// thread and must not block.
    /// </summary>
    public Action<McnfTraceDirection, ReadOnlyMemory<byte>>? Trace { get; set; }

    public McnfSessionState State => (McnfSessionState)Volatile.Read(ref state);

    /// <summary>
    /// Whether printable text (ASCII and line ends) arriving before a framed
    /// response is skipped rather than taken for the response.
    /// </summary>
    /// <remarks>
    /// Some node firmware prints debug text on the host port. The first byte
    /// of a framed response is the error byte (0x00 for success, a small
    /// code otherwise), which is never printable, so text before it can be
    /// told apart; the codes 0x0A and 0x0D (line ends) are the exception and
    /// would end in a timeout instead.
    /// </remarks>
    public bool SkipsLeadingText { get; set; }

    /// <summary>
    /// Transmits one framed request and reads its complete response. A
    /// response reporting an application error byte is returned, not thrown;
    /// transport failures after transmission began fault the session and
    /// surface as <see cref="McnfExchangeException"/>.
    /// </summary>
    public async Task<McnfResponseFrame> ExchangeAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FrameLength > options.NodeBufferSize
            || request.ResponseLength > options.NodeBufferSize)
        {
            throw new ArgumentException(
                "The MCNF request or its response exceeds the node buffer size.",
                nameof(request));
        }

        var responseBuffer = new byte[request.ResponseLength];
        await RunExchangeAsync(request.Bytes, responseBuffer, SkipsLeadingText, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return McnfResponseFrame.Parse(responseBuffer);
        }
        catch (InvalidDataException exception)
        {
            TransitionToFaulted();
            throw new McnfExchangeException(
                "The response failed verification; the outcome is uncertain.",
                executionMayHaveOccurred: true,
                exception);
        }
    }

    /// <summary>
    /// Performs the single-byte MCNF connectivity test and verifies the node's
    /// fixed answer.
    /// </summary>
    public async Task ConnectivityTestAsync(CancellationToken cancellationToken = default)
    {
        var responseBuffer = new byte[1];
        await RunExchangeAsync(
            new[] { McnfConstants.ConnectivityTestChannel },
            responseBuffer,
            skipsText: false,
            cancellationToken).ConfigureAwait(false);

        if (responseBuffer[0] != McnfConstants.ConnectivityTestResponse)
        {
            TransitionToFaulted();
            throw new InvalidDataException(
                $"The connectivity test was answered with 0x{responseBuffer[0]:X2} instead of 0x21.");
        }
    }

    private async Task RunExchangeAsync(
        ReadOnlyMemory<byte> requestBytes,
        byte[] responseBuffer,
        bool skipsText,
        CancellationToken cancellationToken)
    {
        EnsureOpen();
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCancellation = new CancellationTokenSource();
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeCancellation.Token,
            timeoutCancellation.Token);

        var gateEntered = false;
        var writeStarted = false;
        var received = 0;
        List<byte>? text = skipsText ? [] : null;
        try
        {
            await exchangeGate.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            gateEntered = true;
            EnsureOpen();
            timeoutCancellation.CancelAfter(options.TotalExchangeTimeout);

            writeStarted = true;
            Trace?.Invoke(McnfTraceDirection.Transmit, requestBytes);
            await stream
                .WriteAsync(requestBytes, operationCancellation.Token)
                .AsTask()
                .WaitAsync(operationCancellation.Token)
                .ConfigureAwait(false);

            // never read past the response: at most what it still lacks
            var chunk = new byte[responseBuffer.Length];
            while (received < responseBuffer.Length)
            {
                int wanted = responseBuffer.Length - received;
                var count = await stream
                    .ReadAsync(chunk.AsMemory(0, wanted), operationCancellation.Token)
                    .AsTask()
                    .WaitAsync(operationCancellation.Token)
                    .ConfigureAwait(false);

                if (count <= 0 || count > wanted)
                {
                    throw new EndOfStreamException(
                        "The byte stream ended before the complete response was received.");
                }

                for (var index = 0; index < count; index++)
                {
                    byte value = chunk[index];
                    if (text is not null && received == 0 && IsText(value))
                    {
                        text.Add(value);
                    }
                    else
                    {
                        responseBuffer[received++] = value;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            if (writeStarted)
            {
                throw Uncertain(
                    "The session was closed during the exchange.",
                    new ObjectDisposedException(nameof(McnfSession)));
            }

            throw new ObjectDisposedException(nameof(McnfSession));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (writeStarted)
            {
                throw Uncertain("The exchange was cancelled after transmission began.", exception);
            }

            throw;
        }
        catch (OperationCanceledException exception) when (timeoutCancellation.IsCancellationRequested)
        {
            var timeout = new TimeoutException("The node did not answer in time.", exception);
            if (writeStarted)
            {
                throw Uncertain("The node did not answer in time.", timeout);
            }

            TransitionToFaulted();
            throw new McnfExchangeException(
                "The request could not be sent in time.",
                executionMayHaveOccurred: false,
                timeout);
        }
        catch (Exception exception) when (writeStarted && exception is not McnfExchangeException)
        {
            throw Uncertain("The exchange failed after transmission began.", exception);
        }
        finally
        {
            if (text is { Count: > 0 })
            {
                Trace?.Invoke(McnfTraceDirection.Text, text.ToArray());
            }

            // the response as one block; a partial one when the exchange failed
            if (received > 0)
            {
                Trace?.Invoke(McnfTraceDirection.Receive, responseBuffer.AsMemory(0, received));
            }

            if (gateEntered)
            {
                exchangeGate.Release();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
        {
            disposalTask ??= DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref state, (int)McnfSessionState.Disposed);
        await lifetimeCancellation.CancelAsync().ConfigureAwait(false);

        await exchangeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            exchangeGate.Release();
            lifetimeCancellation.Dispose();
        }
    }

    private static bool IsText(byte value) =>
        value is (>= 0x20 and < 0x7F) or 0x0D or 0x0A;

    private McnfExchangeException Uncertain(string message, Exception innerException)
    {
        TransitionToFaulted();
        return new McnfExchangeException(message, executionMayHaveOccurred: true, innerException);
    }

    private void EnsureOpen()
    {
        switch (State)
        {
            case McnfSessionState.Open:
                return;
            case McnfSessionState.Faulted:
                throw new InvalidOperationException(
                    "The connection to the board was lost; connect again.");
            default:
                throw new ObjectDisposedException(nameof(McnfSession));
        }
    }

    private void TransitionToFaulted() =>
        Interlocked.CompareExchange(
            ref state,
            (int)McnfSessionState.Faulted,
            (int)McnfSessionState.Open);
}
