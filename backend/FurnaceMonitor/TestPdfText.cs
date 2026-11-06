using System;
using System.IO;
using UglyToad.PdfPig;

class Program
{
    static void Main()
    {
        string path = @"sample.pdf";
        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
            {
                Console.WriteLine("--- PAGE ---");
                Console.WriteLine(page.Text);
            }
        }
    }
}
