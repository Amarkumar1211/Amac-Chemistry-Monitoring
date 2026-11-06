using System;
using System.Collections.Generic;
using System.Linq;

public class Program
{
    public static void Main()
    {
        string inputPartName = "CB18-16,CB18-18,CB28-18,CB28-20,CB08-16".ToLower();
        string inputGradeName = "AlSi7Mg (CBXX)".ToLower();
        string inputPartNo = "F019 D01 157, F019 D01 167,F019 D04 338, F019 DB1 020".ToLower();

        string dbPartName = "CBXX".ToLower();
        string dbGradeName = "ALSI7MG".ToLower();
        string dbPartNoStr = "157, 167, 311, 388, 020";
        var dbPartNos = dbPartNoStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(p => p.Trim().ToLower())
                                   .Where(p => p.Length > 0)
                                   .ToList();

        bool gradeMatch = !string.IsNullOrEmpty(dbGradeName) && 
                          (inputGradeName.Contains(dbGradeName) || dbGradeName.Contains(inputGradeName));

        bool nameMatch = !string.IsNullOrEmpty(dbPartName) && 
                         (inputPartName.Contains(dbPartName) || dbPartName.Contains(inputPartName) || inputGradeName.Contains(dbPartName));

        bool noMatch = dbPartNos.Any(dbNo => inputPartNo.Contains(dbNo));
        if (!dbPartNos.Any()) noMatch = true;

        Console.WriteLine($"gradeMatch: {gradeMatch}");
        Console.WriteLine($"nameMatch: {nameMatch}");
        Console.WriteLine($"noMatch: {noMatch}");
    }
}
