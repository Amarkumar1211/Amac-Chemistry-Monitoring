using FurnaceMonitor.Application.Services.PdfExtraction.Dtos;
using System.IO;

namespace FurnaceMonitor.Application.Services.PdfExtraction;

public interface IPdfExtractionService
{
    ParsedSpectroResultDto ParseSpectroResult(Stream pdfStream);
}
