using StockSync.Models;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

app.UseCors();

// In-memory source inventory for Phase 1 verification
var sourceInventory = new List<InventoryItem>
{
    new() { Id = 1, ProductName = "Blue Shoes", Quantity = 20, UpdatedAt = DateTime.UtcNow },
    new() { Id = 2, ProductName = "Black Shirt", Quantity = 15, UpdatedAt = DateTime.UtcNow },
    new() { Id = 3, ProductName = "Red Cap", Quantity = 8, UpdatedAt = DateTime.UtcNow }
};

// Health/Welcome endpoint
app.MapGet("/", () => Results.Ok(new { message = "StockSync API is running", status = "Healthy" }));

// Source inventory endpoint
app.MapGet("/source/inventory", () => Results.Ok(sourceInventory));

app.Run();
