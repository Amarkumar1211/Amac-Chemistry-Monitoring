#r "C:\Users\AMAC-U1\.nuget\packages\closedxml\0.102.3\lib\netstandard2.1\ClosedXML.dll"
#r "C:\Users\AMAC-U1\.nuget\packages\documentformat.openxml\2.16.0\lib\netstandard2.0\DocumentFormat.OpenXml.dll"
using System;
using System.Linq;
using ClosedXML.Excel;

var workbook = new XLWorkbook(@"C:\Users\AMAC-U1\Desktop\Process Flow updated (1).xlsx");
var worksheet = workbook.Worksheets.FirstOrDefault();
var rows = worksheet.RowsUsed().Take(3);
foreach (var row in rows) {
    Console.WriteLine($"Row {row.RowNumber()}:");
    foreach (var cell in row.CellsUsed()) {
        Console.WriteLine($"  Col {cell.Address.ColumnNumber}: {cell.GetString()}");
    }
}
