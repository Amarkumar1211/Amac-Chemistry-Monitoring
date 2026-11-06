using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.DTOs.PartSpecification;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FurnaceMonitor.Api.Extensions;

namespace FurnaceMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PartSpecificationsController : ControllerBase
{
    private readonly IPartSpecificationService _service;

    public PartSpecificationsController(IPartSpecificationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var specs = await _service.GetAllAsync(ct);
        return Ok(ApiResponse.Ok(specs));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SavePartSpecificationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return Ok(ApiResponse.Ok(result, "Specification created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SavePartSpecificationRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return Ok(ApiResponse.Ok((object)null, "Specification updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok((object)null, "Specification deleted."));
    }

    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        await _service.DeleteAllAsync(ct);
        return Ok(ApiResponse.Ok((object)null, "All specifications deleted."));
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadExcel(IFormFile file, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file uploaded."));

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("Invalid file format. Please upload an .xlsx file."));

        try
        {
            using var stream = file.OpenReadStream();
            await _service.ImportFromExcelAsync(stream, User.UserDisplayName(), force, ct);

            return Ok(ApiResponse.Ok((object)null, "Specifications imported successfully."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONFLICT_EXISTING")
        {
            return Conflict(ApiResponse.Fail("CONFLICT_EXISTING"));
        }
    }

    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public async Task<IActionResult> ExportExcel(CancellationToken ct)
    {
        var bytes = await _service.ExportExcelAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "master_specs_export.xlsx");
    }

    [HttpGet("template")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var bytes = await _service.GenerateTemplateAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "master_specs_template.xlsx");
    }
}

