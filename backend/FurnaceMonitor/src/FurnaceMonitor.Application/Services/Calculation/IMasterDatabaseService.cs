using System.Collections.Generic;

namespace FurnaceMonitor.Application.Services.Calculation;

public interface IMasterDatabaseService
{
    (MasterSpecResult? Spec, List<string> Mismatches) GetTargetSpecs(string partName, string partNo, string gradeName);
}

public class MasterSpecResult
{
    public string? PartName { get; set; }
    public string? PartNo { get; set; }
    public string? GradeName { get; set; }
    public string? ColorCode { get; set; }
    public Dictionary<string, double> MaxTargets { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> MinTargets { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> CustomerMaxTargets { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> CustomerMinTargets { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
}
