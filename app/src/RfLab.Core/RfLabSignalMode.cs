namespace RfLab.Core;

/// <summary>
/// The application's operating modes, numbered as the original RF-Lab
/// application's mode selector and its stored settings number them.
/// </summary>
public enum RfLabSignalMode
{
    /// <summary>An unmodulated carrier ("OFF" means modulation off).</summary>
    Off = 0,

    AmplitudeModulation = 1,

    FrequencyModulation = 2,

    /// <summary>The AD9910's own frequency sweep, running on the board.</summary>
    Sweep = 3,

    /// <summary>
    /// Steps the carrier across the sweep span and reads the detector at
    /// every step: a scalar response over frequency.
    /// </summary>
    Analyze = 4,

    /// <summary>Reads the detector repeatedly; the generator keeps its carrier.</summary>
    Measure = 5
}
