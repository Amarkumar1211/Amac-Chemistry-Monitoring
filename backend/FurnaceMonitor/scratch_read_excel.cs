using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

class Program
{
    static void Main()
    {
        string excelPath = @"c:\Users\AMAC-U1\Desktop\Quality IT\backend\FurnaceMonitor\Data\MasterConfiguration.xlsx";
        using var workbook = new XLWorkbook(excelPath);
        var worksheet = workbook.Worksheets.First();
        var rows = worksheet.RowsUsed().Skip(2);

        foreach (var row in rows)
        {
            var partName = row.Cell(2).GetString()?.Trim();
            var partNo = row.Cell(3).GetString()?.Trim();
            var gradeName = row.Cell(4).GetString()?.Trim();
            var specType = row.Cell(6).GetString()?.Trim();

            if ((partName != null && partName.Contains("CP4", StringComparison.OrdinalIgnoreCase)) ||
                (gradeName != null && gradeName.Contains("CP4", StringComparison.OrdinalIgnoreCase)) ||
                (partNo != null && partNo.Contains("CP4", StringComparison.OrdinalIgnoreCase)))
            {
                var ti = row.Cell(13).GetString()?.Trim();
                Console.WriteLine($"Found CP4 - Part Name: {partName}, Grade Name: {gradeName}, Part No: {partNo}, Spec Type: {specType}, Ti: {ti}");
            }
        }
    }
}
