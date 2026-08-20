using Microsoft.Extensions.FileProviders;
using StockSync.Data;
using StockSync.Models;
using StockSync.Repositories;
using StockSync.Services;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "database", "stocksync.db"));
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
var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "frontend"));
if (Directory.Exists(frontendPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath)
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath)
    });
}

// Health & System Info
app.MapGet("/", () => Results.Ok(new { system = "Solstice Events Check-in Kiosk", status = "Healthy", version = "2.0-Asynchronous" }));
app.MapGet("/health", () => Results.Ok(new { system = "Solstice Events Check-in Kiosk", status = "Healthy" }));

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

// KIOSK API: Trigger Check-in Scan
app.MapPost("/api/checkin/{attendeeId}", async (string attendeeId, ICheckinService checkinService) =>
{
    var result = await checkinService.ProcessCheckinAsync(attendeeId);
    if (!result.Success && result.Status == "NOT_FOUND")
    {
        return Results.NotFound(result);
    }
    if (!result.Success && result.Status == "INVALID_ID")
    {
        return Results.BadRequest(result);
    }
    return Results.Ok(result);
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

public partial class Program { }
