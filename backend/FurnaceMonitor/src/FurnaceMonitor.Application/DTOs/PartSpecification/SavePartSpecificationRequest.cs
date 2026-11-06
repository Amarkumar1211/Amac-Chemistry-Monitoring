namespace FurnaceMonitor.Application.DTOs.PartSpecification;

public class SavePartSpecificationRequest
{
    public string PartName { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string? ColorCode { get; set; }
    public string? MinTargetsJson { get; set; }
    public string? MaxTargetsJson { get; set; }
    public string? CustomerMaxTargetsJson { get; set; }
    public string? CustomerMinTargetsJson { get; set; }
}
