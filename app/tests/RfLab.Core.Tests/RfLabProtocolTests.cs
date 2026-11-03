using RfLab.Core.Mcnf;

namespace RfLab.Core.Tests;

public sealed class RfLabProtocolTests
{
    [Fact]
    public void CarrierRequest_MatchesCharacterizedFrame()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateCarrierRequest(10_000_000, 10);

        Assert.Equal(
            new byte[] { 0xA5, 0x08, 0x02, 0x00, 0x10, 0x00, 0x98, 0x96, 0x80, 0x00, 0x0A, 0x88 },
            frame.Bytes.ToArray());
        Assert.Equal(2, frame.ResponseLength);
    }

    [Fact]
    public void CarrierRequest_EncodesAttenuationAsPositiveMagnitude()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateCarrierRequest(100_000, 80);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(0x00, bytes[9]);
        Assert.Equal(80, bytes[10]);
    }

    [Theory]
    [InlineData(99_999u)]
    [InlineData(300_000_001u)]
    public void CarrierRequest_RejectsFrequenciesOutsideTheCharacterizedRange(uint frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateCarrierRequest(frequency, 10));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(81)]
    public void CarrierRequest_RejectsAttenuationOutsideTheCharacterizedRange(int attenuation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateCarrierRequest(10_000_000, attenuation));
    }

    [Fact]
    public void AmplitudeModulationRequest_MatchesCharacterizedParameterLayout()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateAmplitudeModulationRequest(
            carrierFrequencyHertz: 0x01020304,
            attenuationDecibel: 0x21,
            modulationFrequencyHertz: 0x00011234,
            depthPercent: 85);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(0xA5, bytes[0]);
        Assert.Equal(16, bytes[1]);
        Assert.Equal(2, bytes[2]);
        Assert.Equal(0x11, bytes[4]);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bytes[5..9]);
        Assert.Equal(new byte[] { 0x00, 0x21 }, bytes[9..11]);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x12, 0x34 }, bytes[11..15]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 85 }, bytes[15..19]);
        Assert.Equal(McnfChecksum.Compute(bytes.AsSpan(0, bytes.Length - 1)), bytes[^1]);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(100_000u)]
    public void AmplitudeModulationRequest_RejectsModulationFrequenciesOutsideTheRange(
        uint modulationFrequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateAmplitudeModulationRequest(
                10_000_000, 10, modulationFrequency, 50));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void AmplitudeModulationRequest_RejectsDepthsOutsideTheRange(int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateAmplitudeModulationRequest(
                10_000_000, 10, 1_000, depth));
    }

    [Fact]
    public void FrequencyModulationRequest_MatchesCharacterizedParameterLayout()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateFrequencyModulationRequest(
            carrierFrequencyHertz: 10_000_000,
            attenuationDecibel: 20,
            modulationFrequencyHertz: 1_000,
            deviationHertz: 50_000);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(0x12, bytes[4]);
        Assert.Equal(new byte[] { 0x00, 0x98, 0x96, 0x80 }, bytes[5..9]);
        Assert.Equal(new byte[] { 0x00, 0x14 }, bytes[9..11]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0xE8 }, bytes[11..15]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0xC3, 0x50 }, bytes[15..19]);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1_000_000u)]
    public void FrequencyModulationRequest_RejectsDeviationsOutsideTheRange(uint deviation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateFrequencyModulationRequest(
                10_000_000, 10, 1_000, deviation));
    }

    [Theory]
    [InlineData(RfLabSweepMode.Bidirectional, 0)]
    [InlineData(RfLabSweepMode.Ramp, 1)]
    [InlineData(RfLabSweepMode.SingleRamp, 2)]
    public void SweepRequest_MatchesCharacterizedParameterLayout(
        RfLabSweepMode sweepMode,
        byte expectedModeByte)
    {
        McnfRequestFrame frame = RfLabProtocol.CreateSweepRequest(
            startFrequencyHertz: 10_000_000,
            stopFrequencyHertz: 30_000_000,
            sweepTimeMilliseconds: 2_000,
            attenuationDecibel: 30,
            sweepMode);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(15, bytes[1]);
        Assert.Equal(0x13, bytes[4]);
        Assert.Equal(new byte[] { 0x00, 0x98, 0x96, 0x80 }, bytes[5..9]);
        Assert.Equal(new byte[] { 0x01, 0xC9, 0xC3, 0x80 }, bytes[9..13]);
        Assert.Equal(new byte[] { 0x07, 0xD0 }, bytes[13..15]);
        Assert.Equal(new byte[] { 0x00, 0x1E }, bytes[15..17]);
        Assert.Equal(expectedModeByte, bytes[17]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16_001)]
    public void SweepRequest_RejectsSweepTimesOutsideTheRange(int sweepTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateSweepRequest(
                10_000_000, 30_000_000, sweepTime, 30, RfLabSweepMode.Ramp));
    }

    [Fact]
    public void ReadSensorRequest_ExpectsFourResponseBytes()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateReadSensorRequest();

        Assert.Equal(0x20, frame.Function);
        Assert.Equal(4, frame.ResponseLength);
    }

    [Fact]
    public void ParseSensorPayload_ReadsTenBitValueMostSignificantByteFirst()
    {
        Assert.Equal(0x029A, RfLabProtocol.ParseSensorPayload([0x02, 0x9A]));
        Assert.Equal(0, RfLabProtocol.ParseSensorPayload([0x00, 0x00]));
        Assert.Equal(1023, RfLabProtocol.ParseSensorPayload([0x03, 0xFF]));
    }

    [Fact]
    public void ParseSensorPayload_RejectsValuesBeyondTheConverterRange()
    {
        Assert.Throws<InvalidDataException>(
            () => RfLabProtocol.ParseSensorPayload([0x04, 0x00]));
        Assert.Throws<InvalidDataException>(
            () => RfLabProtocol.ParseSensorPayload([0x9A]));
    }

    [Theory]
    [InlineData(true, 0x01)]
    [InlineData(false, 0x02)]
    public void IndicatorRequest_UsesTheCharacterizedFunctionCodes(bool enable, byte function)
    {
        McnfRequestFrame frame = RfLabProtocol.CreateIndicatorRequest(enable);

        Assert.Equal(function, frame.Function);
        Assert.Equal(3, frame.ResponseLength);
    }

    [Fact]
    public void IndicatorStateRequest_UsesTheCharacterizedFunctionCode()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateIndicatorStateRequest();

        Assert.Equal(0x03, frame.Function);
        Assert.Equal(3, frame.ResponseLength);
    }

    [Fact]
    public void ParseIndicatorPayload_ReadsBooleanState()
    {
        Assert.True(RfLabProtocol.ParseIndicatorPayload([0x01]));
        Assert.False(RfLabProtocol.ParseIndicatorPayload([0x00]));
        Assert.Throws<InvalidDataException>(
            () => RfLabProtocol.ParseIndicatorPayload([0x02]));
        Assert.Throws<InvalidDataException>(
            () => RfLabProtocol.ParseIndicatorPayload([0x01, 0x00]));
    }

    [Fact]
    public void ClockRequest_ScalesTheFrequencyToHundredthsOfHertz()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateClockRequest(1, 1_000_000);

        byte[] bytes = frame.Bytes.ToArray();
        Assert.Equal(0x40, bytes[4]);
        Assert.Equal(1, bytes[5]);
        // 1 MHz in 0.01 Hz units: 100_000_000 = 0x05F5E100, 64-bit MSB first.
        Assert.Equal(
            new byte[] { 0x00, 0x00, 0x00, 0x00, 0x05, 0xF5, 0xE1, 0x00 },
            bytes[6..14]);
        Assert.Equal(2, frame.ResponseLength);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void ClockRequest_RejectsChannelsOutsideTheGenerator(int channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateClockRequest(channel, 1_000_000));
    }

    [Theory]
    [InlineData(9_999u)]
    [InlineData(160_000_001u)]
    public void ClockRequest_RejectsFrequenciesOutsideTheCharacterizedRange(uint frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RfLabProtocol.CreateClockRequest(0, frequency));
    }

    [Fact]
    public void ReadConfigurationRequest_TargetsDeviceOneOnTheFirstChannel()
    {
        McnfRequestFrame frame = RfLabProtocol.CreateReadConfigurationRequest();

        Assert.Equal(
            new byte[] { 0xA5, 0x06, 0x06, 0x00, 0xC9, 0x01, 0x00, 0x00, 0x00, 0x84 },
            frame.Bytes.ToArray());
    }

    [Fact]
    public void ParseConfigurationPayload_ReadsIndicatorAndGeneratorBits()
    {
        RfLabConfiguration configuration =
            RfLabProtocol.ParseConfigurationPayload([0x00, 0x00, 0x00, 0b11]);
        Assert.True(configuration.LedOn);
        Assert.True(configuration.Si5351Present);

        configuration = RfLabProtocol.ParseConfigurationPayload([0x00, 0x00, 0x00, 0b10]);
        Assert.False(configuration.LedOn);
        Assert.True(configuration.Si5351Present);

        configuration = RfLabProtocol.ParseConfigurationPayload([0x02, 0x01, 0x04, 0b00]);
        Assert.False(configuration.LedOn);
        Assert.False(configuration.Si5351Present);
        Assert.Equal(2, configuration.VariableSetCount);
        Assert.Equal(1, configuration.ActiveVariableSet);
        Assert.Equal(4, configuration.Capabilities);
    }

    [Fact]
    public void ParseConfigurationPayload_RejectsUnexpectedLengths()
    {
        Assert.Throws<InvalidDataException>(
            () => RfLabProtocol.ParseConfigurationPayload([0x00, 0x00, 0x00]));
    }
}
