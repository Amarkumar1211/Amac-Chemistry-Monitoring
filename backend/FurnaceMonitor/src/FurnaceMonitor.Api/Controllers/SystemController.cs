using FurnaceMonitor.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public SystemController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Returns the host machine's LAN IP address, Smart TV direct display URL,
    /// and Spectro PDF watch folder location.
    /// </summary>
    [HttpGet("network-info")]
    [AllowAnonymous]
    public IActionResult GetNetworkInfo()
    {
        var configuredIp = _configuration.GetValue<string>("Network:LanIp");
        var lanIp = (!string.IsNullOrWhiteSpace(configuredIp) && !configuredIp.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
            ? configuredIp
            : NetworkHelper.GetLocalIpAddress();

        var port = 5005;
        var allIps = NetworkHelper.GetAllLocalIpAddresses();

        var configuredPath = _configuration.GetValue<string>("PdfWatcher:DirectoryPath");
        var spectroFolder = (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Spectro PDF")
            : configuredPath;

        return Ok(new
        {
            success = true,
            lanIp,
            port,
            localUrl = $"http://localhost:{port}",
            tvUrl = $"http://{lanIp}:{port}/display",
            allIps = allIps.Select(i => new { i.InterfaceName, i.Description, i.IpAddress, i.HasGateway }),
            spectroPdfPath = spectroFolder
        });
    }
}
