namespace RfLab.Core.Tests;

public sealed class McnfSessionTests
{
    private static readonly McnfFramingOptions Options = new(
        TimeSpan.FromSeconds(5),
        nodeBufferSize: 128);

    private static McnfRequestFrame SensorRequest() =>
        McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: 4);

    [Fact]
    public async Task ExchangeAsync_TransmitsFrameAndParsesSuccessResponse()
    {
        var stream = new ScriptedMcnfByteStream(
            McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A));
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame response = await session.ExchangeAsync(SensorRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(new byte[] { 0x02, 0x9A }, response.Payload.ToArray());
        Assert.Equal(SensorRequest().Bytes.ToArray(), Assert.Single(stream.Writes));
        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_AssemblesResponseFromFragmentedReads()
    {
        byte[] response = McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A);
        var stream = new ScriptedMcnfByteStream(
            response[..1],
            response[1..3],
            response[3..]);
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame parsed = await session.ExchangeAsync(SensorRequest());

        Assert.Equal(new byte[] { 0x02, 0x9A }, parsed.Payload.ToArray());
    }

    [Fact]
    public async Task ExchangeAsync_ReturnsDeviceErrorWithoutFaultingTheSession()
    {
        var stream = new ScriptedMcnfByteStream(
            new byte[] { 0x10, 0x00, 0x00, 0x00 },
            McnfResponseFrameTests.BuildSuccessResponse(0x00, 0x10));
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame response = await session.ExchangeAsync(SensorRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(0x10, response.ErrorCode);
        Assert.Equal(McnfSessionState.Open, session.State);

        McnfResponseFrame next = await session.ExchangeAsync(SensorRequest());
        Assert.True(next.IsSuccess);
    }

    [Fact]
    public async Task ExchangeAsync_FaultsOnCorruptSuccessResponse()
    {
        byte[] corrupt = McnfResponseFrameTests.BuildSuccessResponse(0x02, 0x9A);
        corrupt[^1] ^= 0xFF;
        var stream = new ScriptedMcnfByteStream(corrupt);
        await using var session = new McnfSession(stream, Options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.Equal(McnfSessionState.Faulted, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.ExchangeAsync(SensorRequest()));
    }

    [Fact]
    public async Task ExchangeAsync_FaultsUncertainOnEndOfStream()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x00, 0x02 });
        await using var session = new McnfSession(stream, Options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.IsType<EndOfStreamException>(failure.InnerException);
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_FaultsUncertainOnTimeout()
    {
        var options = new McnfFramingOptions(
            TimeSpan.FromMilliseconds(50),
            nodeBufferSize: 128);
        await using var session = new McnfSession(new PendingMcnfByteStream(), options);

        var failure = await Assert.ThrowsAsync<McnfExchangeException>(
            () => session.ExchangeAsync(SensorRequest()));

        Assert.True(failure.ExecutionMayHaveOccurred);
        Assert.IsType<TimeoutException>(failure.InnerException);
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task ExchangeAsync_RejectsRequestsExceedingTheNodeBuffer()
    {
        var options = new McnfFramingOptions(TimeSpan.FromSeconds(1), nodeBufferSize: 8);
        await using var session = new McnfSession(new ScriptedMcnfByteStream(), options);

        var request = McnfRequestFrame.Create(0xA5, 0x11, new byte[14], responseLength: 2);
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExchangeAsync(request));
    }

    [Fact]
    public async Task ExchangeAsync_RejectsExpectedResponsesExceedingTheNodeBuffer()
    {
        var options = new McnfFramingOptions(TimeSpan.FromSeconds(1), nodeBufferSize: 8);
        await using var session = new McnfSession(new ScriptedMcnfByteStream(), options);

        var request = McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: 9);
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.ExchangeAsync(request));
    }

    [Fact]
    public async Task ConnectivityTest_SendsSingleByteAndAcceptsFixedResponse()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x21 });
        await using var session = new McnfSession(stream, Options);

        await session.ConnectivityTestAsync();

        Assert.Equal(new byte[] { 0xA1 }, Assert.Single(stream.Writes));
        Assert.Equal(McnfSessionState.Open, session.State);
    }

    [Fact]
    public async Task ConnectivityTest_FaultsOnUnexpectedResponseByte()
    {
        var stream = new ScriptedMcnfByteStream(new byte[] { 0x20 });
        await using var session = new McnfSession(stream, Options);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ConnectivityTestAsync());
        Assert.Equal(McnfSessionState.Faulted, session.State);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheStreamAndRejectsFurtherExchanges()
    {
        var stream = new ScriptedMcnfByteStream();
        var session = new McnfSession(stream, Options);

        await session.DisposeAsync();

        Assert.True(stream.Disposed);
        Assert.Equal(McnfSessionState.Disposed, session.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => session.ExchangeAsync(SensorRequest()));
    }
    [Fact]
    public async Task SkipsLeadingText_TakesTheFirstUnprintableByteAsTheResponse()
    {
        byte[] response = McnfResponseFrameTests.BuildSuccessResponse(0x41, 0x0A);
        var stream = new ScriptedMcnfByteStream([.. "debug: x=1\r\n"u8.ToArray(), .. response]);
        await using var session = new McnfSession(stream, Options) { SkipsLeadingText = true };

        McnfResponseFrame frame = await session.ExchangeAsync(
            McnfRequestFrame.Create(0xA5, 0x20, [], responseLength: response.Length));

        // printable bytes inside the response are its payload, not text
        Assert.Equal(new byte[] { 0x41, 0x0A }, frame.Payload.ToArray());
    }

    [Fact]
    public async Task WithoutSkipping_LeadingTextIsTakenForTheResponse()
    {
        byte[] response = McnfResponseFrameTests.BuildSuccessResponse(0x01);
        var stream = new ScriptedMcnfByteStream([.. "hi"u8.ToArray(), .. response]);
        await using var session = new McnfSession(stream, Options);

        McnfResponseFrame frame = await session.ExchangeAsync(
            McnfRequestFrame.Create(0xA5, 0x03, [], responseLength: 3));

        Assert.Equal((byte)'h', frame.ErrorCode);
    }

    [Fact]
    public async Task Trace_ReportsTransmittedAndReceivedBytes()
    {
        var stream = new ScriptedMcnfByteStream(
            McnfResponseFrameTests.BuildSuccessResponse(0x01));
        await using var session = new McnfSession(stream, Options);
        var trace = new List<(McnfTraceDirection Direction, byte[] Bytes)>();
        session.Trace = (direction, bytes) => trace.Add((direction, bytes.ToArray()));

        McnfRequestFrame request = McnfRequestFrame.Create(0xA5, 0x03, [], responseLength: 3);
        await session.ExchangeAsync(request);

        Assert.Equal(McnfTraceDirection.Transmit, trace[0].Direction);
        Assert.Equal(request.Bytes.ToArray(), trace[0].Bytes);
        Assert.Equal(McnfResponseFrameTests.BuildSuccessResponse(0x01), trace.Skip(1).SelectMany(entry => entry.Bytes).ToArray());
        Assert.All(trace.Skip(1), entry => Assert.Equal(McnfTraceDirection.Receive, entry.Direction));
    }
}
