using FurnaceMonitor.Domain.Common;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Admin-managed specification for a specific part.
/// Overrides PDF and Excel data if available.
/// </summary>
public class PartSpecification : EntityBase
{
    public string PartName { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string? ColorCode { get; set; }

    /// <summary>
    /// JSON serialized dictionary of Chemical targets.
    /// Dictionary<string, double> mapping chemical symbols (Si, Ti, etc.) to their min targets.
    /// </summary>
    public string? MinTargetsJson { get; set; }

    /// <summary>
    /// JSON serialized dictionary of Chemical targets.
    /// Dictionary<string, double> mapping chemical symbols to their max targets.
    /// </summary>
    public string? MaxTargetsJson { get; set; }

    /// <summary>
    /// JSON serialized dictionary of Chemical targets.
    /// Dictionary<string, double> mapping chemical symbols to their customer max targets.
    /// </summary>
    public string? CustomerMaxTargetsJson { get; set; }

    /// <summary>
    /// JSON serialized dictionary of Chemical targets.
    /// Dictionary<string, double> mapping chemical symbols to their customer min targets.
    /// </summary>
    public string? CustomerMinTargetsJson { get; set; }
}
