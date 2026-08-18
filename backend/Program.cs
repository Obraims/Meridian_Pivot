using StockSync.Data;
using StockSync.Repositories;
using StockSync.Services;

var builder = WebApplication.CreateBuilder(args);

// Database path & connection string
var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "database", "stocksync.db"));
var connectionString = $"Data Source={dbPath}";

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton(new DatabaseInitializer(connectionString, dbPath));
builder.Services.AddScoped<ISourceInventoryRepository>(_ => new SourceInventoryRepository(connectionString));
builder.Services.AddScoped<IDestinationInventoryRepository>(_ => new DestinationInventoryRepository(connectionString));
builder.Services.AddScoped<ISyncHistoryRepository>(_ => new SyncHistoryRepository(connectionString));
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();

var app = builder.Build();

app.UseCors();

// Initialize database schema and seed data
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    initializer.Initialize();
}

// Health endpoints
app.MapGet("/", () => Results.Ok(new { message = "StockSync API is running", status = "Healthy" }));
app.MapGet("/health", () => Results.Ok(new { message = "StockSync API is running", status = "Healthy" }));

// Source inventory endpoint
app.MapGet("/source/inventory", async (ISourceInventoryRepository repository) =>
{
    var items = await repository.GetAllAsync();
    return Results.Ok(items);
});

// Destination inventory endpoint
app.MapGet("/destination/inventory", async (IDestinationInventoryRepository repository) =>
{
    var items = await repository.GetAllAsync();
    return Results.Ok(items);
});

// Sync history endpoint
app.MapGet("/sync/history", async (ISyncHistoryRepository repository) =>
{
    var history = await repository.GetAllAsync();
    return Results.Ok(history);
});

// Synchronization endpoint
app.MapPost("/api/sync", async (IInventorySyncService syncService, CancellationToken ct) =>
{
    var result = await syncService.SynchronizeAsync(ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();
