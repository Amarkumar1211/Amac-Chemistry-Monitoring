using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FurnaceMonitor.Application.Services.Calculation;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services.Calculation;

public class DatabaseMasterDatabaseService : IMasterDatabaseService
{
    private readonly AppDbContext _dbContext;

    public DatabaseMasterDatabaseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private string CleanPartNo(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return new string(input.Replace('o', '0').Replace('O', '0').Where(c => char.IsDigit(c) || c == ',').ToArray());
    }

    private string NormalizePartStr(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return new string(input.Replace('o', '0').Replace('O', '0').ToLower().Where(char.IsLetterOrDigit).ToArray());
    }

    private bool CheckPartNoMatch(string inputNo, List<string> dbPartNos)
    {
        if (!dbPartNos.Any()) return true;

        var cleanInput = CleanPartNo(inputNo);
        var normInput = NormalizePartStr(inputNo);
        var trailingDigitsInput = Regex.Match(normInput, @"\d+$").Value;

        foreach (var dbNo in dbPartNos)
        {
            var cleanDb = CleanPartNo(dbNo);
            var normDb = NormalizePartStr(dbNo);

            // 1. Direct contains or equality on normalized string (handles F00N212148 vs FOON148, etc.)
            if (!string.IsNullOrEmpty(normInput) && !string.IsNullOrEmpty(normDb))
            {
                if (normDb == normInput || normDb.Contains(normInput) || normInput.Contains(normDb)) return true;
                if (normDb.EndsWith(normInput) || normInput.EndsWith(normDb)) return true;
            }

            // 2. Direct or suffix digit matching (handles 148 matching 212148, 3416 matching 28373416)
            if (!string.IsNullOrEmpty(cleanInput) && !string.IsNullOrEmpty(cleanDb))
            {
                if (cleanDb == cleanInput) return true;
                if (cleanInput.Length >= 3 && cleanDb.EndsWith(cleanInput)) return true;
                if (cleanDb.Length >= 3 && cleanInput.EndsWith(cleanDb)) return true;
                if (cleanInput.Length >= 4 && cleanDb.Contains(cleanInput)) return true;
                if (cleanDb.Length >= 4 && cleanInput.Contains(cleanDb)) return true;
            }

            // 3. Trailing digits match (e.g. FOON148 -> 148, F00N212148 -> 212148 ending with 148)
            var trailingDigitsDb = Regex.Match(normDb, @"\d+$").Value;
            if (!string.IsNullOrEmpty(trailingDigitsInput) && trailingDigitsInput.Length >= 3)
            {
                if (trailingDigitsDb.EndsWith(trailingDigitsInput) || trailingDigitsInput.EndsWith(trailingDigitsDb)) return true;
            }
        }

        return false;
    }

    public (MasterSpecResult? Spec, List<string> Mismatches) GetTargetSpecs(string partName, string partNo, string gradeName)
    {
        var inputPartName = partName?.Trim().ToLower() ?? "";
        var inputGradeName = gradeName?.Trim().ToLower() ?? "";
        var inputPartNo = partNo?.Trim().ToLower() ?? "";

        var dbSpecsList = _dbContext.PartSpecifications.ToList();
        
        // 1. Try EXACT MATCH first (including comma-separated Part Numbers in DB)
        var exactMatch = dbSpecsList.FirstOrDefault(x => 
            (x.PartName?.Trim().ToLower() ?? "") == inputPartName && 
            (x.GradeName?.Trim().ToLower() ?? "") == inputGradeName &&
            (CleanPartNo(x.PartNo) == CleanPartNo(inputPartNo) ||
             (x.PartNo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Any(p => CleanPartNo(p) == CleanPartNo(inputPartNo))));

        if (exactMatch != null)
        {
            return (MapToResult(exactMatch), new List<string>());
        }

        // 2. Try FUZZY MATCH
        var fuzzyMatches = dbSpecsList.Select(x => {
            var dbPartName = x.PartName?.Trim().ToLower() ?? "";
            var dbGradeName = x.GradeName?.Trim().ToLower() ?? "";
            var dbPartNos = (x.PartNo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(p => p.Trim().ToLower())
                                            .Where(p => p.Length > 0)
                                            .ToList();

            // Grade Match: Does input grade contain DB grade? (e.g. "alsi7mg (cbxx)" contains "alsi7mg")
            bool gradeMatch = !string.IsNullOrEmpty(dbGradeName) && 
                              (inputGradeName.Contains(dbGradeName) || dbGradeName.Contains(inputGradeName));

            // Part Name Match: 
            // If DB Part Name is a comma-separated list, check if ANY part of it matches.
            var dbPartNamesList = dbPartName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(p => p.Trim())
                                            .Where(p => p.Length > 2)
                                            .ToList();
            
            bool nameMatch = false;
            if (!string.IsNullOrEmpty(dbPartName))
            {
                nameMatch = inputPartName.Contains(dbPartName) || dbPartName.Contains(inputPartName) || inputGradeName.Contains(dbPartName);
                if (!nameMatch && dbPartNamesList.Any())
                {
                    nameMatch = dbPartNamesList.Any(name => inputPartName.Contains(name) || inputGradeName.Contains(name));
                }
            }

            // Part No Match: Direct or suffix or normalized comparison
            bool noMatch = CheckPartNoMatch(inputPartNo, dbPartNos);

            int score = (nameMatch ? 1 : 0) + (noMatch ? 1 : 0) + (gradeMatch ? 1 : 0);
            
            var mismatches = new List<string>();
            if (!nameMatch) mismatches.Add("Part Name");
            if (!noMatch) mismatches.Add("Part Number");
            if (!gradeMatch) mismatches.Add("Grade Name");
            
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                var logPath = Path.Combine(logDir, "fuzzy_log.txt");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(logPath,
                    $"\n[Spec: {dbGradeName} / {dbPartName} / {string.Join(",", dbPartNos)}] " +
                    $"gradeMatch={gradeMatch}, nameMatch={nameMatch}, noMatch={noMatch}\n" +
                    $"inputGrade={inputGradeName}, inputName={inputPartName}, inputNo={inputPartNo}\n");
            }
            catch { /* Debug log write failure must never crash the calculation engine */ }

            return new { Spec = x, Score = score, Mismatches = mismatches };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        var bestMatch = fuzzyMatches.FirstOrDefault();

        if (bestMatch != null && bestMatch.Score == 3)
        {
            return (MapToResult(bestMatch.Spec), new List<string>());
        }

        if (bestMatch != null && bestMatch.Score > 0)
        {
            return (null, bestMatch.Mismatches);
        }

        return (null, new List<string> { "Part Name", "Part Number", "Grade Name" });
    }

    private MasterSpecResult MapToResult(FurnaceMonitor.Domain.Entities.PartSpecification exactMatch)
    {
        var result = new MasterSpecResult
        {
            PartName = exactMatch.PartName,
            PartNo = exactMatch.PartNo,
            GradeName = exactMatch.GradeName,
            ColorCode = exactMatch.ColorCode
        };

        if (!string.IsNullOrWhiteSpace(exactMatch.MinTargetsJson))
        {
            var minTargets = JsonSerializer.Deserialize<Dictionary<string, double>>(exactMatch.MinTargetsJson) 
                                ?? new Dictionary<string, double>();
            foreach (var kvp in minTargets) result.MinTargets[kvp.Key] = kvp.Value;
        }

        if (!string.IsNullOrWhiteSpace(exactMatch.MaxTargetsJson))
        {
            var maxTargets = JsonSerializer.Deserialize<Dictionary<string, double>>(exactMatch.MaxTargetsJson) 
                                ?? new Dictionary<string, double>();
            foreach (var kvp in maxTargets) result.MaxTargets[kvp.Key] = kvp.Value;
        }

        if (!string.IsNullOrWhiteSpace(exactMatch.CustomerMaxTargetsJson))
        {
            var custMaxTargets = JsonSerializer.Deserialize<Dictionary<string, double>>(exactMatch.CustomerMaxTargetsJson) 
                                    ?? new Dictionary<string, double>();
            foreach (var kvp in custMaxTargets) result.CustomerMaxTargets[kvp.Key] = kvp.Value;
        }

        if (!string.IsNullOrWhiteSpace(exactMatch.CustomerMinTargetsJson))
        {
            var custMinTargets = JsonSerializer.Deserialize<Dictionary<string, double>>(exactMatch.CustomerMinTargetsJson) 
                                    ?? new Dictionary<string, double>();
            foreach (var kvp in custMinTargets) result.CustomerMinTargets[kvp.Key] = kvp.Value;
        }

        return result;
    }
}
