using System.IO;

namespace RfLab.Settings;

/// <summary>
/// The application's own few preferences (theme, serial port), kept as
/// <c>key=value</c> lines in %LocalAppData%\RF-Lab\preferences.txt. A missing
/// or unreadable file means defaults; a failed write is not worth an error.
/// </summary>
public sealed class Preferences
{
    private readonly string path;
    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    public Preferences(string path)
    {
        this.path = path;
        try
        {
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    int separator = line.IndexOf('=');
                    if (separator > 0)
                    {
                        values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RF-Lab");

    public static Preferences Default { get; } =
        new(Path.Combine(DefaultDirectory, "preferences.txt"));

    public string? Theme
    {
        get => Get("theme");
        set => Set("theme", value);
    }

    public string? Port
    {
        get => Get("port");
        set => Set("port", value);
    }

    private string? Get(string key) => values.TryGetValue(key, out string? value) ? value : null;

    private void Set(string key, string? value)
    {
        if (value is null)
        {
            values.Remove(key);
        }
        else
        {
            values[key] = value;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, values.Select(pair => $"{pair.Key}={pair.Value}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
