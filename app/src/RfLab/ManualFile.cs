using System.IO;
using System.Reflection;
using RfLab.Settings;

namespace RfLab;

/// <summary>
/// The manual travels inside the program and is written out to
/// %LocalAppData%\RF-Lab when it is opened, so a single copied exe has it.
/// </summary>
public static class ManualFile
{
    private const string ResourceName = "RfLab.Manual.pdf";

    public static string Extract()
    {
        using Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("This build carries no manual.");
        Directory.CreateDirectory(Preferences.DefaultDirectory);
        string path = Path.Combine(Preferences.DefaultDirectory, "RF-Lab Manual.pdf");
        try
        {
            using FileStream target = File.Create(path);
            source.CopyTo(target);
        }
        catch (IOException) when (File.Exists(path))
        {
            // open in a viewer already: that copy is the same manual
        }

        return path;
    }
}
