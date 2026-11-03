using System.Globalization;

namespace RfLab.Core.Presets;

/// <summary>
/// One stored setting in the original RF-Lab format: a flat list of
/// <c>Key,Value</c> lines.
/// </summary>
/// <remarks>
/// A file may carry keys this application does not use (the original's
/// message generator among them). They are kept in their place, so a file
/// written by the original application survives being saved again here.
/// A key the file does not carry is <c>null</c>, never zero.
/// </remarks>
public sealed class RfLabPreset
{
    public const string NameKey = "Name";
    public const string DescriptionKey = "Description";
    public const string ModeKey = "Mode";
    public const string FrequencyKey = "Frequency";
    public const string AmplitudeKey = "Amplitude";
    public const string ModulationFrequencyKey = "FMod";
    public const string AmplitudeModulationDepthKey = "AMDepth";
    public const string FrequencyDeviationKey = "Fdev";
    public const string SweepModeKey = "SweepMode";
    public const string SensorKey = "Sensor";
    public const string SweepStartFrequencyKey = "Fstart";
    public const string SweepStopFrequencyKey = "Fstop";
    public const string SweepTimeKey = "Tsweep";
    public const string MeasurementIntervalKey = "Tmeasure";
    public const string MeasurementCountKey = "Nmeasure";
    public const string ClockFrequency0Key = "SI5351Fclk0";
    public const string ClockFrequency1Key = "SI5351Fclk1";
    public const string ClockFrequency2Key = "SI5351Fclk2";

    // Keys in file order; values by key, case-insensitive as the original read them.
    private readonly List<string> keys = [];
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    public RfLabPreset(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The preset's name: its file name without the extension.</summary>
    public string Name { get; private set; }

    public string? Description
    {
        get => Text(DescriptionKey);
        set => Set(DescriptionKey, value ?? string.Empty);
    }

    public int? Mode => Number(ModeKey);

    public int? Frequency => Number(FrequencyKey);

    public int? Amplitude => Number(AmplitudeKey);

    public int? ModulationFrequency => Number(ModulationFrequencyKey);

    public int? AmplitudeModulationDepth => Number(AmplitudeModulationDepthKey);

    public int? FrequencyDeviation => Number(FrequencyDeviationKey);

    public string? SweepMode => Text(SweepModeKey);

    public string? Sensor => Text(SensorKey);

    public int? SweepStartFrequency => Number(SweepStartFrequencyKey);

    public int? SweepStopFrequency => Number(SweepStopFrequencyKey);

    public int? SweepTime => Number(SweepTimeKey);

    public int? MeasurementInterval => Number(MeasurementIntervalKey);

    public int? MeasurementCount => Number(MeasurementCountKey);

    public int? ClockFrequency0 => Number(ClockFrequency0Key);

    public int? ClockFrequency1 => Number(ClockFrequency1Key);

    public int? ClockFrequency2 => Number(ClockFrequency2Key);

    /// <summary>All keys in file order.</summary>
    public IReadOnlyList<string> Keys => keys;

    public string? this[string key] => values.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// Reads a preset from a file's lines. A malformed line is skipped rather
    /// than failing the file: one bad line should not cost a whole preset.
    /// </summary>
    public static RfLabPreset FromLines(string name, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var preset = new RfLabPreset(name);
        foreach (string line in lines)
        {
            int separator = line.IndexOf(',');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            if (key.Length > 0)
            {
                preset.Set(key, line[(separator + 1)..].Trim());
            }
        }

        // the file name is the name; a stale Name line is corrected on save
        preset.Set(NameKey, name);
        return preset;
    }

    /// <summary>The file's lines, starting with Name and Description as the original wrote them.</summary>
    public IEnumerable<string> ToLines()
    {
        Set(NameKey, Name);
        if (!values.ContainsKey(DescriptionKey))
        {
            Set(DescriptionKey, string.Empty);
        }

        yield return $"{NameKey},{values[NameKey]}";
        yield return $"{DescriptionKey},{values[DescriptionKey]}";
        foreach (string key in keys)
        {
            if (!key.Equals(NameKey, StringComparison.OrdinalIgnoreCase)
                && !key.Equals(DescriptionKey, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{key},{values[key]}";
            }
        }
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Set(NameKey, name);
    }

    /// <summary>Sets a value; a new key goes to the end, an existing one stays in its place.</summary>
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        // a comma would start a second value, a line break a second line
        value = value.Replace("\r", " ").Replace("\n", " ");
        if (!values.ContainsKey(key))
        {
            keys.Add(key);
        }

        values[key] = value;
    }

    public void Set(string key, int value) =>
        Set(key, value.ToString(CultureInfo.InvariantCulture));

    private string? Text(string key) =>
        values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private int? Number(string key) =>
        values.TryGetValue(key, out string? value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : null;
}
