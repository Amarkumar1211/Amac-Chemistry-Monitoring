using System.Collections.Generic;

namespace FurnaceMonitor.Application.Services.PdfExtraction.Dtos;

public class ParsedSpectroResultDto
{
    public string? BatchNumber { get; set; }
    public int? FurnaceNumber { get; set; }
    public string? ColorCode { get; set; }
    public string? GradeName { get; set; }
    public string? PartName { get; set; }
    public string? PartNo { get; set; }
    public double FurnaceWeight { get; set; } = 450;
    public Dictionary<string, double>? CalculatedAdditions { get; set; } = new();
    public Dictionary<string, double>? ActualValues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> HiddenChemicals { get; set; } = new();
    public List<CalculationStepDto> CalculationDetails { get; set; } = new();
}

public class CalculationStepDto
{
    public string Chemical { get; set; }
    public double ActualValue { get; set; }
    public double MinSpec { get; set; }
    public double MaxSpec { get; set; }
    public double TargetValue { get; set; }
    public double Factor { get; set; }
    public double FurnaceWeight { get; set; }
    public double AdditionGrams { get; set; }
    public string Formula { get; set; }
}
