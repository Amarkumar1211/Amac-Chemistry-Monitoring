using FurnaceMonitor.Application.Services.PdfExtraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

[ApiController]
[Route("api/pdf-upload")]
[Authorize]
public class PdfUploadController : ControllerBase
{
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly ILogger<PdfUploadController> _logger;

    public PdfUploadController(
        IPdfExtractionService pdfExtractionService,
        ILogger<PdfUploadController> logger)
    {
        _pdfExtractionService = pdfExtractionService;
        _logger = logger;
    }

    [HttpPost("parse")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public IActionResult ParsePdf(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (file.ContentType != "application/pdf")
        {
            return BadRequest(new { message = "Only PDF files are allowed." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = _pdfExtractionService.ParseSpectroResult(stream);
            
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            _logger.LogInformation("Parsed Spectro Result: {Json}", json);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF file.");
            return StatusCode(500, new { message = "An error occurred while parsing the PDF.", details = ex.Message, stack = ex.StackTrace });
        }
    }
}
