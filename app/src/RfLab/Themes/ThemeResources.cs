using System.Collections.ObjectModel;
using System.Windows;

namespace RfLab.Themes;

/// <summary>
/// The themes the application offers. Every theme defines the same keys
/// (Themes/Classic.xaml lists them all); switching a theme replaces the
/// application's dictionaries and rebuilds the main window, so a control may
/// read a theme value once, when it is built.
/// </summary>
public static class ThemeResources
{
    public const string Classic = "Classic";
    public const string StudioDark = "Studio dark";
    public const string StudioLight = "Studio light";

    public static IReadOnlyList<string> Names { get; } = [Classic, StudioDark, StudioLight];

    /// <summary>The name to use for a stored or unknown name.</summary>
    public static string Normalize(string? name) =>
        Names.FirstOrDefault(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
        ?? Classic;

    /// <summary>Replaces the application's theme dictionaries.</summary>
    public static void Apply(Application application, string name)
    {
        ArgumentNullException.ThrowIfNull(application);
        name = Normalize(name);
        Collection<ResourceDictionary> merged = application.Resources.MergedDictionaries;
        merged.Clear();
        if (name == Classic)
        {
            merged.Add(Load("Classic.xaml"));
        }
        else
        {
            merged.Add(Load(name == StudioDark ? "StudioDark.xaml" : "StudioLight.xaml"));
            merged.Add(Load("StudioStyles.xaml"));
        }

        merged.Add(Load("Styles.xaml"));
    }

    private static ResourceDictionary Load(string file) =>
        new() { Source = new Uri($"pack://application:,,,/RfLab;component/Themes/{file}", UriKind.Absolute) };
}
