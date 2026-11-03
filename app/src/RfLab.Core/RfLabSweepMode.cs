namespace RfLab.Core;

/// <summary>
/// AD9910 digital-ramp sweep modes as encoded by the RF-Lab firmware.
/// </summary>
public enum RfLabSweepMode : byte
{
    /// <summary>Symmetric up-down triangle sweep.</summary>
    Bidirectional = 0,

    /// <summary>Repeating sawtooth sweep with instant retrace.</summary>
    Ramp = 1,

    /// <summary>One single ramp that holds at the stop frequency.</summary>
    SingleRamp = 2
}
