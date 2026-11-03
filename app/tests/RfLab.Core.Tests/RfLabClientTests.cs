using System.Buffers.Binary;

namespace RfLab.Core.Tests;

public sealed class RfLabClientTests
{
    [Fact]
    public async Task Connect_ReadsIdentityBufferAndConfiguration()
    {
        var node = new FakeRfLabNode { Led = false, Si5351Present = true };

        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        Assert.Equal("AE.70.10.80", client.Identity.NodeType);
        Assert.Equal(64, client.ReportedBufferSize);
        Assert.False(client.Configuration.LedOn);
        Assert.True(client.Configuration.Si5351Present);
        Assert.Equal("COM99", client.PortName);
    }

    [Fact]
    public async Task Connect_RejectsAnotherMcnfNodeAndReleasesTheStream()
    {
        var node = new FakeRfLabNode { NodeType = [0xAE, 0x70, 0x20, 0x80] };

        await Assert.ThrowsAsync<InvalidDataException>(() => RfLabClient.ConnectAsync(node, "COM99"));
        Assert.True(node.Disposed);
    }

    [Fact]
    public async Task Carrier_SendsFrequencyAndAttenuationMostSignificantByteFirst()
    {
        var node = new FakeRfLabNode();
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        await client.ApplyCarrierAsync(21_400_000, 40);

        (byte function, byte[] parameters) = node.Requests[^1];
        Assert.Equal(RfLabDeviceFunctions.DdsSetFrequencyAmplitude, function);
        Assert.Equal(21_400_000u, BinaryPrimitives.ReadUInt32BigEndian(parameters));
        Assert.Equal(40, BinaryPrimitives.ReadUInt16BigEndian(parameters.AsSpan(4)));
    }

    [Fact]
    public async Task Clock_SendsHundredthsOfHertz()
    {
        var node = new FakeRfLabNode();
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        await client.ApplyClockAsync(2, 3_333_000);

        (byte function, byte[] parameters) = node.Requests[^1];
        Assert.Equal(RfLabDeviceFunctions.Si5351SetClock, function);
        Assert.Equal(2, parameters[0]);
        Assert.Equal(333_300_000ul, BinaryPrimitives.ReadUInt64BigEndian(parameters.AsSpan(1)));
    }

    [Fact]
    public async Task Clock_WithoutSi5351_ReportsTheBoardsRefusalAndStaysConnected()
    {
        var node = new FakeRfLabNode { Si5351Present = false };
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        var refusal = await Assert.ThrowsAsync<McnfDeviceErrorException>(
            () => client.ApplyClockAsync(0, 1_000_000));

        Assert.Equal(RfLabDeviceErrorCode.Si5351Disconnected, refusal.ErrorCode);
        Assert.Contains("Si5351Disconnected", refusal.Message);
        Assert.False(client.IsFaulted);
    }

    [Fact]
    public async Task Modulation_SkipsTheTextTheFirmwarePrintsBeforeItsAnswer()
    {
        var node = new FakeRfLabNode();
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");
        var text = new List<string>();
        client.Trace = (direction, bytes) =>
        {
            if (direction == McnfTraceDirection.Text)
            {
                text.Add(System.Text.Encoding.ASCII.GetString(bytes.Span));
            }
        };

        await client.ApplyAmplitudeModulationAsync(10_000_000, 30, 1_000, 50);
        await client.ApplyFrequencyModulationAsync(10_000_000, 30, 1_000, 5_000);
        RfLabSensorReading reading = await client.ReadSensorAsync();

        Assert.Equal([node.ModulationChatter, node.ModulationChatter], text);
        Assert.Equal(node.SensorAdcValue, reading.AdcValue);
        Assert.False(client.IsFaulted);
    }

    [Fact]
    public async Task Indicator_ReturnsTheStateTheBoardReports()
    {
        var node = new FakeRfLabNode { Led = true };
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        Assert.False(await client.SetIndicatorAsync(false));
        Assert.False(await client.ReadIndicatorAsync());
        Assert.True(await client.SetIndicatorAsync(true));
    }

    [Fact]
    public async Task Sensor_ConvertsToMillivoltsAndLevel()
    {
        var node = new FakeRfLabNode { SensorAdcValue = 894 };
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        RfLabSensorReading reading = await client.ReadSensorAsync();

        Assert.Equal(894, reading.AdcValue);
        Assert.Equal(2235.0, reading.Millivolts);
        Assert.Equal(-0.5, reading.Level, 6);
    }

    [Fact]
    public async Task Sweep_CarriesSpanTimeAttenuationAndMode()
    {
        var node = new FakeRfLabNode();
        await using RfLabClient client = await RfLabClient.ConnectAsync(node, "COM99");

        await client.StartSweepAsync(10_000_000, 30_000_000, 2_000, 20, RfLabSweepMode.SingleRamp);

        (byte function, byte[] parameters) = node.Requests[^1];
        Assert.Equal(RfLabDeviceFunctions.DdsSweep, function);
        Assert.Equal(13, parameters.Length);
        Assert.Equal(10_000_000u, BinaryPrimitives.ReadUInt32BigEndian(parameters));
        Assert.Equal(30_000_000u, BinaryPrimitives.ReadUInt32BigEndian(parameters.AsSpan(4)));
        Assert.Equal(2_000, BinaryPrimitives.ReadUInt16BigEndian(parameters.AsSpan(8)));
        Assert.Equal(20, BinaryPrimitives.ReadUInt16BigEndian(parameters.AsSpan(10)));
        Assert.Equal((byte)RfLabSweepMode.SingleRamp, parameters[12]);
    }
}
