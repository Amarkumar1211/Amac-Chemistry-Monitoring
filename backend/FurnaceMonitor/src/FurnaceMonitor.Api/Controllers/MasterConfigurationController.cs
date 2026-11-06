using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FurnaceMonitor.Application.DTOs.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FurnaceMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class MasterConfigurationController : ControllerBase
{
    private string GetFilePath()
    {
        return Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "MasterConfiguration.xlsx");
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var filePath = GetFilePath();
        if (System.IO.File.Exists(filePath))
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            return Ok(ApiResponse.Ok(new
            {
                Exists = true,
                LastModified = fileInfo.LastWriteTimeUtc,
                Size = fileInfo.Length
            }));
        }

        return Ok(ApiResponse.Ok(new
        {
            Exists = false,
            LastModified = (System.DateTime?)null,
            Size = 0
        }));
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadExcel(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file uploaded."));

        if (!file.FileName.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("Invalid file format. Please upload an .xlsx file."));

        var filePath = GetFilePath();
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return Ok(ApiResponse.Ok((object)null, "Master Configuration file uploaded successfully."));
    }

    [HttpGet("download")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public IActionResult DownloadExcel()
    {
        var filePath = GetFilePath();
        if (!System.IO.File.Exists(filePath))
            return NotFound(ApiResponse.Fail("Master Configuration file not found."));

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MasterConfiguration.xlsx");
    }

    [HttpDelete]
    public IActionResult DeleteExcel()
    {
        var filePath = GetFilePath();
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            return Ok(ApiResponse.Ok((object)null, "Master Configuration file deleted successfully."));
        }

        return NotFound(ApiResponse.Fail("Master Configuration file not found."));
    }

    [HttpGet("data")]
    public IActionResult GetData()
    {
        var filePath = GetFilePath();
        if (!System.IO.File.Exists(filePath))
            return NotFound(ApiResponse.Fail("Master Configuration file not found."));

        var result = new List<object>();

        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return Ok(ApiResponse.Ok(result));

            var rows = worksheet.RowsUsed().Skip(2); // Skip 2 header rows

            foreach (var row in rows)
            {
                var slNo = row.Cell(1).GetString()?.Trim();
                var partName = row.Cell(2).GetString()?.Trim();
                var partNo = row.Cell(3).GetString()?.Trim();
                
                if (string.IsNullOrEmpty(partName) && string.IsNullOrEmpty(partNo))
                    continue;

                result.Add(new
                {
                    slNo,
                    partName,
                    partNo,
                    gradeName = row.Cell(4).GetString()?.Trim(),
                    colorCode = row.Cell(5).GetString()?.Trim(),
                    specType = row.Cell(6).GetString()?.Trim(),
                    si = row.Cell(7).GetString()?.Trim(),
                    fe = row.Cell(8).GetString()?.Trim(),
                    cu = row.Cell(9).GetString()?.Trim(),
                    mn = row.Cell(10).GetString()?.Trim(),
                    mg = row.Cell(11).GetString()?.Trim(),
                    zn = row.Cell(12).GetString()?.Trim(),
                    ti = row.Cell(13).GetString()?.Trim(),
                    sr = row.Cell(14).GetString()?.Trim(),
                    pb = row.Cell(15).GetString()?.Trim(),
                    sn = row.Cell(16).GetString()?.Trim(),
                    ni = row.Cell(17).GetString()?.Trim(),
                    cr = row.Cell(18).GetString()?.Trim()
                });
            }

            return Ok(ApiResponse.Ok(result));
        }
        catch (System.Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"Failed to parse Excel file: {ex.Message}"));
        }
    }
}
