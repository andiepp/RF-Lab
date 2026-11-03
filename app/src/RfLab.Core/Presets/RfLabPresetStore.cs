using System.Text;

namespace RfLab.Core.Presets;

/// <summary>
/// The stored settings: one <c>.txt</c> file per preset in one folder, named
/// as the original RF-Lab application named them.
/// </summary>
/// <remarks>
/// Listing and reading treat every failure as an absence (a missing folder
/// means no presets, not an application that will not start). Saving,
/// renaming and deleting report their failures, because the operator asked
/// for them.
/// </remarks>
public sealed class RfLabPresetStore
{
    private const string Extension = ".txt";

    public RfLabPresetStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        DirectoryPath = Path.GetFullPath(directoryPath);
    }

    /// <summary>%LocalAppData%\RF-Lab\Presets: kept across application updates.</summary>
    public static string DefaultDirectoryPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF-Lab",
            "Presets");

    public string DirectoryPath { get; }

    /// <summary>Whether Delete moves files to the recycle bin (on Windows) rather than deleting them.</summary>
    public bool UsesRecycleBin { get; init; } = true;

    /// <summary>
    /// Creates the folder and copies the example presets into it, once: when
    /// the folder does not exist yet. A folder the operator emptied stays empty.
    /// </summary>
    public bool SeedIfMissing(string? examplesDirectory)
    {
        if (Directory.Exists(DirectoryPath))
        {
            return false;
        }

        Directory.CreateDirectory(DirectoryPath);
        if (examplesDirectory is null || !Directory.Exists(examplesDirectory))
        {
            return true;
        }

        foreach (string file in Directory.EnumerateFiles(examplesDirectory, "*" + Extension))
        {
            File.Copy(file, Path.Combine(DirectoryPath, Path.GetFileName(file)), overwrite: false);
        }

        return true;
    }

    public IReadOnlyList<string> ListNames()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return [];
            }

            return [.. Directory
                .EnumerateFiles(DirectoryPath, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(name => name.Length > 0)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public bool Exists(string name) =>
        ValidateName(name) is null && File.Exists(PathOf(name));

    /// <summary>Reads one preset, or <c>null</c> when it cannot be read.</summary>
    public RfLabPreset? Read(string name)
    {
        if (ValidateName(name) is not null)
        {
            return null;
        }

        try
        {
            string path = PathOf(name);
            return File.Exists(path)
                ? RfLabPreset.FromLines(name, File.ReadAllLines(path))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes a preset, replacing a file of the same name.</summary>
    public void Save(RfLabPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ThrowIfInvalid(preset.Name);
        Directory.CreateDirectory(DirectoryPath);

        // write beside, then replace: a failed write leaves the old file intact
        string path = PathOf(preset.Name);
        string temporary = path + ".saving";
        File.WriteAllLines(temporary, preset.ToLines(), new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Renames a preset's file and its Name line.</summary>
    public void Rename(string name, string newName)
    {
        ThrowIfInvalid(name);
        ThrowIfInvalid(newName);
        RfLabPreset preset = Read(name)
            ?? throw new FileNotFoundException($"The preset '{name}' does not exist.");

        bool caseOnly = string.Equals(name, newName, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && File.Exists(PathOf(newName)))
        {
            throw new IOException($"A preset named '{newName}' exists already.");
        }

        preset.Rename(newName);
        if (caseOnly)
        {
            File.Delete(PathOf(name));
        }

        Save(preset);
        if (!caseOnly)
        {
            File.Delete(PathOf(name));
        }
    }

    /// <summary>Deletes a preset; to the recycle bin where Windows offers one.</summary>
    public void Delete(string name)
    {
        ThrowIfInvalid(name);
        string path = PathOf(name);
        if (!File.Exists(path))
        {
            return;
        }

        if (UsesRecycleBin && OperatingSystem.IsWindows())
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return;
        }

        File.Delete(path);
    }

    /// <summary>Why a name cannot be a preset name, or <c>null</c> when it can.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a name.";
        }

        if (name != name.Trim() || name.EndsWith('.'))
        {
            return "A name cannot start or end with a space, or end with a dot.";
        }

        if (name.Length > 80)
        {
            return "A name can have up to 80 characters.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(','))
        {
            return "A name cannot contain \\ / : * ? \" < > | or a comma.";
        }

        return null;
    }

    private static void ThrowIfInvalid(string name)
    {
        string? problem = ValidateName(name);
        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(name));
        }
    }

    private string PathOf(string name) => Path.Combine(DirectoryPath, name + Extension);
}
