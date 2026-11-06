using System;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string sampleId = "KHT-27 MIDDLE";
        int furnaceNumber = 1;

        var fnMatch = Regex.Match(sampleId, @""(?i)(?:FN|Furnace|F)[:\-\s]*(\d+)"");
        if (fnMatch.Success && int.TryParse(fnMatch.Groups[1].Value, out int fn))
        {
            furnaceNumber = fn;
            Console.WriteLine("Matched regex: " + fn);
        }
        else if (sampleId.Length >= 3)
        {
            char thirdLetter = char.ToUpperInvariant(sampleId[2]);
            Console.WriteLine("Third letter: " + thirdLetter);
            if (thirdLetter >= 'A' && thirdLetter <= 'Z')
            {
                furnaceNumber = (thirdLetter - 'A') + 1;
            }
        }
        Console.WriteLine("FurnaceNumber: " + furnaceNumber);
    }
}
