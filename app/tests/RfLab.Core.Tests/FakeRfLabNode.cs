namespace RfLab.Core.Tests;

/// <summary>
/// A byte-level stand-in for the RF-Lab firmware: answers the connectivity
/// test and every framed request the client sends, and records them.
/// </summary>
internal sealed class FakeRfLabNode : IMcnfByteStream
{
    private readonly Queue<byte> output = new();

    public List<(byte Function, byte[] Parameters)> Requests { get; } = [];

    public bool Led { get; set; } = true;

    public bool Si5351Present { get; set; } = true;

    public int SensorAdcValue { get; set; } = 400;

    /// <summary>When set, the device channel answers every application function with this error byte.</summary>
    public byte? ErrorCode { get; set; }

    public byte[] NodeType { get; set; } = [0xAE, 0x70, 0x10, 0x80];

    /// <summary>Text printed before the answer to AM and FM, as the RF-Lab 1.0 firmware does.</summary>
    public string ModulationChatter { get; set; } = "highByte((uint16_t)Step_Rate)=0\r\nFM wave saved to RAM...\r\n";

    public bool Disposed { get; private set; }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        byte[] frame = bytes.ToArray();
        if (frame is [McnfConstants.ConnectivityTestChannel])
        {
            output.Enqueue(McnfConstants.ConnectivityTestResponse);
            return ValueTask.CompletedTask;
        }

        Assert.True(McnfChecksum.IsValid(frame), "request checksum");
        Assert.Equal(frame.Length - McnfConstants.HeaderSize, frame[1]);
        byte channel = frame[0];
        byte function = frame[4];
        byte[] parameters = frame[5..^1];
        Requests.Add((function, parameters));
        if (channel != McnfConstants.NodeAdminChannel
            && function is RfLabDeviceFunctions.DdsSetAmplitudeModulation or RfLabDeviceFunctions.DdsSetFrequencyModulation)
        {
            foreach (char character in ModulationChatter)
            {
                output.Enqueue((byte)character);
            }
        }

        Answer(Respond(channel, function, parameters), frame[2]);
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Assert.NotEmpty(output);
        int count = 0;
        while (count < buffer.Length && output.Count > 0)
        {
            buffer.Span[count++] = output.Dequeue();
        }

        return ValueTask.FromResult(count);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private (byte Error, byte[] Payload) Respond(byte channel, byte function, byte[] parameters)
    {
        if (channel == McnfConstants.NodeAdminChannel)
        {
            return function switch
            {
                McnfConstants.FunctionNodeGetTypeInfo => (0, NodeType),
                McnfConstants.FunctionNodeGetBufferSize => (0, [64]),
                _ => ((byte)McnfNodeErrorCode.UnknownFunction, []),
            };
        }

        if (function == McnfConstants.FunctionReadConfiguration)
        {
            return (0, [1, 1, 0, (byte)((Led ? 1 : 0) | (Si5351Present ? 2 : 0))]);
        }

        if (ErrorCode is byte error)
        {
            return (error, []);
        }

        switch (function)
        {
            case RfLabDeviceFunctions.LedOn:
                Led = true;
                return (0, [1]);
            case RfLabDeviceFunctions.LedOff:
                Led = false;
                return (0, [0]);
            case RfLabDeviceFunctions.LedGetState:
                return (0, [(byte)(Led ? 1 : 0)]);
            case RfLabDeviceFunctions.MeasureReadSensor:
                return (0, [(byte)(SensorAdcValue >> 8), (byte)SensorAdcValue]);
            case RfLabDeviceFunctions.Si5351SetClock when !Si5351Present:
                return (RfLabDeviceErrorCode.Si5351Disconnected, []);
            default:
                return (0, []);
        }
    }

    private void Answer((byte Error, byte[] Payload) response, int responseLength)
    {
        var frame = new byte[responseLength];
        frame[0] = response.Error;
        response.Payload.AsSpan(0, Math.Min(response.Payload.Length, responseLength - 2)).CopyTo(frame.AsSpan(1));
        frame[^1] = McnfChecksum.Compute(frame.AsSpan(0, frame.Length - 1));
        foreach (byte value in frame)
        {
            output.Enqueue(value);
        }
    }
}
