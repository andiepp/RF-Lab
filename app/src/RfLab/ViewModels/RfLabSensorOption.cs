using RfLab.Core;

namespace RfLab.ViewModels;

/// <summary>
/// One way of showing the detector input, named as the original RF-Lab
/// application named its sensors (the name is what a stored setting carries).
/// </summary>
public sealed record RfLabSensorOption(
    string Name,
    string Units,
    double Minimum,
    double Maximum,
    Func<RfLabSensorReading, double> Value)
{
    /// <summary>
    /// The AD8307 log detector with its 50 ohm input, in dB. The axis reaches
    /// below the conversion's -70 dB floor: the detector input of a quiet
    /// board sits at about 375 mV, which the transfer puts at about -72 dB.
    /// </summary>
    public static RfLabSensorOption Ad8307 { get; } = new(
        "AD8307_50",
        "dB",
        -80,
        RfLabSensorConversion.SensorLevelMaximum,
        reading => reading.Level);

    /// <summary>The converter input itself, in mV against the 2.56 V reference.</summary>
    public static RfLabSensorOption Converter { get; } = new(
        "AD_8",
        "mV",
        RfLabSensorConversion.SensorVoltageMinimum,
        RfLabSensorConversion.SensorVoltageMaximum,
        reading => reading.Millivolts);

    public override string ToString() => Name;
}
