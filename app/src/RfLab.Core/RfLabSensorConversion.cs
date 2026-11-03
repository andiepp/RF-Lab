namespace RfLab.Core;

/// <summary>
/// Converts the RF-Lab 10-bit sensor reading, taken against the 2.56 V
/// internal reference, into millivolts and into the characterized AD8307
/// 50 Ohm detector level in Decibel.
/// </summary>
public static class RfLabSensorConversion
{
    /// <summary>Millivolts per ADC count: 2560 mV across 1024 counts.</summary>
    private const double MillivoltsPerCount = 2.5;

    private const double DetectorFloorMillivolts = 200.0;
    private const double DetectorFloorLevel = -70.0;

    public const double SensorVoltageMinimum = 0.0;
    public const double SensorVoltageMaximum = 2560.0;

    public const double SensorLevelMinimum = -70.0;
    public const double SensorLevelMaximum = 10.0;

    public static double MillivoltsFromAdcValue(int adcValue)
    {
        if (adcValue is < 0 or > 1023)
        {
            throw new ArgumentOutOfRangeException(
                nameof(adcValue),
                adcValue,
                "The RF-Lab sensor reading must be a 10-bit value.");
        }

        return adcValue * MillivoltsPerCount;
    }

    /// <summary>
    /// The characterized AD8307 50 Ohm detector transfer:
    /// levels below the 200 mV floor report -70 dB.
    /// </summary>
    public static double LevelFromMillivolts(double millivolts)
    {
        if (millivolts < DetectorFloorMillivolts)
        {
            return DetectorFloorLevel;
        }

        return -(2235.0 - millivolts) / 25.917 - 0.5;
    }
}
