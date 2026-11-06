namespace FurnaceMonitor.Domain.Enums;

/// <summary>
/// Visual health indicator for a furnace batch.
/// Green = nominal, Yellow = caution, Red = critical.
/// </summary>
public enum IndicatorColor
{
    /// <summary>Nominal operating conditions.</summary>
    Green = 1,

    /// <summary>Caution — parameters near limits.</summary>
    Yellow = 2,

    /// <summary>Critical — parameters out of tolerance.</summary>
    Red = 3
}
