using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RfLab.ViewModels;

namespace RfLab.Views;

/// <summary>
/// The RF-Lab window: the original application's layout with a connection
/// bar, stored-setting editing and a byte trace.
/// </summary>
/// <remarks>
/// The digit and dial controls carry their value as a plain property with a
/// change event, not as a dependency property, exactly as in the original.
/// They cannot be bound, so this window carries the values both ways: each
/// control's change reaches the view model, and each value the view model
/// changes (loading a stored setting changes a dozen) is pushed back into
/// its control. A control added without being wired here does not follow.
/// </remarks>
public partial class MainWindow : Window, IRfLabDialogs
{
    private bool keepViewModel;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        ManualCommand = new RelayCommand(OpenManual);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Dialogs = this;
    }

    public MainViewModel ViewModel { get; }

    public ICommand ManualCommand { get; }

    /// <summary>Closes this window for a rebuilt one, leaving the connection open.</summary>
    public void CloseKeepingViewModel()
    {
        keepViewModel = true;
        Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        PushAllValues();

        FrequencyDial.ValueChanged += (_, args) => ViewModel.Frequency = args.Value;
        AmplitudeDial.ValueChanged += (_, args) => ViewModel.Amplitude = args.Value;
        NCMD_Fmod.ValueChanged += (_, args) => ViewModel.ModulationFrequency = args.Value;
        NCMD_AMdepth.ValueChanged += (_, args) => ViewModel.AmplitudeModulationDepth = args.Value;
        NCMD_FMdev.ValueChanged += (_, args) => ViewModel.FrequencyDeviation = args.Value;
        NCMD_Fstart.ValueChanged += (_, args) => ViewModel.SweepStartFrequency = args.Value;
        NCMD_Fstop.ValueChanged += (_, args) => ViewModel.SweepStopFrequency = args.Value;
        NCMD_Tsweep.ValueChanged += (_, args) => ViewModel.SweepTime = args.Value;
        NCMD_Tmeasure.ValueChanged += (_, args) => ViewModel.MeasurementInterval = args.Value;
        NCMD_Nmeasure.ValueChanged += (_, args) => ViewModel.MeasurementCount = args.Value;
        NCMD_SI5351_Fclk0.ValueChanged += (_, args) => ViewModel.ClockFrequency0 = args.Value;
        NCMD_SI5351_Fclk1.ValueChanged += (_, args) => ViewModel.ClockFrequency1 = args.Value;
        NCMD_SI5351_Fclk2.ValueChanged += (_, args) => ViewModel.ClockFrequency2 = args.Value;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.TraceLines.CollectionChanged += OnTraceChanged;
    }

    private void PresetList_MouseDoubleClick(object sender, MouseButtonEventArgs eventArgs)
    {
        if (PresetList.SelectedItem is not null && ViewModel.ApplyPresetCommand.CanExecute(null))
        {
            ViewModel.ApplyPresetCommand.Execute(null);
        }
    }

    /// <summary>The trace list is built when its tab first opens: show the newest line then.</summary>
    private void TraceList_Loaded(object sender, RoutedEventArgs eventArgs) => OnTraceChanged(this, null);

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.TraceLines.CollectionChanged -= OnTraceChanged;
        if (!keepViewModel)
        {
            ViewModel.Dispose();
        }
    }

    /// <summary>
    /// Carries a value the view model changed into its control. The digit
    /// controls are told not to raise their change event, so the value does
    /// not travel back; the dials' echo is harmless (the value is already held).
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(MainViewModel.Frequency):
                FrequencyDial.Frequency = ViewModel.Frequency;
                break;
            case nameof(MainViewModel.Amplitude):
                AmplitudeDial.Amplitude = ViewModel.Amplitude;
                break;
            case nameof(MainViewModel.ModulationFrequency):
                NCMD_Fmod.SetValue(ViewModel.ModulationFrequency, false);
                break;
            case nameof(MainViewModel.AmplitudeModulationDepth):
                NCMD_AMdepth.SetValue(ViewModel.AmplitudeModulationDepth, false);
                break;
            case nameof(MainViewModel.FrequencyDeviation):
                NCMD_FMdev.SetValue(ViewModel.FrequencyDeviation, false);
                break;
            case nameof(MainViewModel.SweepStartFrequency):
                NCMD_Fstart.SetValue(ViewModel.SweepStartFrequency, false);
                break;
            case nameof(MainViewModel.SweepStopFrequency):
                NCMD_Fstop.SetValue(ViewModel.SweepStopFrequency, false);
                break;
            case nameof(MainViewModel.SweepTime):
                NCMD_Tsweep.SetValue(ViewModel.SweepTime, false);
                break;
            case nameof(MainViewModel.MeasurementInterval):
                NCMD_Tmeasure.SetValue(ViewModel.MeasurementInterval, false);
                break;
            case nameof(MainViewModel.MeasurementCount):
                NCMD_Nmeasure.SetValue(ViewModel.MeasurementCount, false);
                break;
            case nameof(MainViewModel.ClockFrequency0):
                NCMD_SI5351_Fclk0.SetValue(ViewModel.ClockFrequency0, false);
                break;
            case nameof(MainViewModel.ClockFrequency1):
                NCMD_SI5351_Fclk1.SetValue(ViewModel.ClockFrequency1, false);
                break;
            case nameof(MainViewModel.ClockFrequency2):
                NCMD_SI5351_Fclk2.SetValue(ViewModel.ClockFrequency2, false);
                break;
        }
    }

    private void PushAllValues()
    {
        FrequencyDial.Frequency = ViewModel.Frequency;
        AmplitudeDial.Amplitude = ViewModel.Amplitude;
        NCMD_Fmod.SetValue(ViewModel.ModulationFrequency, false);
        NCMD_AMdepth.SetValue(ViewModel.AmplitudeModulationDepth, false);
        NCMD_FMdev.SetValue(ViewModel.FrequencyDeviation, false);
        NCMD_Fstart.SetValue(ViewModel.SweepStartFrequency, false);
        NCMD_Fstop.SetValue(ViewModel.SweepStopFrequency, false);
        NCMD_Tsweep.SetValue(ViewModel.SweepTime, false);
        NCMD_Tmeasure.SetValue(ViewModel.MeasurementInterval, false);
        NCMD_Nmeasure.SetValue(ViewModel.MeasurementCount, false);
        NCMD_SI5351_Fclk0.SetValue(ViewModel.ClockFrequency0, false);
        NCMD_SI5351_Fclk1.SetValue(ViewModel.ClockFrequency1, false);
        NCMD_SI5351_Fclk2.SetValue(ViewModel.ClockFrequency2, false);
    }

    private void PresetList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs eventArgs)
    {
        if (PresetList.SelectedItem is { } item)
        {
            PresetList.ScrollIntoView(item);
        }
    }

    /// <summary>Keeps the newest trace line in view.</summary>
    private void OnTraceChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs? eventArgs)
    {
        if (ViewModel.TraceLines.Count > 0)
        {
            // after the list has taken the change in: scrolling inside the
            // collection-changed event itself throws
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel.TraceLines.Count > 0)
                {
                    TraceList.ScrollIntoView(ViewModel.TraceLines[^1]);
                }
            });
        }
    }

    private void OpenManual()
    {
        try
        {
            string path = ManualFile.Extract();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception)
        {
            MessageBox.Show(this, $"The manual could not be opened: {exception.Message}", "RF-Lab",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    (string Name, string Description)? IRfLabDialogs.AskPresetName(string title, string name, string description)
    {
        var dialog = new PresetNameDialog(title, name, description) { Owner = this };
        return dialog.ShowDialog() == true ? (dialog.PresetName, dialog.PresetDescription) : null;
    }

    bool IRfLabDialogs.Confirm(string message) =>
        MessageBox.Show(this, message, "RF-Lab", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    void IRfLabDialogs.OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
}
