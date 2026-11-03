using RfLab.Core.Mcnf;

namespace RfLab.Core;

/// <summary>One reading of the RF-Lab detector input.</summary>
/// <param name="AdcValue">The raw 10-bit converter value.</param>
/// <param name="Millivolts">The detector voltage against the 2.56 V reference.</param>
/// <param name="Level">The AD8307 level in dB, -70 at the detector floor.</param>
public sealed record RfLabSensorReading(int AdcValue, double Millivolts, double Level);

/// <summary>
/// A connection to one RF-Lab board over its MCNF serial protocol: the
/// identity check at connect, and one method per board function.
/// </summary>
/// <remarks>
/// The board keeps no readable signal state: it reports its identity, its
/// configuration (LED, Si5351 fitted), the LED and the detector, and accepts
/// a complete signal setting per request. The application therefore holds the
/// settings and sends the whole setting whenever one value changes.
/// </remarks>
public sealed class RfLabClient : IAsyncDisposable
{
    /// <summary>
    /// The time the board needs after the port opens. Opening the port resets
    /// the Arduino Mega; 1.5 s was measured too short, 3 s works.
    /// </summary>
    public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromSeconds(3);

    private static readonly McnfFramingOptions Framing =
        new(TimeSpan.FromSeconds(5), RfLabProtocol.NodeBufferSize);

    private readonly McnfSession session;

    private RfLabClient(
        McnfSession session,
        string portName,
        RfLabIdentity identity,
        int reportedBufferSize,
        RfLabConfiguration configuration)
    {
        this.session = session;
        PortName = portName;
        Identity = identity;
        ReportedBufferSize = reportedBufferSize;
        Configuration = configuration;
    }

    public string PortName { get; }

    public RfLabIdentity Identity { get; }

    /// <summary>The message buffer the board reports (64 on the RF-Lab firmware).</summary>
    public int ReportedBufferSize { get; }

    public RfLabConfiguration Configuration { get; }

    /// <summary>Whether the connection failed and must be opened again.</summary>
    public bool IsFaulted => session.State != McnfSessionState.Open;

    /// <summary>Receives every byte block sent and received.</summary>
    public Action<McnfTraceDirection, ReadOnlyMemory<byte>>? Trace
    {
        get => session.Trace;
        set => session.Trace = value;
    }

    /// <summary>
    /// Opens the serial port, waits for the board to boot and checks that an
    /// RF-Lab board answers.
    /// </summary>
    public static async Task<RfLabClient> ConnectAsync(
        string portName,
        CancellationToken cancellationToken = default)
    {
        SerialPortByteStream stream = SerialPortByteStream.Open(portName, RfLabProtocol.BaudRate);
        try
        {
            await Task.Delay(DefaultSettleDelay, cancellationToken).ConfigureAwait(false);
            stream.DiscardInput();
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return await ConnectAsync(stream, portName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks the board on an opened, settled byte stream; takes ownership of it.</summary>
    public static async Task<RfLabClient> ConnectAsync(
        IMcnfByteStream stream,
        string portName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // The AD9910 code in the RF-Lab firmware prints debug text on the host
        // port while it prepares AM and FM (about 100 bytes before the answer).
        var session = new McnfSession(stream, Framing) { SkipsLeadingText = true };
        try
        {
            await session.ConnectivityTestAsync(cancellationToken).ConfigureAwait(false);

            McnfResponseFrame typeInfo = await ExchangeSuccessfullyAsync(
                session,
                McnfNodeAdminRequests.CreateNodeTypeInfoRequest(),
                cancellationToken).ConfigureAwait(false);
            RfLabIdentity identity = RfLabIdentity.ParseNodeTypeInfo(typeInfo.Payload.Span);

            McnfResponseFrame bufferSize = await ExchangeSuccessfullyAsync(
                session,
                McnfNodeAdminRequests.CreateBufferSizeRequest(),
                cancellationToken).ConfigureAwait(false);

            McnfResponseFrame configuration = await ExchangeSuccessfullyAsync(
                session,
                RfLabProtocol.CreateReadConfigurationRequest(),
                cancellationToken).ConfigureAwait(false);

            return new RfLabClient(
                session,
                portName,
                identity,
                bufferSize.Payload.Length == 1 ? bufferSize.Payload.Span[0] : 0,
                RfLabProtocol.ParseConfigurationPayload(configuration.Payload.Span));
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> ReadIndicatorAsync(CancellationToken cancellationToken = default)
    {
        McnfResponseFrame response = await ExchangeAsync(
            RfLabProtocol.CreateIndicatorStateRequest(),
            cancellationToken).ConfigureAwait(false);
        return RfLabProtocol.ParseIndicatorPayload(response.Payload.Span);
    }

    /// <summary>Switches the board's LED and returns the state the board reports back.</summary>
    public async Task<bool> SetIndicatorAsync(bool on, CancellationToken cancellationToken = default)
    {
        McnfResponseFrame response = await ExchangeAsync(
            RfLabProtocol.CreateIndicatorRequest(on),
            cancellationToken).ConfigureAwait(false);
        return RfLabProtocol.ParseIndicatorPayload(response.Payload.Span);
    }

    /// <summary>An unmodulated carrier; ends any modulation or sweep.</summary>
    public Task ApplyCarrierAsync(
        uint frequencyHertz,
        int attenuationDecibel,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(
            RfLabProtocol.CreateCarrierRequest(frequencyHertz, attenuationDecibel),
            cancellationToken);

    public Task ApplyAmplitudeModulationAsync(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        int depthPercent,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(
            RfLabProtocol.CreateAmplitudeModulationRequest(
                carrierFrequencyHertz,
                attenuationDecibel,
                modulationFrequencyHertz,
                depthPercent),
            cancellationToken);

    public Task ApplyFrequencyModulationAsync(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        uint deviationHertz,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(
            RfLabProtocol.CreateFrequencyModulationRequest(
                carrierFrequencyHertz,
                attenuationDecibel,
                modulationFrequencyHertz,
                deviationHertz),
            cancellationToken);

    /// <summary>Starts the AD9910's own frequency sweep, which runs on the board.</summary>
    public Task StartSweepAsync(
        uint startFrequencyHertz,
        uint stopFrequencyHertz,
        int sweepTimeMilliseconds,
        int attenuationDecibel,
        RfLabSweepMode sweepMode,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(
            RfLabProtocol.CreateSweepRequest(
                startFrequencyHertz,
                stopFrequencyHertz,
                sweepTimeMilliseconds,
                attenuationDecibel,
                sweepMode),
            cancellationToken);

    /// <summary>Sets one Si5351 clock output (0 to 2).</summary>
    public Task ApplyClockAsync(
        int clockChannel,
        uint frequencyHertz,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(
            RfLabProtocol.CreateClockRequest(clockChannel, frequencyHertz),
            cancellationToken);

    public async Task<RfLabSensorReading> ReadSensorAsync(CancellationToken cancellationToken = default)
    {
        McnfResponseFrame response = await ExchangeAsync(
            RfLabProtocol.CreateReadSensorRequest(),
            cancellationToken).ConfigureAwait(false);
        int adcValue = RfLabProtocol.ParseSensorPayload(response.Payload.Span);
        double millivolts = RfLabSensorConversion.MillivoltsFromAdcValue(adcValue);
        return new RfLabSensorReading(
            adcValue,
            millivolts,
            RfLabSensorConversion.LevelFromMillivolts(millivolts));
    }

    public ValueTask DisposeAsync() => session.DisposeAsync();

    private Task<McnfResponseFrame> ExchangeAsync(
        McnfRequestFrame request,
        CancellationToken cancellationToken) =>
        ExchangeSuccessfullyAsync(session, request, cancellationToken);

    private static async Task<McnfResponseFrame> ExchangeSuccessfullyAsync(
        McnfSession session,
        McnfRequestFrame request,
        CancellationToken cancellationToken)
    {
        McnfResponseFrame response = await session
            .ExchangeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            throw new McnfDeviceErrorException(
                response.ErrorCode,
                $"The board refused function 0x{request.Function:X2}: "
                + RfLabDeviceErrorCode.Describe(response.ErrorCode) + ".");
        }

        return response;
    }
}
