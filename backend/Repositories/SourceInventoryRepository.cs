namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class SourceInventoryRepository : ISourceInventoryRepository
{
    private readonly string _connectionString;

    public SourceInventoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<InventoryItem>> GetAllAsync()
    {
        var items = new List<InventoryItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT id, product_name, quantity, updated_at FROM source_inventory ORDER BY id ASC;";
        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new InventoryItem
            {
                Id = reader.GetInt32(0),
                ProductName = reader.GetString(1),
                Quantity = reader.GetInt32(2),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return items;
    }
}
