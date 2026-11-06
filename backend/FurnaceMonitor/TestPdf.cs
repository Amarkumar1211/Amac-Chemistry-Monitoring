using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig;

class Program
{
    static void Main()
    {
        string path = @"sample.pdf";
        string fullText = "";
        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
            {
                fullText += page.Text + "\n";
            }
        }
        
        var blockMatches = Regex.Matches(fullText, @"(?:Meas\.|Elements Conc\. Meas\.|Si\s+Fe\s+Cu)\s*((?:[A-Z][a-z]?\s+){3,})");
        foreach (Match bMatch in blockMatches)
        {
            var elementsStr = bMatch.Groups[1].Value;
            var els = elementsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine("Elements: " + string.Join(", ", els));
            var avgMatch = Regex.Match(fullText.Substring(bMatch.Index), @"(?:<x>|Average)\s+((?:[<]?\d+\.\d+\s*|[-]\s*|\+\s*)+)");
            if (avgMatch.Success)
            {
                var valuesStr = avgMatch.Groups[1].Value.Replace("+", " ");
                var vals = Regex.Matches(valuesStr, @"[<]?\d+\.\d+");
                Console.WriteLine("Values: " + string.Join(", ", vals.Cast<Match>().Select(m => m.Value)));
                
                for (int i = 0; i < els.Length && i < vals.Count; i++)
                {
                    Console.WriteLine($"{els[i]} = {vals[i].Value}");
                }
            }
        }
    }
}
