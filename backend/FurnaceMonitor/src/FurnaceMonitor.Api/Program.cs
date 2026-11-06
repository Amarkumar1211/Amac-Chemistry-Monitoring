using System.Text;
using FurnaceMonitor.Api.Middleware;
using FurnaceMonitor.Api.Extensions;
using FurnaceMonitor.Infrastructure;
using FurnaceMonitor.Infrastructure.Persistence;
using FurnaceMonitor.Infrastructure.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to bind to port 5005 on all interfaces (0.0.0.0) so both localhost and LAN Smart TVs can connect
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5005);
});

// Required for PdfPig to parse certain fonts correctly in .NET Core/.NET 5+
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Configure Serilog (file + console) from appsettings and built-in defaults.
builder.Host.UseSerilog((context, services, cfg) => cfg
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "furnace-monitor-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90));

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Furnace Monitor API",
        Version = "v1",
        Description = "Real-time furnace monitoring and operator management system."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT token as Bearer <token>."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 256; // 256 KB
});

// Infrastructure DI (DbContext, repositories, services)
builder.Services.AddInfrastructure(builder.Configuration);

// JWT authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    // Enable JWT access token in the SignalR query string.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var jti = context.Principal?.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var session = await dbContext.UserSessions.FirstOrDefaultAsync(s => s.JwtId == jti);
                if (session == null || !session.IsActive)
                {
                    context.Fail("Session has been invalidated.");
                }
            }
        }
    };
});

builder.Services.AddAuthorization();

// CORS for the React dev server, Smart TVs over LAN, and internal displays.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow all LAN connections (Smart TVs, operator terminals)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Wire the realtime events (batch service -> SignalR notification service).
var provider = app.Services;
FurnaceMonitor.Infrastructure.DependencyInjection.WireRealtimeEvents(provider);

// Middleware pipeline
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

app.UseCors("ClientApp");

app.UseDefaultFiles(); // Automatically serve index.html from wwwroot

// Serve static files with explicit MIME types for video.
// Smart TVs require correct Content-Type and HTTP 206 range-request support for video streaming.
var fileTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
fileTypeProvider.Mappings[".mp4"]  = "video/mp4";
fileTypeProvider.Mappings[".webm"] = "video/webm";
fileTypeProvider.Mappings[".ogg"]  = "video/ogg";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = fileTypeProvider,
    OnPrepareResponse = ctx =>
    {
        // Allow Smart TVs / browsers to cache video files for 1 hour
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=3600";
        // Explicitly advertise range-request support — required by Smart TV browsers for video
        ctx.Context.Response.Headers["Accept-Ranges"] = "bytes";
    }
}); // Serve React SPA bundle from wwwroot

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hub
app.MapHub<FurnaceHub>("/hubs/furnace");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapFallbackToFile("index.html"); // Handle React Client-Side Routing

// Apply pending migrations and seed initial data at startup.
await InfrastructureSeed.MigrateAndSeedAsync(app.Services, app.Logger);

// Detect network information for TV link and display banner
var configuredLanIp = builder.Configuration.GetValue<string>("Network:LanIp");
var lanIp = (!string.IsNullOrWhiteSpace(configuredLanIp) && !configuredLanIp.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
    ? configuredLanIp
    : FurnaceMonitor.Api.Helpers.NetworkHelper.GetLocalIpAddress();
var localUrl = "http://localhost:5005";
var tvUrl = $"http://{lanIp}:5005/display";
var spectroFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Spectro PDF");
var allIps = FurnaceMonitor.Api.Helpers.NetworkHelper.GetAllLocalIpAddresses();
var secondaryIps = allIps.Where(i => i.IpAddress != lanIp).ToList();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║               AMAC QUALITY IT - FURNACE MONITORING SYSTEM                    ║
╠══════════════════════════════════════════════════════════════════════════════╣");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"║  [PC Local Access]    : {localUrl,-52} ║");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"║  [Smart TV Link (LAN)]: {tvUrl,-52} ║");
if (secondaryIps.Any())
{
    foreach (var sec in secondaryIps)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        var altText = $"http://{sec.IpAddress}:5005/display ({sec.InterfaceName})";
        if (altText.Length > 52) altText = altText.Substring(0, 52);
        Console.WriteLine($"║  [Alt Link (Other)]   : {altText,-52} ║");
    }
}
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"║  [Spectro PDF Watch]  : {spectroFolder,-52} ║");
var activeDb = FurnaceMonitor.Infrastructure.DependencyInjection.ActiveDatabasePath;
if (activeDb.Length > 52) activeDb = "..." + activeDb.Substring(activeDb.Length - 49);
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"║  [Database Active]    : {activeDb,-52} ║");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"╚══════════════════════════════════════════════════════════════════════════════╝
");
Console.ResetColor();

// Ensure Desktop Shortcut with AMAC logo exists on the user's desktop
EnsureDesktopShortcut();

// Automatically launch browser to local URL on startup
_ = Task.Run(async () =>
{
    await Task.Delay(1200);
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = localUrl,
            UseShellExecute = true
        });
    }
    catch
    {
        try
        {
            System.Diagnostics.Process.Start("cmd", $"/c start {localUrl}");
        }
        catch { /* Ignore */ }
    }
});

app.Logger.LogInformation("Furnace Monitor API started on {LocalUrl} (TV: {TvUrl})", localUrl, tvUrl);
app.Run();

static void EnsureDesktopShortcut()
{
    try
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktopPath, "AMAC Furnace Monitor.lnk");
        var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FurnaceMonitor.exe");
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");

        if (!File.Exists(shortcutPath) && File.Exists(exePath))
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = AppContext.BaseDirectory;
                    shortcut.Description = "AMAC Furnace Monitor - Quality IT";
                    if (File.Exists(iconPath))
                    {
                        shortcut.IconLocation = $"{iconPath},0";
                    }
                    else
                    {
                        shortcut.IconLocation = $"{exePath},0";
                    }
                    shortcut.Save();
                }
            }
        }
    }
    catch
    {
        // Non-critical, ignore if permissions restrict
    }
}

// Expose Program for integration tests.
public partial class Program { }

namespace FurnaceMonitor.Api.Extensions
{
    /// <summary>
    /// Marker for the API extensions namespace.
    /// </summary>
    public static class ApiStartupMarker { }
}
