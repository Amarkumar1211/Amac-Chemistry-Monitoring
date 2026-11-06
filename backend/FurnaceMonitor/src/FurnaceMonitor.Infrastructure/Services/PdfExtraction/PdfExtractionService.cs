using FurnaceMonitor.Application.Services.PdfExtraction;
using FurnaceMonitor.Application.Services.PdfExtraction.Dtos;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

using FurnaceMonitor.Application.Services.Calculation;

namespace FurnaceMonitor.Infrastructure.Services.PdfExtraction;

public class PdfExtractionService : IPdfExtractionService
{
    private readonly IChemicalCalculationService _chemicalCalculationService;

    public PdfExtractionService(IChemicalCalculationService chemicalCalculationService)
    {
        _chemicalCalculationService = chemicalCalculationService;
    }

    public ParsedSpectroResultDto ParseSpectroResult(Stream pdfStream)
    {
        var result = new ParsedSpectroResultDto();

        using (var pdfReader = new PdfReader(pdfStream))
        using (var document = new PdfDocument(pdfReader))
        {
            if (document.GetNumberOfPages() == 0) return result;

            var textBuilder = new System.Text.StringBuilder();
            for (int i = 1; i <= document.GetNumberOfPages(); i++)
            {
                var page = document.GetPage(i);
                var strategy = new LocationTextExtractionStrategy();
                textBuilder.AppendLine(PdfTextExtractor.GetTextFromPage(page, strategy));
            }
            var text = textBuilder.ToString();
            
            // iText7 extracts text with newlines. We flatten it to a single line with spaces for the regexes.
            var fullText = text.Replace('\n', ' ').Replace('\r', ' ');
            // reduce multiple spaces to single spaces
            fullText = Regex.Replace(fullText, @"\s+", " ");
            
            Console.WriteLine("RAW PDF TEXT: " + fullText);

            // ---------------------------------------------------------------------------
            // STRATEGY 1 (HIGH PRIORITY): Table row pattern
            // Looks for "Sample ID Grade Part Name Part No <SampleID> <Grade> <PartName> <PartNo>"
            // This is the most reliable source — it is a structured table row in Spectro PDFs.
            // ---------------------------------------------------------------------------
            var sampleIdRegex = new Regex(@"Sample ID\s*Grade\s*Part N(?:am|ma)e\s*Part No\s*(.*?)\s+(AlSi[^\s]+(?:\s*\([^)]+\))?|\bAC2A\b|\bA356\b|\bAlSi\w+\b)\s+(.*?)\s+([A-Za-z0-9\-_,\s]+?)(?=\s+(?:Elements|Conc\.|Meas\.|Si|Fe|\bA\.\b)|$)", RegexOptions.IgnoreCase);
            var tableMatch = sampleIdRegex.Match(fullText);
            if (tableMatch.Success)
            {
                result.BatchNumber = tableMatch.Groups[1].Value.Trim();
                result.GradeName   = tableMatch.Groups[2].Value.Trim();
                result.PartName    = tableMatch.Groups[3].Value.Trim();
                result.PartNo      = tableMatch.Groups[4].Value.Trim();
            }

            // ---------------------------------------------------------------------------
            // STRATEGY 2 (FALLBACK): Sample Result Name slash-format
            // e.g. "Sample Result Name ... KJS-49 MT/AlSi8Cu3 (CPN6)/CPN6/FOON148"
            // Only used when Strategy 1 did not yield results.
            // Guard: reject candidates containing metadata noise words that indicate
            // the regex matched a corrupted header (timestamps, operator names, etc.)
            // ---------------------------------------------------------------------------
            if (string.IsNullOrEmpty(result.BatchNumber) || string.IsNullOrEmpty(result.GradeName) ||
                string.IsNullOrEmpty(result.PartName)    || string.IsNullOrEmpty(result.PartNo))
            {
                var srMatch = Regex.Match(fullText, @"Sample Result Name(?:.*?Type Corr Sample Name|\s+Type[^\n\r]*?)?\s*([A-Za-z0-9\-_ ]+?)\s*/\s*([A-Za-z0-9_\(\)\. ]+?)\s*/\s*([^/]+?)\s*/\s*([^/\r\n]+?)(?:\s*Unknown|\s*\d{2}-\d{2}-\d{4}|\s+Origin|\s+Type|$)");
                if (srMatch.Success)
                {
                    // Noise filter: discard if the captured batch candidate contains metadata words
                    var batchCandidate = srMatch.Groups[1].Value.Trim();
                    var noiseWords = new[] { "Measured", "copy", "None", "Unknown", "Origin", "Method", "Date", "Time" };
                    bool isNoisy = noiseWords.Any(w => batchCandidate.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!isNoisy)
                    {
                        if (string.IsNullOrEmpty(result.BatchNumber)) result.BatchNumber = batchCandidate;
                        if (string.IsNullOrEmpty(result.GradeName))   result.GradeName   = srMatch.Groups[2].Value.Trim();
                        if (string.IsNullOrEmpty(result.PartName))    result.PartName    = srMatch.Groups[3].Value.Trim();
                        if (string.IsNullOrEmpty(result.PartNo))      result.PartNo      = srMatch.Groups[4].Value.Trim();
                    }
                }
            }

            // 3. Fallback: look for typical Grade strings and assume the word before it might be part of Sample ID
            if (string.IsNullOrEmpty(result.GradeName) || string.IsNullOrEmpty(result.BatchNumber))
            {
                var gradeMatch = Regex.Match(fullText, @"(AlSi\w+(?:\.\d+)?(?:_\w+)?|\bAC2A\b|\bA356\b)(?:\s*\([^)]+\))?");
                if (gradeMatch.Success)
                {
                    if (string.IsNullOrEmpty(result.GradeName)) result.GradeName = gradeMatch.Value.Trim();
                    
                    var idx = fullText.IndexOf(gradeMatch.Value, StringComparison.OrdinalIgnoreCase);
                    if (idx > 0 && string.IsNullOrEmpty(result.BatchNumber))
                    {
                        var beforeGrade = fullText.Substring(0, idx).Trim();
                        var parts = beforeGrade.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts.Length >= 2 && parts[parts.Length - 1].Equals("LAST", StringComparison.OrdinalIgnoreCase))
                            result.BatchNumber = $"{parts[parts.Length - 2]} {parts[parts.Length - 1]}";
                        else if (parts.Length >= 1)
                            result.BatchNumber = parts.Last();
                    }
                }
            }

            if (string.IsNullOrEmpty(result.BatchNumber) || string.IsNullOrEmpty(result.GradeName))
            {
                var ocrMatch = Regex.Match(fullText, @"Part No\s+(.+?LAST)\s+(.+?)\s+(?:CP4|Part|Elements)", RegexOptions.IgnoreCase);
                if (ocrMatch.Success)
                {
                    if (string.IsNullOrEmpty(result.BatchNumber)) result.BatchNumber = ocrMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(result.GradeName)) result.GradeName = ocrMatch.Groups[2].Value.Trim();
                }
            }

            if (string.IsNullOrEmpty(result.PartName) || string.IsNullOrEmpty(result.PartNo))
            {
                var partRegex = Regex.Match(fullText, @"Part N(?:am|ma)e\s+(.*?)\s+Part No\s+(.*?)\s+Grade", RegexOptions.IgnoreCase);
                if (partRegex.Success)
                {
                    if (string.IsNullOrEmpty(result.PartName)) result.PartName = partRegex.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(result.PartNo)) result.PartNo = partRegex.Groups[2].Value.Trim();
                }
            }

            // Extract actual chemical values
            result.ActualValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            
            // Try block extraction for <x> or Average
            var blockMatches = Regex.Matches(fullText, @"(?:Meas\.|Elements Conc\. Meas\.|Si\s+Fe\s+Cu)\s*((?:[A-Z][a-z]?\s+){3,})");
            foreach (Match bMatch in blockMatches)
            {
                var elementsStr = bMatch.Groups[1].Value;
                var els = elementsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Find next <x> or Average
                var avgMatch = Regex.Match(fullText.Substring(bMatch.Index), @"(?:<x>|Average)\s+((?:[<]?\d+\.\d+\s*|[-]\s*|\+\s*)+)");
                if (avgMatch.Success)
                {
                    var valuesStr = avgMatch.Groups[1].Value.Replace("+", " ");
                    var vals = Regex.Matches(valuesStr, @"[<]?\d+\.\d+");
                    
                    for (int i = 0; i < els.Length && i < vals.Count; i++)
                    {
                        var valStr = vals[i].Groups[0].Value.TrimStart('<');
                        if (double.TryParse(valStr, out double val))
                        {
                            result.ActualValues[els[i]] = val;
                        }
                    }
                }
            }

            // If block extraction failed, try the element-by-element fallback
            if (!result.ActualValues.Any())
            {
                var elements = new[] { "Si", "Fe", "Cu", "Mn", "Mg", "Zn", "Ti", "Sr", "Pb", "Sn", "Ni", "Cr" };
                foreach (var el in elements)
                {
                    var elMatch = Regex.Match(fullText, $@"\b{el}\s*:?\s*(\d+\.\d+)\b", RegexOptions.IgnoreCase);
                    if (elMatch.Success && double.TryParse(elMatch.Groups[1].Value, out double val))
                    {
                        result.ActualValues[el] = val;
                    }
                }
            }
            
            string rawSampleId = result.BatchNumber ?? string.Empty;
            ParseSampleId(rawSampleId, result);

            // Trigger calculation
            _chemicalCalculationService.CalculateAndApplyAdditions(result);
        }

        return result;
    }

    private void ParseSampleId(string sampleId, ParsedSpectroResultDto result)
    {
        if (string.IsNullOrWhiteSpace(sampleId)) return;

        result.BatchNumber = sampleId;

        // Set Furnace Weight based on sample text
        if (sampleId.Contains("MT", StringComparison.OrdinalIgnoreCase) || 
            sampleId.Contains("First", StringComparison.OrdinalIgnoreCase))
        {
            result.FurnaceWeight = 450;
        }
        else
        {
            result.FurnaceWeight = 250;
        }

        var fwMatch = Regex.Match(sampleId, @"(?i)(?:FW|Weight)[:\-\s]*(\d+(?:\.\d+)?)");
        if (fwMatch.Success && double.TryParse(fwMatch.Groups[1].Value, out double weight))
        {
            result.FurnaceWeight = weight;
        }

        var trimmed = sampleId.Trim();

        // -----------------------------------------------------------------
        // 3RD LETTER CONCEPT FOR FURNACE NUMBER:
        //
        // In the factory tracking standard, the 3rd character of the Sample ID
        // defines the Furnace Number:
        //   A = 1 (F1),  B = 2 (F2),  C = 3 (F3) ...
        //   J = 10 (F10) ...
        //   S = 19 (F19), T = 20 (F20) ...
        //   U = 21 (F21), V = 22 (F22), W = 23 (F23), X = 24 (F24), Y = 25 (F25)
        // -----------------------------------------------------------------
        if (trimmed.Length >= 3)
        {
            char thirdChar = char.ToUpperInvariant(trimmed[2]);
            if (thirdChar >= 'A' && thirdChar <= 'Z')
            {
                result.FurnaceNumber = (thirdChar - 'A') + 1;
            }
        }

        // Fallback for separated formats like "K-J-S-49"
        if (result.FurnaceNumber == 0)
        {
            var cleanChars = trimmed.Where(char.IsLetterOrDigit).ToArray();
            if (cleanChars.Length >= 3)
            {
                char c3 = char.ToUpperInvariant(cleanChars[2]);
                if (c3 >= 'A' && c3 <= 'Z')
                {
                    result.FurnaceNumber = (c3 - 'A') + 1;
                }
            }
        }

        // Explicit fallback ONLY when 3rd-letter concept did not determine furnace number.
        // (Prevents furnace 6's letter 'F' in batches like 'KKF-02' or 'KJF-03' from matching 'F-02'/'F-03')
        if (result.FurnaceNumber == 0)
        {
            var fnMatch = Regex.Match(trimmed, @"(?i)\b(?:FN|Furnace|F)[\s:\-]*(\d+)\b");
            if (fnMatch.Success && int.TryParse(fnMatch.Groups[1].Value, out int fnExplicit) && fnExplicit >= 1 && fnExplicit <= 25)
            {
                result.FurnaceNumber = fnExplicit;
            }
            else if (int.TryParse(trimmed, out int pureFn) && pureFn >= 1 && pureFn <= 25)
            {
                result.FurnaceNumber = pureFn;
            }
        }
    }
}
