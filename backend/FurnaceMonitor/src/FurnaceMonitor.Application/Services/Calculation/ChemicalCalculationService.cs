using System;
using System.Collections.Generic;
using System.Linq;
using FurnaceMonitor.Application.Services.PdfExtraction.Dtos;

namespace FurnaceMonitor.Application.Services.Calculation;

public interface IChemicalCalculationService
{
    void CalculateAndApplyAdditions(ParsedSpectroResultDto dto);
}

public class ChemicalCalculationService : IChemicalCalculationService
{
    private readonly IMasterDatabaseService _masterDatabaseService;

    public ChemicalCalculationService(IMasterDatabaseService masterDatabaseService)
    {
        _masterDatabaseService = masterDatabaseService;
    }

    public void CalculateAndApplyAdditions(ParsedSpectroResultDto dto)
    {
        if (dto.ActualValues == null || !dto.ActualValues.Any() || 
            string.IsNullOrEmpty(dto.PartName) || 
            string.IsNullOrEmpty(dto.PartNo) || 
            string.IsNullOrEmpty(dto.GradeName))
        {
            dto.Warnings.Add("Missing Part Name, Part No, or Grade Name. Cannot validate against Master Database.");
            return;
        }

        dto.CalculatedAdditions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        dto.Warnings ??= new List<string>();
        dto.HiddenChemicals = new List<string>();

        var (masterSpecs, mismatches) = _masterDatabaseService.GetTargetSpecs(dto.PartName, dto.PartNo, dto.GradeName);

        if (masterSpecs == null)
        {
            if (mismatches != null && mismatches.Count < 3 && mismatches.Count > 0)
            {
                dto.Warnings.Add($"Mapping Error: Specification mismatch found on {string.Join(" and ", mismatches)} in Master Database for Part '{dto.PartName}', Part No '{dto.PartNo}', Grade '{dto.GradeName}'. Halting calculation. Auto-start aborted due to Mapping Error.");
            }
            else
            {
                dto.Warnings.Add($"Mapping Error: Strict 3-point match failed (Part Name: '{dto.PartName}', Part Number: '{dto.PartNo}', Grade: '{dto.GradeName}') in Master Database. Halting calculation. Auto-start aborted due to Mapping Error.");
            }
            return;
        }

        // Apply Color Code from DB
        if (!string.IsNullOrEmpty(masterSpecs.ColorCode))
        {
            dto.ColorCode = masterSpecs.ColorCode;
        }

        // Canonicalize PartName and PartNo from matched Master DB record
        // to preserve full names when PDF multi-line column wrapping fragments them
        if (!string.IsNullOrWhiteSpace(masterSpecs.PartName))
        {
            dto.PartName = string.Join(" ", masterSpecs.PartName.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(masterSpecs.PartNo))
        {
            dto.PartNo = string.Join(" ", masterSpecs.PartNo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        }

        double maxAlAdditionGrams = 0;
        foreach (var kvp in dto.ActualValues)
        {
            string chemical = kvp.Key;
            double actualValue = kvp.Value;

            bool hasMax = masterSpecs.MaxTargets.TryGetValue(chemical, out double maxTarget);
            bool hasMin = masterSpecs.MinTargets.TryGetValue(chemical, out double minTarget);
            bool hasCustMax = masterSpecs.CustomerMaxTargets.TryGetValue(chemical, out double custMaxTarget);
            bool hasCustMin = masterSpecs.CustomerMinTargets.TryGetValue(chemical, out double custMinTarget);

            if (!hasMax && !hasMin && !hasCustMax && !hasCustMin) continue; // Not mentioned in Master Database

            if (!hasMax && hasCustMax) maxTarget = custMaxTarget;
            if (!hasMin && hasCustMin) minTarget = custMinTarget;

            // Rejection Logic:
            // Batch is ONLY rejected when Actual > Customer Max Spec (custMaxTarget).
            // If Customer Max is not given, fall back to Our Spec Max as rejection threshold.
            // If Our Spec Max < Actual <= Customer Max → treated as In Specification (no warning, no rejection).
            double rejectionTarget = hasCustMax ? custMaxTarget : maxTarget;

            if (actualValue > rejectionTarget)
            {
                dto.Warnings.Add($"{chemical}: Contact lab in Remark");
            }

            bool isMinOnly = !hasMax && hasMin;

            if (isMinOnly)
            {
                bool isTiSiCu = chemical.Equals("Ti", StringComparison.OrdinalIgnoreCase) || 
                                chemical.Equals("Si", StringComparison.OrdinalIgnoreCase) || 
                                chemical.Equals("Cu", StringComparison.OrdinalIgnoreCase);
                if (!isTiSiCu)
                {
                    dto.HiddenChemicals.Add(chemical);
                    continue; // Skip calculation for others if it's a minimum-only spec
                }
            }

            if (!hasMin)
            {
                minTarget = 0.0; // If no min spec is given, assume 0
            }

            if (actualValue < minTarget)
            {
                // BELOW Min Spec -> Do Calculation
                double factor = 1.0;
                if (chemical.Equals("Ti", StringComparison.OrdinalIgnoreCase))
                {
                    factor = 20.0;
                }
                else if (chemical.Equals("Sr", StringComparison.OrdinalIgnoreCase))
                {
                    factor = 15.0;
                }

                // Formula: W_add = ((Target - <x>) / 100) * W_furnace * Factor
                double targetValue = maxTarget;
                if (chemical.Equals("Ti", StringComparison.OrdinalIgnoreCase) || 
                    chemical.Equals("Si", StringComparison.OrdinalIgnoreCase) || 
                    chemical.Equals("Cu", StringComparison.OrdinalIgnoreCase))
                {
                    targetValue = minTarget;
                }

                double additionKg = ((targetValue - actualValue) / 100.0) * dto.FurnaceWeight * factor;
                double additionGrams = additionKg * 1000.0;
                
                dto.CalculatedAdditions[chemical] = Math.Round(additionGrams, 0); // Grams are usually whole numbers

                // Record step for transparency
                dto.CalculationDetails ??= new List<CalculationStepDto>();
                dto.CalculationDetails.Add(new CalculationStepDto
                {
                    Chemical = chemical,
                    ActualValue = actualValue,
                    MinSpec = minTarget,
                    MaxSpec = rejectionTarget,
                    TargetValue = targetValue,
                    Factor = factor,
                    FurnaceWeight = dto.FurnaceWeight,
                    AdditionGrams = dto.CalculatedAdditions[chemical],
                    Formula = "((Target - Actual) / 100) * Furnace Weight * Factor"
                });
            }
            else if (actualValue > rejectionTarget)
            {
                // OVER Customer Max Spec -> Batch Rejected
                // rejectionTarget = Customer Max when given; falls back to Our Spec Max only if no Customer Max exists.
                dto.CalculationDetails ??= new List<CalculationStepDto>();
                dto.CalculationDetails.Add(new CalculationStepDto
                {
                    Chemical = chemical,
                    ActualValue = actualValue,
                    MinSpec = minTarget,
                    MaxSpec = rejectionTarget,
                    TargetValue = rejectionTarget,
                    Factor = 1.0,
                    FurnaceWeight = dto.FurnaceWeight,
                    AdditionGrams = 0,
                    Formula = "Over Customer Max Spec — Batch Rejected"
                });
            }
            else
            {
                // In Spec (between Min and Customer Max)
                dto.HiddenChemicals.Add(chemical);

                // Also record target spec so frontend dynamically shows Tgt: X% for in-spec elements
                double inSpecTarget = (chemical.Equals("Ti", StringComparison.OrdinalIgnoreCase) || 
                                       chemical.Equals("Si", StringComparison.OrdinalIgnoreCase) || 
                                       chemical.Equals("Cu", StringComparison.OrdinalIgnoreCase)) 
                                       ? minTarget : (maxTarget > 0 ? maxTarget : minTarget);

                dto.CalculationDetails ??= new List<CalculationStepDto>();
                dto.CalculationDetails.Add(new CalculationStepDto
                {
                    Chemical = chemical,
                    ActualValue = actualValue,
                    MinSpec = minTarget,
                    MaxSpec = rejectionTarget,
                    TargetValue = inSpecTarget,
                    Factor = 1.0,
                    FurnaceWeight = dto.FurnaceWeight,
                    AdditionGrams = 0,
                    Formula = "In Specification"
                });
            }
        }

        // Special logic for part name CP4:
        // Manganese (Mn) calculation depends on Iron (Fe) actual value:
        // If Fe (actual value) <= 0.4: no need to calculate Mn addition.
        // If Fe (actual value) > 0.4: maintain Mn maximum spec from Master Spec
        // (if Our Spec Max is given, go with Our Spec; if not given, go with Customer Spec).
        bool isCP4 = !string.IsNullOrWhiteSpace(dto.PartName) && 
                     (dto.PartName.Trim().Equals("CP4", StringComparison.OrdinalIgnoreCase) ||
                      dto.PartName.Trim().Replace(" ", "").Replace("-", "").Equals("CP4", StringComparison.OrdinalIgnoreCase));

        if (isCP4)
        {
            double feActual = dto.ActualValues.TryGetValue("Fe", out double feVal) ? feVal : 0.0;
            double mnActual = dto.ActualValues.TryGetValue("Mn", out double mnV) ? mnV : 0.0;

            if (feActual > 0.4)
            {
                // Priority: Our Spec (MaxTargets) first. If not given (or 0), fallback to Customer Spec (CustomerMaxTargets).
                double mnMaxSpec = (masterSpecs.MaxTargets.TryGetValue("Mn", out double mMax) && mMax > 0) 
                                   ? mMax 
                                   : (masterSpecs.CustomerMaxTargets.TryGetValue("Mn", out double cMnMax) && cMnMax > 0 ? cMnMax : 0.35);

                double mnMinSpec = (masterSpecs.MinTargets.TryGetValue("Mn", out double mMin) && mMin > 0) 
                                   ? mMin 
                                   : (masterSpecs.CustomerMinTargets.TryGetValue("Mn", out double cMnMin) && cMnMin > 0 ? cMnMin : 0.0);

                if (mnActual < mnMaxSpec)
                {
                    double furnaceWeight = dto.FurnaceWeight > 0 ? dto.FurnaceWeight : 450;
                    double mnAdditionKg = ((mnMaxSpec - mnActual) / 100.0) * furnaceWeight;
                    double mnAdditionGrams = mnAdditionKg * 1000.0;
                    double roundedMnGrams = Math.Round(mnAdditionGrams, 0);

                    dto.CalculatedAdditions["Mn"] = roundedMnGrams;
                    dto.HiddenChemicals.RemoveAll(c => c.Equals("Mn", StringComparison.OrdinalIgnoreCase));

                    dto.CalculationDetails ??= new List<CalculationStepDto>();
                    dto.CalculationDetails.RemoveAll(c => c.Chemical.Equals("Mn", StringComparison.OrdinalIgnoreCase));

                    dto.CalculationDetails.Add(new CalculationStepDto
                    {
                        Chemical = "Mn",
                        ActualValue = mnActual,
                        MinSpec = mnMinSpec,
                        MaxSpec = mnMaxSpec,
                        TargetValue = mnMaxSpec,
                        Factor = 1.0,
                        FurnaceWeight = furnaceWeight,
                        AdditionGrams = roundedMnGrams,
                        Formula = "((Target - Actual) / 100) * Furnace Weight"
                    });
                }
                else
                {
                    // Mn actual is already >= Mn Max Spec
                    dto.CalculatedAdditions.Remove("Mn");
                    dto.CalculationDetails?.RemoveAll(c => c.Chemical.Equals("Mn", StringComparison.OrdinalIgnoreCase));
                    if (dto.ActualValues.ContainsKey("Mn") && !dto.HiddenChemicals.Any(c => c.Equals("Mn", StringComparison.OrdinalIgnoreCase)))
                    {
                        dto.HiddenChemicals.Add("Mn");
                    }
                }
            }
            else
            {
                // Fe actual <= 0.4: No Manganese addition needed
                dto.CalculatedAdditions.Remove("Mn");
                dto.CalculationDetails?.RemoveAll(c => c.Chemical.Equals("Mn", StringComparison.OrdinalIgnoreCase));
                if (dto.ActualValues.ContainsKey("Mn") && !dto.HiddenChemicals.Any(c => c.Equals("Mn", StringComparison.OrdinalIgnoreCase)))
                {
                    dto.HiddenChemicals.Add("Mn");
                }
            }
        }
    }
}
