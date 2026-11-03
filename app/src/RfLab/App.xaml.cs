using System.IO;
using System.Windows;
using RfLab.Core.Presets;
using RfLab.Settings;
using RfLab.Themes;
using RfLab.ViewModels;
using RfLab.Views;

namespace RfLab;

public partial class App : Application
{
    /// <summary>
    /// Starts the window. Arguments: <c>--port COMx</c> picks the port,
    /// <c>--connect</c> connects at start, <c>--theme "Studio dark"</c> picks a theme.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new RfLabPresetStore(RfLabPresetStore.DefaultDirectoryPath);
        try
        {
            store.SeedIfMissing(Path.Combine(AppContext.BaseDirectory, "presets"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // no examples then; the operator can still store settings
        }

        Preferences preferences = Preferences.Default;
        string? theme = Argument(e.Args, "--theme");
        if (theme is not null)
        {
            preferences.Theme = ThemeResources.Normalize(theme);
        }

        var viewModel = new MainViewModel(store, preferences);
        viewModel.ThemeChangeRequested += (_, name) => SwitchTheme(name);
        ThemeResources.Apply(this, viewModel.SelectedTheme);

        if (Argument(e.Args, "--port") is { } port)
        {
            viewModel.SelectedPort = port.ToUpperInvariant();
        }

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();

        if (e.Args.Contains("--connect", StringComparer.OrdinalIgnoreCase))
        {
            _ = viewModel.ConnectAsync();
        }
    }

    /// <summary>
    /// Applies a theme and rebuilds the main window over the same view
    /// model: the connection and the values stay as they are.
    /// </summary>
    private void SwitchTheme(string theme)
    {
        if (MainWindow is not MainWindow current)
        {
            return;
        }

        // after the theme picker has finished its own selection change
        Dispatcher.BeginInvoke(() =>
        {
            ThemeResources.Apply(this, theme);
            var next = new MainWindow(current.ViewModel)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = current.Left,
                Top = current.Top,
            };
            MainWindow = next;
            next.Show();
            current.CloseKeepingViewModel();
        });
    }

    private static string? Argument(string[] args, string name)
    {
        int index = Array.FindIndex(args, arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
