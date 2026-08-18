namespace StockSync.Tests;

using Microsoft.Data.Sqlite;
using StockSync.Data;
using StockSync.Models;
using StockSync.Repositories;
using StockSync.Services;
using Xunit;

public class InventorySyncServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SourceInventoryRepository _sourceRepo;
    private readonly DestinationInventoryRepository _destRepo;
    private readonly SyncHistoryRepository _syncHistoryRepo;
    private readonly InventorySyncService _syncService;

    public InventorySyncServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"stocksync_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";

        var initializer = new DatabaseInitializer(_connectionString, _dbPath);
        initializer.Initialize();

        // Clear seeded source items so tests control exact data
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM source_inventory; DELETE FROM destination_inventory; DELETE FROM sync_history;";
            cmd.ExecuteNonQuery();
        }

        _sourceRepo = new SourceInventoryRepository(_connectionString);
        _destRepo = new DestinationInventoryRepository(_connectionString);
        _syncHistoryRepo = new SyncHistoryRepository(_connectionString);
        _syncService = new InventorySyncService(_sourceRepo, _destRepo, _syncHistoryRepo);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    private void InsertSource(string name, int quantity)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO source_inventory (product_name, quantity, updated_at) VALUES (@name, @qty, @updated);";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private void InsertDestination(string name, int quantity)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO destination_inventory (product_name, quantity, updated_at) VALUES (@name, @qty, @updated);";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task Test1_NoDifferences_ShouldApplyZeroChanges()
    {
        // Arrange: identical inventory in source and destination
        InsertSource("Blue Shoes", 20);
        InsertSource("Black Shirt", 15);

        InsertDestination("Blue Shoes", 20);
        InsertDestination("Black Shirt", 15);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsChecked);
        Assert.Equal(0, result.ItemsAdded);
        Assert.Equal(0, result.ItemsUpdated);
        Assert.Equal(0, result.ItemsRemoved);
        Assert.Equal(0, result.ChangesApplied);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Test2_NewProduct_ShouldAddToDestination()
    {
        // Arrange: source has product not in destination
        InsertSource("Blue Shoes", 20);
        InsertSource("Red Cap", 8);

        InsertDestination("Blue Shoes", 20);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.ItemsChecked);
        Assert.Equal(1, result.ItemsAdded);
        Assert.Equal(0, result.ItemsUpdated);
        Assert.Equal(0, result.ItemsRemoved);
        Assert.Equal(1, result.ChangesApplied);

        var destItems = await _destRepo.GetAllAsync();
        Assert.Equal(2, destItems.Count);
        Assert.Contains(destItems, d => d.ProductName == "Red Cap" && d.Quantity == 8);
    }

    [Fact]
    public async Task Test3_QuantityUpdate_ShouldUpdateDestinationQuantity()
    {
        // Arrange: same product with different quantities
        InsertSource("Blue Shoes", 50);
        InsertDestination("Blue Shoes", 20);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsChecked);
        Assert.Equal(0, result.ItemsAdded);
        Assert.Equal(1, result.ItemsUpdated);
        Assert.Equal(0, result.ItemsRemoved);
        Assert.Equal(1, result.ChangesApplied);

        var destItems = await _destRepo.GetAllAsync();
        Assert.Single(destItems);
        Assert.Equal("Blue Shoes", destItems[0].ProductName);
        Assert.Equal(50, destItems[0].Quantity);
    }

    [Fact]
    public async Task Test4_RemovedProduct_ShouldRemoveFromDestination()
    {
        // Arrange: destination has item that no longer exists in source
        InsertSource("Blue Shoes", 20);

        InsertDestination("Blue Shoes", 20);
        InsertDestination("Obsolete Item", 5);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsChecked);
        Assert.Equal(0, result.ItemsAdded);
        Assert.Equal(0, result.ItemsUpdated);
        Assert.Equal(1, result.ItemsRemoved);
        Assert.Equal(1, result.ChangesApplied);

        var destItems = await _destRepo.GetAllAsync();
        Assert.Single(destItems);
        Assert.Equal("Blue Shoes", destItems[0].ProductName);
        Assert.DoesNotContain(destItems, d => d.ProductName == "Obsolete Item");
    }

    [Fact]
    public async Task Test5_MultipleChanges_ShouldApplyAllAdditionsUpdatesRemovals()
    {
        // Arrange:
        // - "Blue Shoes": qty 20 -> 35 (Update)
        // - "Red Cap": new in source (Add)
        // - "Black Shirt": identical (Unchanged)
        // - "Discontinued Pants": in destination only (Remove)
        InsertSource("Blue Shoes", 35);
        InsertSource("Black Shirt", 15);
        InsertSource("Red Cap", 8);

        InsertDestination("Blue Shoes", 20);
        InsertDestination("Black Shirt", 15);
        InsertDestination("Discontinued Pants", 10);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.ItemsChecked);
        Assert.Equal(1, result.ItemsAdded);
        Assert.Equal(1, result.ItemsUpdated);
        Assert.Equal(1, result.ItemsRemoved);
        Assert.Equal(3, result.ChangesApplied);

        var destItems = await _destRepo.GetAllAsync();
        Assert.Equal(3, destItems.Count);

        var blueShoes = destItems.FirstOrDefault(d => d.ProductName == "Blue Shoes");
        var blackShirt = destItems.FirstOrDefault(d => d.ProductName == "Black Shirt");
        var redCap = destItems.FirstOrDefault(d => d.ProductName == "Red Cap");

        Assert.NotNull(blueShoes);
        Assert.Equal(35, blueShoes.Quantity);

        Assert.NotNull(blackShirt);
        Assert.Equal(15, blackShirt.Quantity);

        Assert.NotNull(redCap);
        Assert.Equal(8, redCap.Quantity);

        Assert.DoesNotContain(destItems, d => d.ProductName == "Discontinued Pants");
    }

    [Fact]
    public async Task Test6_SyncHistory_ShouldRecordSuccessfulExecution()
    {
        // Arrange
        InsertSource("Blue Shoes", 20);
        InsertDestination("Blue Shoes", 10);

        // Act
        var result = await _syncService.SynchronizeAsync();

        // Assert
        Assert.True(result.Success);

        var historyList = await _syncHistoryRepo.GetAllAsync();
        Assert.NotEmpty(historyList);

        var latestHistory = historyList[0];
        Assert.Equal("Success", latestHistory.Status);
        Assert.Contains("Checked: 1", latestHistory.Details);
        Assert.Contains("Updated: 1", latestHistory.Details);
    }

    [Fact]
    public async Task Test7_Failure_ShouldHandleGracefullyAndRecordFailure()
    {
        // Arrange: A faulty destination repository that throws during ApplyChangesAsync
        var failingDestRepo = new FailingDestinationRepository();
        var failingSyncService = new InventorySyncService(_sourceRepo, failingDestRepo, _syncHistoryRepo);

        InsertSource("Blue Shoes", 20);

        // Act
        var result = await failingSyncService.SynchronizeAsync();

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Database connection failed", result.ErrorMessage);

        var historyList = await _syncHistoryRepo.GetAllAsync();
        Assert.NotEmpty(historyList);

        var latestHistory = historyList[0];
        Assert.Equal("Failed", latestHistory.Status);
        Assert.Contains("Database connection failed", latestHistory.Details);
    }

    private class FailingDestinationRepository : IDestinationInventoryRepository
    {
        public Task<List<InventoryItem>> GetAllAsync() => Task.FromResult(new List<InventoryItem>());
        public Task AddAsync(InventoryItem item) => throw new InvalidOperationException("Database connection failed");
        public Task UpdateAsync(InventoryItem item) => throw new InvalidOperationException("Database connection failed");
        public Task DeleteAsync(int id) => throw new InvalidOperationException("Database connection failed");
        public Task ApplyChangesAsync(IEnumerable<InventoryItem> toAdd, IEnumerable<InventoryItem> toUpdate, IEnumerable<int> toDeleteIds)
            => throw new InvalidOperationException("Database connection failed");
    }
}
