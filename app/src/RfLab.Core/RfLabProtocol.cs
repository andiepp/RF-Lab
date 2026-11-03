using System.Buffers.Binary;
using RfLab.Core.Mcnf;

namespace RfLab.Core;

/// <summary>
/// Builds the characterized RF-Lab MCNF requests and parses their response
/// payloads. All multi-byte values travel most-significant byte first;
/// attenuation travels as a positive magnitude which the node negates.
/// </summary>
public static class RfLabProtocol
{
    /// <summary>The single RF-Lab device lives on the first device channel.</summary>
    public const byte DeviceChannel = McnfConstants.DeviceChannelOffset;

    /// <summary>The single RF-Lab device carries device number one.</summary>
    public const ushort DeviceNumber = 1;

    /// <summary>The RF-Lab configuration payload length in bytes.</summary>
    public const int ConfigurationByteSize = 4;

    /// <summary>The RF-Lab node message buffer size in bytes.</summary>
    public const int NodeBufferSize = 128;

    /// <summary>The characterized serial baud rate of the RF-Lab node.</summary>
    public const int BaudRate = 115200;

    /// <summary>
    /// The Si5351 clock frequency resolution on the wire: hundredths of Hertz.
    /// </summary>
    private const int ClockFrequencyScale = 100;

    public static McnfRequestFrame CreateReadConfigurationRequest() =>
        McnfStandardDeviceRequests.CreateReadConfigurationRequest(
            DeviceChannel,
            DeviceNumber,
            ConfigurationByteSize);

    public static RfLabConfiguration ParseConfigurationPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != ConfigurationByteSize)
        {
            throw new InvalidDataException(
                "The RF-Lab configuration payload does not have the characterized length.");
        }

        return new RfLabConfiguration(
            VariableSetCount: payload[0],
            ActiveVariableSet: payload[1],
            Capabilities: payload[2],
            LedOn: (payload[3] & 0b01) != 0,
            Si5351Present: (payload[3] & 0b10) != 0);
    }

    public static McnfRequestFrame CreateIndicatorRequest(bool enable) =>
        McnfRequestFrame.Create(
            DeviceChannel,
            enable ? RfLabDeviceFunctions.LedOn : RfLabDeviceFunctions.LedOff,
            [],
            responseLength: 3);

    public static McnfRequestFrame CreateIndicatorStateRequest() =>
        McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.LedGetState,
            [],
            responseLength: 3);

    public static bool ParseIndicatorPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 1 || payload[0] > 1)
        {
            throw new InvalidDataException(
                "The RF-Lab indicator payload does not report a Boolean state.");
        }

        return payload[0] == 1;
    }

    public static McnfRequestFrame CreateCarrierRequest(
        uint frequencyHertz,
        int attenuationDecibel)
    {
        ValidateCarrierFrequency(frequencyHertz, nameof(frequencyHertz));
        ValidateAttenuation(attenuationDecibel);

        Span<byte> parameters = stackalloc byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(parameters, frequencyHertz);
        BinaryPrimitives.WriteUInt16BigEndian(
            parameters[4..],
            (ushort)attenuationDecibel);

        return McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.DdsSetFrequencyAmplitude,
            parameters,
            responseLength: 2);
    }

    public static McnfRequestFrame CreateAmplitudeModulationRequest(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        int depthPercent)
    {
        ValidateCarrierFrequency(carrierFrequencyHertz, nameof(carrierFrequencyHertz));
        ValidateAttenuation(attenuationDecibel);
        ValidateModulationFrequency(modulationFrequencyHertz);
        if (depthPercent is < RfLabRanges.AmplitudeModulationDepthMinimum
            or > RfLabRanges.AmplitudeModulationDepthMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depthPercent),
                depthPercent,
                "The RF-Lab amplitude-modulation depth is outside the characterized range.");
        }

        Span<byte> parameters = stackalloc byte[14];
        BinaryPrimitives.WriteUInt32BigEndian(parameters, carrierFrequencyHertz);
        BinaryPrimitives.WriteUInt16BigEndian(
            parameters[4..],
            (ushort)attenuationDecibel);
        BinaryPrimitives.WriteUInt32BigEndian(parameters[6..], modulationFrequencyHertz);
        BinaryPrimitives.WriteUInt32BigEndian(parameters[10..], (uint)depthPercent);

        return McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.DdsSetAmplitudeModulation,
            parameters,
            responseLength: 2);
    }

    public static McnfRequestFrame CreateFrequencyModulationRequest(
        uint carrierFrequencyHertz,
        int attenuationDecibel,
        uint modulationFrequencyHertz,
        uint deviationHertz)
    {
        ValidateCarrierFrequency(carrierFrequencyHertz, nameof(carrierFrequencyHertz));
        ValidateAttenuation(attenuationDecibel);
        ValidateModulationFrequency(modulationFrequencyHertz);
        if (deviationHertz is < RfLabRanges.FrequencyModulationDeviationMinimum
            or > RfLabRanges.FrequencyModulationDeviationMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deviationHertz),
                deviationHertz,
                "The RF-Lab frequency-modulation deviation is outside the characterized range.");
        }

        Span<byte> parameters = stackalloc byte[14];
        BinaryPrimitives.WriteUInt32BigEndian(parameters, carrierFrequencyHertz);
        BinaryPrimitives.WriteUInt16BigEndian(
            parameters[4..],
            (ushort)attenuationDecibel);
        BinaryPrimitives.WriteUInt32BigEndian(parameters[6..], modulationFrequencyHertz);
        BinaryPrimitives.WriteUInt32BigEndian(parameters[10..], deviationHertz);

        return McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.DdsSetFrequencyModulation,
            parameters,
            responseLength: 2);
    }

    public static McnfRequestFrame CreateSweepRequest(
        uint startFrequencyHertz,
        uint stopFrequencyHertz,
        int sweepTimeMilliseconds,
        int attenuationDecibel,
        RfLabSweepMode sweepMode)
    {
        ValidateCarrierFrequency(startFrequencyHertz, nameof(startFrequencyHertz));
        ValidateCarrierFrequency(stopFrequencyHertz, nameof(stopFrequencyHertz));
        ValidateAttenuation(attenuationDecibel);
        if (sweepTimeMilliseconds is < RfLabRanges.SweepTimeMillisecondsMinimum
            or > RfLabRanges.SweepTimeMillisecondsMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sweepTimeMilliseconds),
                sweepTimeMilliseconds,
                "The RF-Lab sweep time is outside the characterized range.");
        }

        if (!Enum.IsDefined(sweepMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sweepMode),
                sweepMode,
                "The RF-Lab sweep mode is not supported.");
        }

        Span<byte> parameters = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(parameters, startFrequencyHertz);
        BinaryPrimitives.WriteUInt32BigEndian(parameters[4..], stopFrequencyHertz);
        BinaryPrimitives.WriteUInt16BigEndian(
            parameters[8..],
            (ushort)sweepTimeMilliseconds);
        BinaryPrimitives.WriteUInt16BigEndian(
            parameters[10..],
            (ushort)attenuationDecibel);
        parameters[12] = (byte)sweepMode;

        return McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.DdsSweep,
            parameters,
            responseLength: 2);
    }

    public static McnfRequestFrame CreateReadSensorRequest() =>
        McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.MeasureReadSensor,
            [],
            responseLength: 4);

    public static int ParseSensorPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 2)
        {
            throw new InvalidDataException(
                "The RF-Lab sensor payload does not have the characterized length.");
        }

        int adcValue = BinaryPrimitives.ReadUInt16BigEndian(payload);
        if (adcValue > 1023)
        {
            throw new InvalidDataException(
                "The RF-Lab sensor payload exceeds the 10-bit converter range.");
        }

        return adcValue;
    }

    public static McnfRequestFrame CreateClockRequest(
        int clockChannel,
        uint frequencyHertz)
    {
        if (clockChannel is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clockChannel),
                clockChannel,
                "The Si5351 clock channel must be between 0 and 2.");
        }

        if (frequencyHertz is < RfLabRanges.ClockFrequencyMinimum
            or > RfLabRanges.ClockFrequencyMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequencyHertz),
                frequencyHertz,
                "The Si5351 clock frequency is outside the characterized range.");
        }

        Span<byte> parameters = stackalloc byte[9];
        parameters[0] = (byte)clockChannel;
        BinaryPrimitives.WriteUInt64BigEndian(
            parameters[1..],
            (ulong)frequencyHertz * ClockFrequencyScale);

        return McnfRequestFrame.Create(
            DeviceChannel,
            RfLabDeviceFunctions.Si5351SetClock,
            parameters,
            responseLength: 2);
    }

    private static void ValidateCarrierFrequency(uint frequencyHertz, string parameterName)
    {
        if (frequencyHertz is < RfLabRanges.CarrierFrequencyMinimum
            or > RfLabRanges.CarrierFrequencyMaximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                frequencyHertz,
                "The RF-Lab carrier frequency is outside the characterized range.");
        }
    }

    private static void ValidateAttenuation(int attenuationDecibel)
    {
        if (attenuationDecibel is < RfLabRanges.AttenuationMinimum
            or > RfLabRanges.AttenuationMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attenuationDecibel),
                attenuationDecibel,
                "The RF-Lab attenuation is outside the characterized range.");
        }
    }

    private static void ValidateModulationFrequency(uint modulationFrequencyHertz)
    {
        if (modulationFrequencyHertz is < RfLabRanges.ModulationFrequencyMinimum
            or > RfLabRanges.ModulationFrequencyMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modulationFrequencyHertz),
                modulationFrequencyHertz,
                "The RF-Lab modulation frequency is outside the characterized range.");
        }
    }
}
