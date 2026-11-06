using System;
using System.IO;
using UglyToad.PdfPig;

class Program
{
    static void Main()
    {
        string path = @"sample.pdf";
        if (File.Exists(path))
        {
            using var document = PdfDocument.Open(path);
            foreach (var page in document.GetPages())
            {
                Console.WriteLine(page.Text);
            }
        }
        else
        {
            Console.WriteLine("Sample file not found: " + path);
        }
    }
}
