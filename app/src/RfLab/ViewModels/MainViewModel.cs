using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Input;
using RfLab.Controls;
using RfLab.Core;
using RfLab.Core.Mcnf;
using RfLab.Core.Presets;
using RfLab.Settings;
using RfLab.Themes;

namespace RfLab.ViewModels;

/// <summary>
/// One line of the byte trace. A class, not a record: two equal lines are
/// still two entries, and the list finds each by reference.
/// </summary>
public sealed class TraceLine(string text, bool isSent)
{
    public string Text { get; } = text;

    public bool IsSent { get; } = isSent;
}

/// <summary>
/// Drives the RF-Lab window: the connection, the signal settings, the
/// measurements, the clock outputs, the stored settings and the byte trace.
/// </summary>
/// <remarks>
/// The board keeps no readable signal state, so this view model holds the
/// settings and sends the whole setting whenever one value changes: the
/// carrier and its modulation in one request, each clock output in its own.
/// Values can be edited without a board; they are sent once one is connected.
/// The bound member names are those of the original application's view
/// model, so the original window layout binds unchanged.
/// </remarks>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>The points the chart holds before it starts over, as in the original.</summary>
    private const int MaximumMeasurementPoints = 500;

    /// <summary>The shortest analysis run; the original raised a shorter sweep time to this.</summary>
    private const int MinimumAnalyzeSweepTimeMs = 3_200;

    private const int MinimumAnalyzePoints = 10;

    private const int MaximumTraceLines = 2_000;

    private static readonly IReadOnlyDictionary<string, RfLabSweepMode> SweepModeNames =
        new Dictionary<string, RfLabSweepMode>(StringComparer.Ordinal)
        {
            ["Bidirectional"] = RfLabSweepMode.Bidirectional,
            ["Ramp"] = RfLabSweepMode.Ramp,
            ["SingleRamp"] = RfLabSweepMode.SingleRamp,
        };

    private readonly IRfLabPanelScheduler scheduler;
    private readonly RfLabPresetStore presetStore;
    private readonly Preferences preferences;
    private readonly SynchronizationContext? uiContext;
    private readonly ObservableCollection<RfLabMeasurementPoint> measurementData = [];
    private readonly bool[] clockApplyInFlight = new bool[3];
    private readonly bool[] clockApplyPending = new bool[3];
    private readonly bool[] pendingPresetClocks = new bool[3];

    private RfLabClient? client;
    private string? selectedPort;
    private bool isConnecting;
    private string connectionText = "Not connected";
    private RfLabSignalMode mode = RfLabSignalMode.Off;
    private RfLabSensorOption selectedSensor;
    private string selectedSweepMode = "Bidirectional";
    private bool isSweepActive;
    private bool isMeasurementActive;
    private bool errorStatus;
    private bool led;
    private string statusInfo = "Not connected. Choose the board's serial port and click Connect.";
    private string sensorValueString = "--";
    private double sensorValueNormalized;
    private string nodeType = string.Empty;
    private string bufferInfo = string.Empty;
    private string clockGeneratorState = string.Empty;
    private bool isClockGeneratorPresent;
    private int measurementIndex;
    private IDisposable? measurementLoop;
    private CancellationTokenSource? analyzeCancellation;
    private bool applyInFlight;
    private bool applyPending;
    private bool disposed;
    private int frequency = 10_000_000;
    private int amplitude = 20;
    private int modulationFrequency = 1_000;
    private int amplitudeModulationDepth = 80;
    private int frequencyDeviation = 10_000;
    private int sweepStartFrequency = 10_000_000;
    private int sweepStopFrequency = 30_000_000;
    private int sweepTime = 2_000;
    private int measurementInterval = 1_000;
    private int measurementCount = 200;
    private int clockFrequency0 = 1_000_000;
    private int clockFrequency1 = 2_000_000;
    private int clockFrequency2 = 3_000_000;
    private string? selectedSettingsFile;
    private string presetDescription = string.Empty;
    private bool loadingPreset;
    private bool isTraceEnabled = true;
    private string selectedTheme;

    public MainViewModel(
        RfLabPresetStore presetStore,
        Preferences preferences,
        IRfLabPanelScheduler? scheduler = null)
    {
        this.presetStore = presetStore ?? throw new ArgumentNullException(nameof(presetStore));
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.scheduler = scheduler ?? new RfLabPanelScheduler();
        uiContext = SynchronizationContext.Current;

        Sensors = [RfLabSensorOption.Ad8307, RfLabSensorOption.Converter];
        selectedSensor = Sensors[0];
        SweepModes = [.. SweepModeNames.Keys];
        selectedTheme = ThemeResources.Normalize(preferences.Theme);

        RefreshPorts();
        selectedPort = preferences.Port is { } port && Ports.Contains(port, StringComparer.OrdinalIgnoreCase)
            ? port
            : Ports.LastOrDefault();
        RefreshPresets();

        ConnectCommand = new RelayCommand(
            () => _ = IsConnected ? DisconnectAsync() : ConnectAsync(),
            () => !isConnecting && (IsConnected || !string.IsNullOrEmpty(SelectedPort)));
        RefreshPortsCommand = new RelayCommand(RefreshPorts, () => !IsConnected && !isConnecting);
        ClearMeasurementCommand = new RelayCommand(ClearMeasurement);
        ToggleIndicatorCommand = new RelayCommand(() => _ = ToggleIndicatorAsync(), () => IsConnected);
        ApplyPresetCommand = new RelayCommand(ApplySelectedPreset, () => SelectedSettingsFile is not null);
        SavePresetCommand = new RelayCommand(SavePreset, () => SelectedSettingsFile is not null);
        SavePresetAsCommand = new RelayCommand(SavePresetAs);
        RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedSettingsFile is not null);
        DeletePresetCommand = new RelayCommand(DeletePreset, () => SelectedSettingsFile is not null);
        OpenPresetFolderCommand = new RelayCommand(OpenPresetFolder);
        ClearTraceCommand = new RelayCommand(TraceLines.Clear);
    }

    /// <summary>Asks the operator; set by the window. Without it, questions are answered "no".</summary>
    public IRfLabDialogs? Dialogs { get; set; }

    /// <summary>Raised when the operator picks another theme; the application rebuilds the window.</summary>
    public event EventHandler<string>? ThemeChangeRequested;

    public string Title => "RF-Lab";

    public IReadOnlyList<RfLabSensorOption> Sensors { get; }

    public IReadOnlyList<string> SweepModes { get; }

    public IReadOnlyList<string> Themes => ThemeResources.Names;

    public ObservableCollection<string> Ports { get; } = [];

    public ObservableCollection<string> SettingsFiles { get; } = [];

    public ObservableCollection<TraceLine> TraceLines { get; } = [];

    public ObservableCollection<RfLabMeasurementPoint> MeasurementData => measurementData;

    public ICommand ConnectCommand { get; }

    public ICommand RefreshPortsCommand { get; }

    public ICommand ClearMeasurementCommand { get; }

    public ICommand ToggleIndicatorCommand { get; }

    public ICommand ApplyPresetCommand { get; }

    public ICommand SavePresetCommand { get; }

    public ICommand SavePresetAsCommand { get; }

    public ICommand RenamePresetCommand { get; }

    public ICommand DeletePresetCommand { get; }

    public ICommand OpenPresetFolderCommand { get; }

    public ICommand ClearTraceCommand { get; }

    public string PresetDirectory => presetStore.DirectoryPath;

    // ---------------------------------------------------------------
    // Connection
    // ---------------------------------------------------------------

    public string? SelectedPort
    {
        get => selectedPort;
        set => SetProperty(ref selectedPort, value);
    }

    public bool IsConnected => client is not null;

    public bool IsDisconnected => client is null && !isConnecting;

    public bool IsConnecting
    {
        get => isConnecting;
        private set
        {
            if (SetProperty(ref isConnecting, value))
            {
                RaiseConnectionChanged();
            }
        }
    }

    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

    public string ConnectionText
    {
        get => connectionText;
        private set => SetProperty(ref connectionText, value);
    }

    public string SelectedTheme
    {
        get => selectedTheme;
        set
        {
            string theme = ThemeResources.Normalize(value);
            if (SetProperty(ref selectedTheme, theme))
            {
                preferences.Theme = theme;
                ThemeChangeRequested?.Invoke(this, theme);
            }
        }
    }

    // ---------------------------------------------------------------
    // Signal settings. A dial change sends the whole setting at once, the
    // way the original's dials drove the generator; sweep and measurement
    // values take effect when a sweep or a measurement starts.
    // ---------------------------------------------------------------

    public int Frequency
    {
        get => frequency;
        set => SetLiveTarget(ref frequency, value);
    }

    /// <summary>The output attenuation in dB below full scale (the dial shows it as "- dB").</summary>
    public int Amplitude
    {
        get => amplitude;
        set => SetLiveTarget(ref amplitude, value);
    }

    public int ModulationFrequency
    {
        get => modulationFrequency;
        set => SetLiveTarget(ref modulationFrequency, value);
    }

    public int AmplitudeModulationDepth
    {
        get => amplitudeModulationDepth;
        set => SetLiveTarget(ref amplitudeModulationDepth, value);
    }

    public int FrequencyDeviation
    {
        get => frequencyDeviation;
        set => SetLiveTarget(ref frequencyDeviation, value);
    }

    public int SweepStartFrequency
    {
        get => sweepStartFrequency;
        set
        {
            if (SetProperty(ref sweepStartFrequency, value))
            {
                RaisePropertyChanged(nameof(Xmin));
            }
        }
    }

    public int SweepStopFrequency
    {
        get => sweepStopFrequency;
        set
        {
            if (SetProperty(ref sweepStopFrequency, value))
            {
                RaisePropertyChanged(nameof(Xmax));
            }
        }
    }

    public int SweepTime
    {
        get => sweepTime;
        set => SetProperty(ref sweepTime, value);
    }

    public int MeasurementInterval
    {
        get => measurementInterval;
        set
        {
            if (SetProperty(ref measurementInterval, value) && isMeasurementActive)
            {
                StartMeasurementLoop();
            }
        }
    }

    public int MeasurementCount
    {
        get => measurementCount;
        set => SetProperty(ref measurementCount, value);
    }

    public int DDS_ModMode
    {
        get => (int)mode;
        set
        {
            var requested = (RfLabSignalMode)value;
            if (mode == requested || !Enum.IsDefined(requested))
            {
                return;
            }

            mode = requested;
            CancelAnalyze();
            if (isSweepActive)
            {
                SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
                RaisePropertyChanged(nameof(IsSweepInactive));
            }

            RaisePropertyChanged(nameof(DDS_ModMode));
            RaisePropertyChanged(nameof(IsModeAM));
            RaisePropertyChanged(nameof(IsModeFM));
            RaisePropertyChanged(nameof(IsModeSWEEP));
            RaisePropertyChanged(nameof(IsModeANALYZE));
            RaisePropertyChanged(nameof(IsModeMEASURE));
            RaisePropertyChanged(nameof(IsFModEnabled));
            RaisePropertyChanged(nameof(Xlabel));
            RaisePropertyChanged(nameof(Xmin));
            RaisePropertyChanged(nameof(Xmax));

            if (requested != RfLabSignalMode.Measure)
            {
                IsMeasurementActive = false;
            }

            // SWEEP sends nothing until Start; the other modes send their carrier at once
            if (!loadingPreset && requested != RfLabSignalMode.Sweep)
            {
                _ = ApplyModeCoalescedAsync();
            }
        }
    }

    public RfLabSignalMode Mode => mode;

    public bool IsModeAM => mode == RfLabSignalMode.AmplitudeModulation;

    public bool IsModeFM => mode == RfLabSignalMode.FrequencyModulation;

    /// <summary>Whether the sweep span and time apply: in SWEEP and in ANALYZE, as in the original.</summary>
    public bool IsModeSWEEP => mode is RfLabSignalMode.Sweep or RfLabSignalMode.Analyze;

    public bool IsModeANALYZE => mode == RfLabSignalMode.Analyze;

    public bool IsModeMEASURE => mode == RfLabSignalMode.Measure;

    public bool IsFModEnabled => IsModeAM || IsModeFM;

    /// <summary>The Start/Stop key of SWEEP and ANALYZE.</summary>
    public bool IsSweepActive
    {
        get => isSweepActive;
        set
        {
            if (value && !IsConnected)
            {
                RaisePropertyChanged();
                return;
            }

            if (!SetProperty(ref isSweepActive, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsSweepInactive));

            if (!value)
            {
                if (analyzeCancellation is not null)
                {
                    CancelAnalyze();
                }
                else
                {
                    // the board sweeps until it gets another setting
                    _ = ApplyModeCoalescedAsync();
                }

                return;
            }

            _ = IsModeANALYZE ? StartAnalyzeAsync() : StartSweepAsync();
        }
    }

    public bool IsSweepInactive => !isSweepActive;

    /// <summary>The Run/Halt key of MEASURE.</summary>
    public bool IsMeasurementActive
    {
        get => isMeasurementActive;
        set
        {
            if (value && !IsConnected)
            {
                RaisePropertyChanged();
                return;
            }

            if (!SetProperty(ref isMeasurementActive, value))
            {
                return;
            }

            if (value)
            {
                StartMeasurementLoop();
            }
            else
            {
                StopMeasurementLoop();
            }
        }
    }

    public bool ErrorStatus
    {
        get => errorStatus;
        private set => SetProperty(ref errorStatus, value);
    }

    /// <summary>The board's LED, as the board last reported it.</summary>
    public bool LED
    {
        get => led;
        private set => SetProperty(ref led, value);
    }

    public string StatusInfo
    {
        get => statusInfo;
        private set => SetProperty(ref statusInfo, value);
    }

    public string SensorValueString
    {
        get => sensorValueString;
        private set => SetProperty(ref sensorValueString, value);
    }

    public double SensorValueNormalized
    {
        get => sensorValueNormalized;
        private set => SetProperty(ref sensorValueNormalized, value);
    }

    public string NodeType
    {
        get => nodeType;
        private set => SetProperty(ref nodeType, value);
    }

    public string BufferInfo
    {
        get => bufferInfo;
        private set => SetProperty(ref bufferInfo, value);
    }

    public string ClockGeneratorState
    {
        get => clockGeneratorState;
        private set => SetProperty(ref clockGeneratorState, value);
    }

    /// <summary>Whether the board reported an Si5351; gates the clock controls.</summary>
    public bool IsClockGeneratorPresent
    {
        get => isClockGeneratorPresent;
        private set => SetProperty(ref isClockGeneratorPresent, value);
    }

    public int ClockFrequency0
    {
        get => clockFrequency0;
        set => SetClockTarget(ref clockFrequency0, value, 0);
    }

    public int ClockFrequency1
    {
        get => clockFrequency1;
        set => SetClockTarget(ref clockFrequency1, value, 1);
    }

    public int ClockFrequency2
    {
        get => clockFrequency2;
        set => SetClockTarget(ref clockFrequency2, value, 2);
    }

    public RfLabSensorOption SelectedSensor
    {
        get => selectedSensor;
        set
        {
            if (value is not null && SetProperty(ref selectedSensor, value))
            {
                ClearMeasurement();
            }
        }
    }

    public string SelectedSweepMode
    {
        get => selectedSweepMode;
        set => SetProperty(ref selectedSweepMode, value);
    }

    public string Xlabel => IsModeMEASURE ? "n" : "f [Hz]";

    public double Xmin => IsModeMEASURE ? 0 : SweepStartFrequency;

    public double Xmax => IsModeMEASURE ? MaximumMeasurementPoints : SweepStopFrequency;

    // ---------------------------------------------------------------
    // Trace
    // ---------------------------------------------------------------

    public bool IsTraceEnabled
    {
        get => isTraceEnabled;
        set => SetProperty(ref isTraceEnabled, value);
    }

    // ---------------------------------------------------------------
    // Stored settings
    // ---------------------------------------------------------------

    /// <summary>
    /// The selected stored setting. As in the original, selecting one only
    /// selects it (Save, Rename and Delete work on it); Apply or a double
    /// click loads it into the window and sends it.
    /// </summary>
    public string? SelectedSettingsFile
    {
        get => selectedSettingsFile;
        set
        {
            if (SetProperty(ref selectedSettingsFile, value))
            {
                PresetDescription = value is null ? string.Empty : presetStore.Read(value)?.Description ?? string.Empty;
            }
        }
    }

    public string PresetDescription
    {
        get => presetDescription;
        private set => SetProperty(ref presetDescription, value);
    }

    /// <summary>Opens the port, waits for the board to boot, checks it and sends the window's setting.</summary>
    public async Task ConnectAsync()
    {
        string? port = SelectedPort;
        if (client is not null || isConnecting || string.IsNullOrEmpty(port))
        {
            return;
        }

        IsConnecting = true;
        ConnectionText = $"Connecting to {port} …";
        StatusInfo = $"Opening {port}; the board restarts and needs about three seconds.";
        try
        {
            RfLabClient connected = await RfLabClient.ConnectAsync(port).ConfigureAwait(true);
            connected.Trace = OnTrace;
            client = connected;
            preferences.Port = port;

            NodeType = $"Node type {connected.Identity.NodeType}";
            BufferInfo = $"Message buffer {connected.ReportedBufferSize} bytes";
            IsClockGeneratorPresent = connected.Configuration.Si5351Present;
            ClockGeneratorState = IsClockGeneratorPresent ? "Clock generator: present" : "Clock generator: absent";
            LED = connected.Configuration.LedOn;
            ConnectionText = $"RF-Lab on {port}";
            ErrorStatus = false;
            StatusInfo = $"Connected to the RF-Lab board on {port}.";
            IsConnecting = false;

            // The board restarted on its own defaults: bring it to what the window shows.
            await ApplyModeCoalescedAsync().ConfigureAwait(true);
            if (IsClockGeneratorPresent)
            {
                for (int channel = 0; channel < 3 && IsConnected; channel++)
                {
                    await ApplyClockCoalescedAsync(channel).ConfigureAwait(true);
                }
            }

            if (IsConnected)
            {
                await ReadSensorAsync().ConfigureAwait(true);
            }

            if (IsConnected && !ErrorStatus)
            {
                StatusInfo = $"Connected to the RF-Lab board on {port}; the window's setting is sent.";
            }
        }
        catch (Exception exception)
        {
            ConnectionText = "Not connected";
            Fail($"No RF-Lab board on {port}: {Describe(exception)}");
        }
        finally
        {
            IsConnecting = false;
            RaiseConnectionChanged();
        }
    }

    public async Task DisconnectAsync(string? reason = null)
    {
        RfLabClient? closing = client;
        if (closing is null)
        {
            return;
        }

        CancelAnalyze();
        StopMeasurementLoop();
        SetProperty(ref isMeasurementActive, false, nameof(IsMeasurementActive));
        if (isSweepActive)
        {
            SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
            RaisePropertyChanged(nameof(IsSweepInactive));
        }

        client = null;
        RaiseConnectionChanged();
        ConnectionText = "Not connected";
        NodeType = BufferInfo = ClockGeneratorState = string.Empty;
        IsClockGeneratorPresent = false;
        try
        {
            await closing.DisposeAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        if (reason is null)
        {
            ErrorStatus = false;
            StatusInfo = "Disconnected. The board keeps its last signal.";
        }
        else
        {
            Fail(reason);
        }
    }

    public void RefreshPorts()
    {
        string? keep = selectedPort;
        Ports.Clear();
        foreach (string port in SerialPortByteStream.PortNames())
        {
            Ports.Add(port);
        }

        SelectedPort = keep is not null && Ports.Contains(keep) ? keep : Ports.LastOrDefault();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelAnalyze();
        StopMeasurementLoop();
        RfLabClient? closing = client;
        client = null;
        closing?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
    }

    // ---------------------------------------------------------------
    // Applying settings
    // ---------------------------------------------------------------

    private void SetLiveTarget(
        ref int field,
        int value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);

        // a preset sets many values; it sends once when all are staged
        if (!loadingPreset
            && mode is RfLabSignalMode.Off or RfLabSignalMode.AmplitudeModulation or RfLabSignalMode.FrequencyModulation)
        {
            _ = ApplyModeCoalescedAsync();
        }
    }

    private void SetClockTarget(
        ref int field,
        int value,
        int channel,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);

        if (loadingPreset)
        {
            pendingPresetClocks[channel] = true;
            return;
        }

        _ = ApplyClockCoalescedAsync(channel);
    }

    /// <summary>
    /// Sends one clock output, collapsing overlapping changes so the output
    /// ends at the newest value.
    /// </summary>
    private async Task ApplyClockCoalescedAsync(int channel)
    {
        if (!IsConnected || !IsClockGeneratorPresent)
        {
            return;
        }

        if (clockApplyInFlight[channel])
        {
            clockApplyPending[channel] = true;
            return;
        }

        clockApplyInFlight[channel] = true;
        try
        {
            do
            {
                clockApplyPending[channel] = false;
                int target = ClockFrequency(channel);
                await RunAsync(
                    (board, token) => board.ApplyClockAsync(channel, (uint)target, token),
                    $"Clock output {channel} at {FormatFrequency(target)}.").ConfigureAwait(true);
            }
            while (clockApplyPending[channel] && !disposed && IsConnected);
        }
        finally
        {
            clockApplyInFlight[channel] = false;
        }
    }

    private int ClockFrequency(int channel) =>
        channel switch
        {
            0 => clockFrequency0,
            1 => clockFrequency1,
            _ => clockFrequency2,
        };

    /// <summary>
    /// Sends the current setting, collapsing the changes a moving dial makes
    /// into one request in flight and one pending, which re-reads the newest values.
    /// </summary>
    private async Task ApplyModeCoalescedAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        if (applyInFlight)
        {
            applyPending = true;
            return;
        }

        applyInFlight = true;
        try
        {
            do
            {
                applyPending = false;
                await ApplyModeAsync().ConfigureAwait(true);
            }
            while (applyPending && !disposed && IsConnected);
        }
        finally
        {
            applyInFlight = false;
        }
    }

    private async Task ApplyModeAsync()
    {
        int carrier = Frequency;
        int attenuation = Amplitude;
        switch (mode)
        {
            case RfLabSignalMode.AmplitudeModulation:
                int fmodAm = ModulationFrequency;
                int depth = AmplitudeModulationDepth;
                await RunAsync(
                    (board, token) => board.ApplyAmplitudeModulationAsync(
                        (uint)carrier, attenuation, (uint)fmodAm, depth, token),
                    $"AM: {FormatFrequency(carrier)}, -{attenuation} dB, {fmodAm} Hz, {depth} %.").ConfigureAwait(true);
                break;

            case RfLabSignalMode.FrequencyModulation:
                int fmodFm = ModulationFrequency;
                int deviation = FrequencyDeviation;
                await RunAsync(
                    (board, token) => board.ApplyFrequencyModulationAsync(
                        (uint)carrier, attenuation, (uint)fmodFm, (uint)deviation, token),
                    $"FM: {FormatFrequency(carrier)}, -{attenuation} dB, {fmodFm} Hz, ±{deviation} Hz.").ConfigureAwait(true);
                break;

            default:
                // OFF, and the carrier SWEEP, ANALYZE and MEASURE return to
                await RunAsync(
                    (board, token) => board.ApplyCarrierAsync((uint)carrier, attenuation, token),
                    $"Carrier: {FormatFrequency(carrier)}, -{attenuation} dB.").ConfigureAwait(true);
                break;
        }

        if (mode == RfLabSignalMode.Measure)
        {
            IsMeasurementActive = true;
        }
        else
        {
            IsMeasurementActive = false;
        }
    }

    private async Task StartSweepAsync()
    {
        if (!SweepModeNames.TryGetValue(SelectedSweepMode, out RfLabSweepMode sweepMode))
        {
            Fail("The selected sweep mode is not supported.");
            IsSweepActive = false;
            return;
        }

        int start = SweepStartFrequency;
        int stop = SweepStopFrequency;
        int time = SweepTime;
        int attenuation = Amplitude;
        ClearMeasurement();
        bool started = await RunAsync(
            (board, token) => board.StartSweepAsync((uint)start, (uint)stop, time, attenuation, sweepMode, token),
            $"Sweep ({SelectedSweepMode}) {FormatFrequency(start)} to {FormatFrequency(stop)} in {time} ms.").ConfigureAwait(true);
        if (!started && isSweepActive)
        {
            SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
            RaisePropertyChanged(nameof(IsSweepInactive));
        }
    }

    /// <summary>
    /// Steps the carrier across the sweep span and reads the detector at each
    /// step, plotting the response over frequency: the original's ANALYZE.
    /// </summary>
    /// <remarks>
    /// The sweep time sets the settling time per point (with the original's
    /// floor), less the round trip. When the run ends — completed or stopped —
    /// the carrier returns to the window's frequency, so the generator is
    /// never left at the last step.
    /// </remarks>
    private async Task StartAnalyzeAsync()
    {
        CancelAnalyze();
        var cancellation = new CancellationTokenSource();
        analyzeCancellation = cancellation;
        CancellationToken cancellationToken = cancellation.Token;

        int points = Math.Clamp(MeasurementCount, MinimumAnalyzePoints, MaximumMeasurementPoints);
        int start = SweepStartFrequency;
        int stop = SweepStopFrequency;
        int attenuation = Amplitude;
        if (stop <= start)
        {
            Fail("ANALYZE needs a stop frequency above the start frequency.");
            analyzeCancellation = null;
            cancellation.Dispose();
            SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
            RaisePropertyChanged(nameof(IsSweepInactive));
            return;
        }

        double settlingDelay = Math.Max(SweepTime, MinimumAnalyzeSweepTimeMs) / (double)points;
        double step = (stop - start) / (double)points;
        string? outcome = null;

        ClearMeasurement();
        IsMeasurementActive = false;

        try
        {
            for (int index = 0; index < points; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RfLabClient board = client ?? throw new OperationCanceledException();

                var pointFrequency = (int)Math.Round(start + (step * index));
                long started = Environment.TickCount64;
                await board.ApplyCarrierAsync((uint)pointFrequency, attenuation, cancellationToken).ConfigureAwait(true);

                double elapsed = Environment.TickCount64 - started;
                if (settlingDelay > elapsed)
                {
                    await scheduler.DelayAsync(
                        TimeSpan.FromMilliseconds(settlingDelay - elapsed),
                        cancellationToken).ConfigureAwait(true);
                }

                RfLabSensorReading reading = await board.ReadSensorAsync(cancellationToken).ConfigureAwait(true);
                double value = SelectedSensor.Value(reading);
                AddMeasurementPoint(pointFrequency, value);
                ShowSensorValue(value);
                StatusInfo = $"ANALYZE {index + 1}/{points} at {FormatFrequency(pointFrequency)}.";
            }

            outcome = $"ANALYZE complete: {points} points from {FormatFrequency(start)} to {FormatFrequency(stop)}.";
        }
        catch (OperationCanceledException)
        {
            if (!ErrorStatus)
            {
                outcome = "ANALYZE stopped.";
            }
        }
        catch (Exception exception)
        {
            await HandleFailureAsync("ANALYZE stopped", exception).ConfigureAwait(true);
        }
        finally
        {
            if (ReferenceEquals(analyzeCancellation, cancellation))
            {
                analyzeCancellation = null;
            }

            cancellation.Dispose();

            if (isSweepActive)
            {
                SetProperty(ref isSweepActive, false, nameof(IsSweepActive));
                RaisePropertyChanged(nameof(IsSweepInactive));
            }

            // back to the window's carrier
            await ApplyModeCoalescedAsync().ConfigureAwait(true);
            if (outcome is not null && !ErrorStatus)
            {
                StatusInfo = outcome;
            }
        }
    }

    private void CancelAnalyze()
    {
        CancellationTokenSource? cancellation = analyzeCancellation;
        analyzeCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ToggleIndicatorAsync()
    {
        bool target = !LED;
        await RunAsync(
            async (board, token) => LED = await board.SetIndicatorAsync(target, token).ConfigureAwait(true),
            target ? "LED on." : "LED off.").ConfigureAwait(true);
    }

    private void StartMeasurementLoop()
    {
        StopMeasurementLoop();
        measurementLoop = scheduler.Schedule(
            TimeSpan.FromMilliseconds(Math.Max(10, MeasurementInterval)),
            ReadSensorAsync);
    }

    private void StopMeasurementLoop()
    {
        measurementLoop?.Dispose();
        measurementLoop = null;
    }

    private async Task ReadSensorAsync()
    {
        RfLabClient? board = client;
        if (board is null)
        {
            return;
        }

        try
        {
            RfLabSensorReading reading = await board.ReadSensorAsync().ConfigureAwait(true);
            double value = SelectedSensor.Value(reading);
            ShowSensorValue(value);
            if (isMeasurementActive)
            {
                AddMeasurementPoint(value);
            }
        }
        catch (Exception exception)
        {
            await HandleFailureAsync("Detector read failed", exception).ConfigureAwait(true);
        }
    }

    private void ShowSensorValue(double value)
    {
        SensorValueString = value.ToString("0.0", CultureInfo.CurrentCulture);
        double span = SelectedSensor.Maximum - SelectedSensor.Minimum;
        SensorValueNormalized = span <= 0 ? 0 : Math.Clamp((value - SelectedSensor.Minimum) / span, 0, 1);
    }

    private void AddMeasurementPoint(double value) =>
        AddMeasurementPoint(
            IsModeMEASURE
                ? measurementIndex
                : Xmin + ((Xmax - Xmin) * (measurementIndex / (double)MaximumMeasurementPoints)),
            value);

    private void AddMeasurementPoint(double abscissa, double value)
    {
        if (measurementData.Count >= MaximumMeasurementPoints)
        {
            ClearMeasurement();
        }

        measurementData.Add(new RfLabMeasurementPoint(abscissa, value));
        measurementIndex++;
    }

    private void ClearMeasurement()
    {
        measurementData.Clear();
        measurementIndex = 0;
        RaisePropertyChanged(nameof(Xlabel));
        RaisePropertyChanged(nameof(Xmin));
        RaisePropertyChanged(nameof(Xmax));
    }

    /// <summary>Runs one board operation; reports its outcome in the status line.</summary>
    private async Task<bool> RunAsync(Func<RfLabClient, CancellationToken, Task> operation, string successMessage)
    {
        RfLabClient? board = client;
        if (board is null)
        {
            return false;
        }

        try
        {
            await operation(board, CancellationToken.None).ConfigureAwait(true);
            ErrorStatus = false;
            StatusInfo = successMessage;
            return true;
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(null, exception).ConfigureAwait(true);
            return false;
        }
    }

    /// <summary>Shows a failure; a lost connection is closed so the operator can connect again.</summary>
    private async Task HandleFailureAsync(string? context, Exception exception)
    {
        string message = context is null ? Describe(exception) : $"{context}: {Describe(exception)}";
        if (client is { IsFaulted: true })
        {
            await DisconnectAsync($"{message} The connection was closed; connect again.").ConfigureAwait(true);
            return;
        }

        Fail(message);
    }

    private static string Describe(Exception exception) =>
        exception switch
        {
            ArgumentOutOfRangeException range => range.Message.Split(" (Parameter", 2)[0],
            McnfExchangeException { InnerException: TimeoutException } => "the board did not answer.",
            UnauthorizedAccessException => "the port is in use by another program.",
            FileNotFoundException => "the port does not exist.",
            _ => exception.Message,
        };

    private void Fail(string message)
    {
        ErrorStatus = true;
        StatusInfo = message;
    }

    private void RaiseConnectionChanged()
    {
        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(IsDisconnected));
        RaisePropertyChanged(nameof(ConnectButtonText));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string FormatFrequency(int hertz) =>
        hertz >= 1_000_000
            ? $"{hertz / 1_000_000.0:0.######} MHz"
            : hertz >= 1_000 ? $"{hertz / 1_000.0:0.###} kHz" : $"{hertz} Hz";

    private void OnTrace(McnfTraceDirection direction, ReadOnlyMemory<byte> bytes)
    {
        if (!isTraceEnabled)
        {
            return;
        }

        var text = new StringBuilder(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
        if (direction == McnfTraceDirection.Text)
        {
            // what the firmware printed before its answer, as text
            text.Append("  board text: ");
            foreach (byte value in bytes.Span)
            {
                text.Append(value switch
                {
                    0x0A => " | ",
                    0x0D => string.Empty,
                    _ => ((char)value).ToString(),
                });
            }
        }
        else
        {
            text.Append(direction == McnfTraceDirection.Transmit ? "  PC → " : "  PC ← ");
            foreach (byte value in bytes.Span)
            {
                text.Append(value.ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
            }
        }

        var line = new TraceLine(text.ToString().TrimEnd(), direction == McnfTraceDirection.Transmit);
        if (uiContext is null)
        {
            AddTraceLine(line);
        }
        else
        {
            uiContext.Post(_ => AddTraceLine(line), null);
        }
    }

    private void AddTraceLine(TraceLine line)
    {
        TraceLines.Add(line);
        while (TraceLines.Count > MaximumTraceLines)
        {
            TraceLines.RemoveAt(0);
        }
    }

    // ---------------------------------------------------------------
    // Stored settings
    // ---------------------------------------------------------------

    private void RefreshPresets(string? select = null)
    {
        SettingsFiles.Clear();
        foreach (string name in presetStore.ListNames())
        {
            SettingsFiles.Add(name);
        }

        selectedSettingsFile = select is not null && SettingsFiles.Contains(select) ? select : null;
        RaisePropertyChanged(nameof(SelectedSettingsFile));
        PresetDescription = selectedSettingsFile is null
            ? string.Empty
            : presetStore.Read(selectedSettingsFile)?.Description ?? string.Empty;
    }

    /// <summary>
    /// Loads the selected stored setting into the window and sends it. Values
    /// the file does not carry stay as they are.
    /// </summary>
    /// <remarks>
    /// The mode is set last and nothing is sent while the values are staged,
    /// so the board gets the whole setting in one request rather than passing
    /// through intermediate states (a new frequency with the old attenuation).
    /// </remarks>
    private void ApplySelectedPreset()
    {
        string? name = SelectedSettingsFile;
        if (name is null)
        {
            return;
        }

        RfLabPreset? preset = presetStore.Read(name);
        if (preset is null)
        {
            Fail($"The stored setting '{name}' could not be read.");
            return;
        }

        Array.Clear(pendingPresetClocks);
        loadingPreset = true;
        try
        {
            StagePreset(preset);
        }
        finally
        {
            loadingPreset = false;
        }

        _ = ApplyModeCoalescedAsync();
        for (int channel = 0; channel < pendingPresetClocks.Length; channel++)
        {
            if (pendingPresetClocks[channel])
            {
                _ = ApplyClockCoalescedAsync(channel);
            }
        }

        ErrorStatus = false;
        StatusInfo = IsConnected
            ? $"Stored setting '{name}' loaded and sent."
            : $"Stored setting '{name}' loaded; it is sent when a board is connected.";
    }

    private void StagePreset(RfLabPreset preset)
    {
        Frequency = preset.Frequency ?? Frequency;
        Amplitude = preset.Amplitude ?? Amplitude;
        ModulationFrequency = preset.ModulationFrequency ?? ModulationFrequency;
        AmplitudeModulationDepth = preset.AmplitudeModulationDepth ?? AmplitudeModulationDepth;
        FrequencyDeviation = preset.FrequencyDeviation ?? FrequencyDeviation;
        SweepStartFrequency = preset.SweepStartFrequency ?? SweepStartFrequency;
        SweepStopFrequency = preset.SweepStopFrequency ?? SweepStopFrequency;
        SweepTime = preset.SweepTime ?? SweepTime;
        MeasurementInterval = preset.MeasurementInterval ?? MeasurementInterval;
        MeasurementCount = preset.MeasurementCount ?? MeasurementCount;

        if (preset.SweepMode is { } sweep && SweepModeNames.ContainsKey(sweep))
        {
            SelectedSweepMode = sweep;
        }

        if (preset.Sensor is { } sensorName
            && Sensors.FirstOrDefault(sensor => sensor.Name.Equals(sensorName, StringComparison.OrdinalIgnoreCase)) is { } sensor)
        {
            SelectedSensor = sensor;
        }

        ClockFrequency0 = preset.ClockFrequency0 ?? ClockFrequency0;
        ClockFrequency1 = preset.ClockFrequency1 ?? ClockFrequency1;
        ClockFrequency2 = preset.ClockFrequency2 ?? ClockFrequency2;

        if (preset.Mode is int presetMode && Enum.IsDefined((RfLabSignalMode)presetMode))
        {
            DDS_ModMode = presetMode;
        }
    }

    /// <summary>Writes the window's values into a preset, keeping the keys this application does not use.</summary>
    private void WriteValues(RfLabPreset preset)
    {
        preset.Set(RfLabPreset.ModeKey, (int)mode);
        preset.Set(RfLabPreset.FrequencyKey, Frequency);
        preset.Set(RfLabPreset.AmplitudeKey, Amplitude);
        preset.Set(RfLabPreset.ModulationFrequencyKey, ModulationFrequency);
        preset.Set(RfLabPreset.AmplitudeModulationDepthKey, AmplitudeModulationDepth);
        preset.Set(RfLabPreset.FrequencyDeviationKey, FrequencyDeviation);
        preset.Set(RfLabPreset.SweepModeKey, SelectedSweepMode);
        preset.Set(RfLabPreset.SensorKey, SelectedSensor.Name);
        preset.Set(RfLabPreset.SweepStartFrequencyKey, SweepStartFrequency);
        preset.Set(RfLabPreset.SweepStopFrequencyKey, SweepStopFrequency);
        preset.Set(RfLabPreset.SweepTimeKey, SweepTime);
        preset.Set(RfLabPreset.MeasurementIntervalKey, MeasurementInterval);
        preset.Set(RfLabPreset.MeasurementCountKey, MeasurementCount);
        preset.Set(RfLabPreset.ClockFrequency0Key, ClockFrequency0);
        preset.Set(RfLabPreset.ClockFrequency1Key, ClockFrequency1);
        preset.Set(RfLabPreset.ClockFrequency2Key, ClockFrequency2);
    }

    private void SavePreset()
    {
        string? name = SelectedSettingsFile;
        if (name is null)
        {
            return;
        }

        if (Dialogs?.Confirm($"Replace the stored setting '{name}' with the values in the window?") != true)
        {
            return;
        }

        RfLabPreset preset = presetStore.Read(name) ?? new RfLabPreset(name);
        WriteValues(preset);
        StorePreset(preset, $"Stored setting '{name}' saved.");
    }

    private void SavePresetAs()
    {
        RfLabPreset? template = SelectedSettingsFile is { } current ? presetStore.Read(current) : null;
        (string Name, string Description)? answer = Dialogs?.AskPresetName(
            "Save as a new stored setting",
            template is null ? string.Empty : template.Name + " copy",
            template?.Description ?? string.Empty);
        if (answer is not { } chosen)
        {
            return;
        }

        if (presetStore.Exists(chosen.Name)
            && Dialogs?.Confirm($"A stored setting '{chosen.Name}' exists. Replace it?") != true)
        {
            return;
        }

        // a copy keeps what the template carried for the original application's other functions
        RfLabPreset preset = template is null
            ? new RfLabPreset(chosen.Name)
            : RfLabPreset.FromLines(chosen.Name, template.ToLines());
        preset.Description = chosen.Description;
        WriteValues(preset);
        StorePreset(preset, $"Stored setting '{chosen.Name}' saved.");
    }

    private void RenamePreset()
    {
        string? name = SelectedSettingsFile;
        RfLabPreset? preset = name is null ? null : presetStore.Read(name);
        if (name is null || preset is null)
        {
            return;
        }

        (string Name, string Description)? answer = Dialogs?.AskPresetName(
            "Rename the stored setting", name, preset.Description ?? string.Empty);
        if (answer is not { } chosen)
        {
            return;
        }

        try
        {
            if (chosen.Name != name)
            {
                presetStore.Rename(name, chosen.Name);
            }

            RfLabPreset renamed = presetStore.Read(chosen.Name)!;
            renamed.Description = chosen.Description;
            presetStore.Save(renamed);
            RefreshPresets(chosen.Name);
            ErrorStatus = false;
            StatusInfo = $"Stored setting '{name}' is now '{chosen.Name}'.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Fail($"Could not rename '{name}': {exception.Message}");
        }
    }

    private void DeletePreset()
    {
        string? name = SelectedSettingsFile;
        if (name is null || Dialogs?.Confirm($"Delete the stored setting '{name}'?") != true)
        {
            return;
        }

        try
        {
            presetStore.Delete(name);
            RefreshPresets();
            ErrorStatus = false;
            StatusInfo = $"Stored setting '{name}' deleted.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Fail($"Could not delete '{name}': {exception.Message}");
        }
    }

    private void StorePreset(RfLabPreset preset, string successMessage)
    {
        try
        {
            presetStore.Save(preset);
            RefreshPresets(preset.Name);
            ErrorStatus = false;
            StatusInfo = successMessage;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Fail($"Could not save '{preset.Name}': {exception.Message}");
        }
    }

    private void OpenPresetFolder()
    {
        Directory.CreateDirectory(presetStore.DirectoryPath);
        Dialogs?.OpenFolder(presetStore.DirectoryPath);
    }
}
