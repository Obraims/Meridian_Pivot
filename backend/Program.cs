using Microsoft.Extensions.FileProviders;
using StockSync.Data;
using StockSync.Models;
using StockSync.Repositories;
using StockSync.Services;

var builder = WebApplication.CreateBuilder(args);

// Find frontend directory across all execution modes
string FindFrontendDirectory()
{
    var candidates = new[]
    {
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "frontend")),
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "frontend")),
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "frontend")),
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frontend")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend")),
        "/frontend"
    };

    foreach (var dir in candidates)
    {
        if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "index.html")))
        {
            return dir;
        }
    }
    return candidates[0];
}

var frontendPath = FindFrontendDirectory();

// Find database directory across all execution modes
string FindDatabasePath()
{
    var dbDir = Path.GetFullPath(Path.Combine(frontendPath, "..", "database"));
    if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);
    return Path.Combine(dbDir, "stocksync.db");
}

var dbPath = FindDatabasePath();
var connectionString = $"Data Source={dbPath}";

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();

// Database & Repositories
builder.Services.AddSingleton(new DatabaseInitializer(connectionString, dbPath));
builder.Services.AddScoped<IAttendeeRepository>(_ => new AttendeeRepository(connectionString));
builder.Services.AddScoped<IPrintJobRepository>(_ => new PrintJobRepository(connectionString));
builder.Services.AddScoped<IWebhookEventRepository>(_ => new WebhookEventRepository(connectionString));

// Domain Services & RabbitMQ Publisher
builder.Services.AddScoped<IBadgePrintPublisher, BadgePrintPublisher>();
builder.Services.AddScoped<ICheckinService, CheckinService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();

// RabbitMQ Mock Badge Printer Worker (BackgroundService)
builder.Services.AddHostedService<MockBadgePrinterWorker>();

var app = builder.Build();

app.UseCors();

// Initialize SQLite Schema & Seed Initial Attendees (A001 Alice, A002 Brian, A003 Carol)
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    initializer.Initialize();
}

// Serve Static Frontend UI Files
if (Directory.Exists(frontendPath))
{
    var fileProvider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });
}

// Root Endpoint: Serve Kiosk UI
app.MapGet("/", () =>
{
    var indexPath = Path.Combine(frontendPath, "index.html");
    return File.Exists(indexPath)
        ? Results.File(indexPath, "text/html")
        : Results.Ok(new { system = "Solstice Events Check-in Kiosk", status = "Healthy", version = "2.0-Asynchronous" });
});

// Health & System Info
app.MapGet("/health", () => Results.Ok(new { system = "Solstice Events Check-in Kiosk", status = "Healthy" }));
app.MapGet("/api/health", () => Results.Ok(new { system = "Solstice Events Check-in Kiosk", status = "Healthy" }));

// KIOSK API: Attendees List
app.MapGet("/api/attendees", async (IAttendeeRepository repo) =>
{
    var attendees = await repo.GetAllAsync();
    return Results.Ok(attendees);
});

// KIOSK API: Single Attendee
app.MapGet("/api/attendees/{id}", async (string id, IAttendeeRepository repo) =>
{
    var attendee = await repo.GetByIdAsync(id);
    return attendee != null ? Results.Ok(attendee) : Results.NotFound(new { message = $"Attendee '{id}' not found." });
});

// KIOSK API: Trigger Check-in Scan (Path Param)
app.MapPost("/api/checkin/{attendeeId}", async (string attendeeId, ICheckinService checkinService) =>
{
    var result = await checkinService.ProcessCheckinAsync(attendeeId);
    if (!result.Success && result.Status == "NOT_FOUND")
    {
        return Results.NotFound(new { error = result.Message, attendeeId, status = "NOT_FOUND" });
    }
    if (!result.Success && result.Status == "INVALID_ID")
    {
        return Results.BadRequest(new { error = result.Message, attendeeId, status = "INVALID_ID" });
    }
    if (result.Status == AttendeeStatus.CheckedIn)
    {
        return Results.Json(new
        {
            attendeeId = result.AttendeeId,
            name = result.AttendeeName,
            status = "ALREADY_CHECKED_IN",
            message = result.Message
        }, statusCode: 409);
    }
    return Results.Ok(new
    {
        attendeeId = result.AttendeeId,
        name = result.AttendeeName,
        status = result.Status,
        message = result.Message,
        printJobId = result.PrintJobId
    });
});

// KIOSK API: Trigger Check-in Scan (JSON Body)
app.MapPost("/api/checkin", async (CheckinRequestBody body, ICheckinService checkinService) =>
{
    var id = body?.AttendeeId ?? string.Empty;
    var result = await checkinService.ProcessCheckinAsync(id);
    if (!result.Success && result.Status == "NOT_FOUND")
    {
        return Results.NotFound(new { error = result.Message, attendeeId = id, status = "NOT_FOUND" });
    }
    if (!result.Success && result.Status == "INVALID_ID")
    {
        return Results.BadRequest(new { error = result.Message, attendeeId = id, status = "INVALID_ID" });
    }
    if (result.Status == AttendeeStatus.CheckedIn)
    {
        return Results.Json(new
        {
            attendeeId = result.AttendeeId,
            name = result.AttendeeName,
            status = "ALREADY_CHECKED_IN",
            message = result.Message
        }, statusCode: 409);
    }
    return Results.Ok(new
    {
        attendeeId = result.AttendeeId,
        name = result.AttendeeName,
        status = result.Status,
        message = result.Message,
        printJobId = result.PrintJobId
    });
});

// KIOSK API: Check Status for Polling
app.MapGet("/api/checkin/{attendeeId}/status", async (string attendeeId, IAttendeeRepository repo) =>
{
    var attendee = await repo.GetByIdAsync(attendeeId);
    if (attendee == null)
    {
        return Results.NotFound(new { error = $"Attendee '{attendeeId}' not found." });
    }
    return Results.Ok(new
    {
        attendeeId = attendee.Id,
        name = attendee.Name,
        status = attendee.Status
    });
});

// WEBHOOK CALLBACK: Mock Badge Printer Callback
app.MapPost("/api/webhooks/print-completed", async (PrintCompletedWebhookPayload payload, IWebhookService webhookService) =>
{
    var result = await webhookService.ProcessPrintCompletedAsync(payload);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// AUDIT / ADMIN: View Print Jobs
app.MapGet("/api/print-jobs", async (IPrintJobRepository repo) =>
{
    var jobs = await repo.GetAllAsync();
    return Results.Ok(jobs);
});

// DEMO HELPER: Reset System State for Repeat Demonstrations
app.MapPost("/api/reset", async (
    IAttendeeRepository attendeeRepo,
    IPrintJobRepository printJobRepo,
    IWebhookEventRepository webhookRepo) =>
{
    await attendeeRepo.ResetAllAsync();
    await printJobRepo.ResetAllAsync();
    await webhookRepo.ResetAllAsync();
    return Results.Ok(new { message = "System state reset successfully: all attendees set to NOT_CHECKED_IN, print jobs and webhook events cleared." });
});

app.Run();

public record CheckinRequestBody(string? AttendeeId);
public partial class Program { }
