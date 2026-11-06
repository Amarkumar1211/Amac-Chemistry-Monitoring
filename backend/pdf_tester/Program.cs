using System;
using System.IO;
using FurnaceMonitor.Infrastructure.Persistence;
using FurnaceMonitor.Infrastructure.Services.Calculation;
using FurnaceMonitor.Infrastructure.Services.PdfExtraction;
using FurnaceMonitor.Application.Services.Calculation;
using Microsoft.EntityFrameworkCore;

class TestFurnaceExtraction
{
    static void Main()
    {
        var dbPath = @"C:\ProgramData\AMAC_FurnaceMonitor\Data\furnaces.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        using var context = new AppDbContext(options);

        var masterDbService = new DatabaseMasterDatabaseService(context);
        var calcService = new ChemicalCalculationService(masterDbService);
        var pdfService = new PdfExtractionService(calcService);

        string[] testFiles = new[]
        {
            @"C:\Users\AMAC-U1\Downloads\KKF-02 MT.pdf",
            @"C:\Users\AMAC-U1\Downloads\KKF-03 MT.pdf",
            @"C:\Users\AMAC-U1\Downloads\KKF-02 FIRST.pdf"
        };

        foreach (var file in testFiles)
        {
            Console.WriteLine($"\n==========================================");
            Console.WriteLine($"Testing: {Path.GetFileName(file)}");
            if (!File.Exists(file))
            {
                Console.WriteLine($"File does not exist: {file}");
                continue;
            }
            using var stream = File.OpenRead(file);
            var result = pdfService.ParseSpectroResult(stream);

            Console.WriteLine($"Batch Number  : '{result.BatchNumber}'");
            Console.WriteLine($"Furnace Number: {result.FurnaceNumber} (Expected: 6 for letter 'F')");
            Console.WriteLine($"Grade Name    : '{result.GradeName}'");
            Console.WriteLine($"Part Name     : '{result.PartName}'");
            Console.WriteLine($"Part No       : '{result.PartNo}'");
            Console.WriteLine($"Furnace Weight: {result.FurnaceWeight} KG");
            Console.WriteLine($"Additions     : {result.CalculatedAdditions?.Count ?? 0} items");
            foreach (var kvp in result.CalculatedAdditions ?? new())
            {
                Console.WriteLine($"  - {kvp.Key}: {kvp.Value} g");
            }
        }
    }
}
