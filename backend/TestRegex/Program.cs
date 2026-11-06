using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class Program
{
    public static void Main()
    {
        string rawPdfText = "ASIAAN METALS Sample Result Name KJS-49 MT/AlSi8Cu3 (CPN6)/CPN6/FOON148 Unknown 16-10-2026 Sample ID Grade Part Name Part No KJS-49 MT AlSi8Cu3 (CPN6) CPN6 FOON148 Elements Conc.";
        
        string BatchNumber = null;
        string GradeName = null;
        string PartName = null;
        string PartNo = null;

        var srMatch = Regex.Match(rawPdfText, @"Sample Result Name(?:.*?Type Corr Sample Name|\s+Type[^\n\r]*?)?\s*([A-Za-z0-9\-_ ]+?)\s*/\s*([A-Za-z0-9_\(\)\. ]+?)\s*/\s*([^/]+?)\s*/\s*([^/\r\n]+?)(?:\s*Unknown|\s*\d{2}-\d{2}-\d{4}|\s+Origin|\s+Type|$)");
        if (srMatch.Success)
        {
            BatchNumber = srMatch.Groups[1].Value.Trim();
            GradeName = srMatch.Groups[2].Value.Trim();
            PartName = srMatch.Groups[3].Value.Trim();
            PartNo = srMatch.Groups[4].Value.Trim();
        }

        Console.WriteLine($"EXTRACTED by Regex:");
        Console.WriteLine($"BatchNumber: '{BatchNumber}'");
        Console.WriteLine($"GradeName: '{GradeName}'");
        Console.WriteLine($"PartName: '{PartName}'");
        Console.WriteLine($"PartNo: '{PartNo}'");

        // Furnace number from 3rd char
        int furnaceNumber = 1;
        if (BatchNumber.Length >= 3)
        {
            char third = char.ToUpperInvariant(BatchNumber[2]);
            if (third >= 'A' && third <= 'Z') furnaceNumber = (third - 'A') + 1;
        }
        Console.WriteLine($"Furnace Number: {furnaceNumber}");

        // Fuzzy match against DB Spec 1144: CPN6 / F00N212055, F00N212148, F00N211700 / AlSi8Cu3 (CPN6)
        string inputPartName = PartName?.Trim().ToLower() ?? "";
        string inputGradeName = GradeName?.Trim().ToLower() ?? "";
        string inputPartNo = PartNo?.Trim().ToLower() ?? "";

        string dbPartName = "CPN6".ToLower();
        string dbGradeName = "AlSi8Cu3 (CPN6)".ToLower();
        string dbPartNoStr = "F00N212055, F00N212148, F00N211700";
        var dbPartNos = dbPartNoStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(p => p.Trim().ToLower())
                                   .Where(p => p.Length > 0)
                                   .ToList();

        bool gradeMatch = !string.IsNullOrEmpty(dbGradeName) && 
                          (inputGradeName.Contains(dbGradeName) || dbGradeName.Contains(inputGradeName));

        bool nameMatch = !string.IsNullOrEmpty(dbPartName) && 
                         (inputPartName.Contains(dbPartName) || dbPartName.Contains(inputPartName) || inputGradeName.Contains(dbPartName));

        string CleanPartNo(string? input) => string.IsNullOrWhiteSpace(input) ? "" : new string(input.Replace('o', '0').Replace('O', '0').Where(c => char.IsDigit(c) || c == ',').ToArray());
        string NormalizePartStr(string? input) => string.IsNullOrWhiteSpace(input) ? "" : new string(input.Replace('o', '0').Replace('O', '0').ToLower().Where(char.IsLetterOrDigit).ToArray());

        bool CheckPartNoMatch(string inputNo, List<string> dbList)
        {
            var cleanInput = CleanPartNo(inputNo);
            var normInput = NormalizePartStr(inputNo);

            var trailingDigitsInput = Regex.Match(normInput, @"\d+$").Value;

            foreach (var dbNo in dbList)
            {
                var cleanDb = CleanPartNo(dbNo);
                var normDb = NormalizePartStr(dbNo);
                if (!string.IsNullOrEmpty(normInput) && !string.IsNullOrEmpty(normDb))
                {
                    if (normDb == normInput || normDb.Contains(normInput) || normInput.Contains(normDb)) return true;
                    if (normDb.EndsWith(normInput) || normInput.EndsWith(normDb)) return true;
                }
                if (!string.IsNullOrEmpty(cleanInput) && !string.IsNullOrEmpty(cleanDb))
                {
                    if (cleanDb == cleanInput) return true;
                    if (cleanInput.Length >= 3 && cleanDb.EndsWith(cleanInput)) return true;
                    if (cleanDb.Length >= 3 && cleanInput.EndsWith(cleanDb)) return true;
                    if (cleanInput.Length >= 4 && cleanDb.Contains(cleanInput)) return true;
                    if (cleanDb.Length >= 4 && cleanInput.Contains(cleanDb)) return true;
                }
                var trailingDigitsDb = Regex.Match(normDb, @"\d+$").Value;
                if (!string.IsNullOrEmpty(trailingDigitsInput) && trailingDigitsInput.Length >= 3)
                {
                    if (trailingDigitsDb.EndsWith(trailingDigitsInput) || trailingDigitsInput.EndsWith(trailingDigitsDb)) return true;
                }
            }
            return false;
        }

        bool noMatch = CheckPartNoMatch(inputPartNo, dbPartNos);

        Console.WriteLine($"\nMATCH RESULTS:");
        Console.WriteLine($"gradeMatch: {gradeMatch}");
        Console.WriteLine($"nameMatch: {nameMatch}");
        Console.WriteLine($"noMatch: {noMatch}");
        
        int score = (nameMatch ? 1 : 0) + (noMatch ? 1 : 0) + (gradeMatch ? 1 : 0);
        Console.WriteLine($"Score: {score}/3");
    }
}
