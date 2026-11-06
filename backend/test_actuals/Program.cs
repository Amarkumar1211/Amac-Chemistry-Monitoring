using System;
using System.IO;
using FurnaceMonitor.Infrastructure.Services.PdfExtraction;
using FurnaceMonitor.Infrastructure.Services.Calculation;
using Microsoft.Extensions.Logging.Abstractions;
using FurnaceMonitor.Application.Common.Interfaces;
using FurnaceMonitor.Domain.Entities;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

class Program {
    static void Main() {
        var dummyService = new DummyChemicalCalculationService();
        var extractionService = new PdfExtractionService(dummyService);
        string samplePath = "sample.pdf";
        if (!File.Exists(samplePath)) {
            Console.WriteLine("Sample file not found: " + samplePath);
            return;
        }
        using var stream = File.OpenRead(samplePath);
        var result = extractionService.ParseSpectroResult(stream);
        
        Console.WriteLine($"Extracted {result.ActualValues?.Count ?? 0} ActualValues");
        foreach(var kvp in result.ActualValues ?? new Dictionary<string, double>()) {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
    }
}

class DummyChemicalCalculationService : IChemicalCalculationService {
    public void CalculateAndApplyAdditions(FurnaceMonitor.Application.DTOs.ParsedSpectroResultDto dto) {}
    public Task<FurnaceBatch> CalculateInitialBatchAdditionsAsync(FurnaceBatch batch, Furnace furnace, CancellationToken cancellationToken) => Task.FromResult(batch);
}
