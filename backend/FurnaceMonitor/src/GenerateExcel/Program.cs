using System;
using ClosedXML.Excel;
using System.IO;

namespace GenerateExcel
{
    class Program
    {
        static void Main()
        {
            var path = @"C:\Users\AMAC-U1\Desktop\Quality IT\backend\FurnaceMonitor\src\FurnaceMonitor.Infrastructure\Data\MasterConfiguration.xlsx";
            
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Master");

            ws.Cell("A1").Value = "SL NO";
            ws.Cell("B1").Value = "PART NAME";
            ws.Cell("C1").Value = "PART NO";
            ws.Cell("D1").Value = "GRADE NAME";
            ws.Cell("E1").Value = "COLOUR CODE";
            ws.Cell("F1").Value = "Spec Type";
            ws.Cell("G1").Value = "Si";
            ws.Cell("H1").Value = "Fe";
            ws.Cell("I1").Value = "Cu";
            ws.Cell("J1").Value = "Mn";
            ws.Cell("K1").Value = "Mg";
            ws.Cell("L1").Value = "Zn";
            ws.Cell("M1").Value = "Ti";
            ws.Cell("N1").Value = "Sr";
            ws.Cell("O1").Value = "Pb";
            ws.Cell("P1").Value = "Sn";
            ws.Cell("Q1").Value = "Ni";
            ws.Cell("R1").Value = "Cr";
            ws.Cell("S1").Value = "REMARKS";

            ws.Cell("A3").Value = 41;
            ws.Cell("B3").Value = "FRONT PLATE";
            ws.Cell("C3").Value = "3416";
            ws.Cell("D3").Value = "ALSI7CU3MG";
            ws.Cell("E3").Value = "PINK";
            ws.Cell("F3").Value = "Customer Spec";
            ws.Cell("G3").Value = "6.50 - 8.50";
            ws.Cell("H3").Value = "0.80 MAX";
            ws.Cell("I3").Value = "3.0 - 4.0";
            ws.Cell("J3").Value = "0.20 - 0.65";
            ws.Cell("K3").Value = "0.30 - 0.60";
            ws.Cell("L3").Value = "0.05 MAX";
            ws.Cell("M3").Value = "0.25 MAX";
            ws.Cell("N3").Value = "-";
            ws.Cell("O3").Value = "0.25 MAX";
            ws.Cell("P3").Value = "0.15 MAX";
            ws.Cell("Q3").Value = "0.30 MAX";
            ws.Cell("R3").Value = "-";

            ws.Cell("A4").Value = 41;
            ws.Cell("B4").Value = "FRONT PLATE";
            ws.Cell("C4").Value = "3416";
            ws.Cell("D4").Value = "ALSI7CU3MG";
            ws.Cell("E4").Value = "PINK";
            ws.Cell("F4").Value = "Our Spec";
            ws.Cell("G4").Value = "7.00 - 7.30";
            ws.Cell("H4").Value = "-";
            ws.Cell("I4").Value = "3.1 - 3.5";
            ws.Cell("J4").Value = "0.20 - 0.26";
            ws.Cell("K4").Value = "0.40 - 0.50";
            ws.Cell("L4").Value = "-";
            ws.Cell("M4").Value = "0.10 - 0.11";

            workbook.SaveAs(path);
            Console.WriteLine("Excel generated successfully at " + path);
        }
    }
}
