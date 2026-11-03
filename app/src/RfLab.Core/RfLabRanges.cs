namespace RfLab.Core;

/// <summary>
/// Characterized RF-Lab value limits. Frequencies are in Hertz, attenuation
/// in Decibel below full scale, the sweep time in milliseconds.
/// </summary>
public static class RfLabRanges
{
    public const uint CarrierFrequencyMinimum = 100_000;
    public const uint CarrierFrequencyMaximum = 300_000_000;

    public const int AttenuationMinimum = 0;
    public const int AttenuationMaximum = 80;

    public const uint ModulationFrequencyMinimum = 1;
    public const uint ModulationFrequencyMaximum = 99_999;

    public const int AmplitudeModulationDepthMinimum = 0;
    public const int AmplitudeModulationDepthMaximum = 99;

    public const uint FrequencyModulationDeviationMinimum = 1;
    public const uint FrequencyModulationDeviationMaximum = 999_999;

    public const int SweepTimeMillisecondsMinimum = 1;
    public const int SweepTimeMillisecondsMaximum = 16_000;

    public const uint ClockFrequencyMinimum = 10_000;
    public const uint ClockFrequencyMaximum = 160_000_000;
}
