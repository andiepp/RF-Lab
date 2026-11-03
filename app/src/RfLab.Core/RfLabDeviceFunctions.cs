namespace RfLab.Core;

/// <summary>
/// RF-Lab device function codes on the MCNF device channel, as characterized
/// from the RF-Lab node firmware. The message-generator functions 0x30-0x32
/// are deliberately absent: the characterized firmware revision stores their
/// parameters but never transmits a pattern.
/// </summary>
public static class RfLabDeviceFunctions
{
    public const byte LedOn = 0x01;
    public const byte LedOff = 0x02;
    public const byte LedGetState = 0x03;
    public const byte DdsSetFrequencyAmplitude = 0x10;
    public const byte DdsSetAmplitudeModulation = 0x11;
    public const byte DdsSetFrequencyModulation = 0x12;
    public const byte DdsSweep = 0x13;
    public const byte MeasureReadSensor = 0x20;
    public const byte Si5351SetClock = 0x40;
}
