namespace FurnaceMonitor.Domain.Enums;

/// <summary>
/// Lifecycle status of a furnace batch.
/// </summary>
public enum BatchStatus
{
    /// <summary>Batch is currently running in the furnace.</summary>
    Running = 1,

    /// <summary>Batch was closed normally by an operator.</summary>
    Closed = 2,

    /// <summary>Batch was cleared without completion.</summary>
    Cleared = 3
}
