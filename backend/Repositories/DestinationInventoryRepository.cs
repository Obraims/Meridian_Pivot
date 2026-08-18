namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class DestinationInventoryRepository : IDestinationInventoryRepository
{
    private readonly string _connectionString;

    public DestinationInventoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<InventoryItem>> GetAllAsync()
    {
        var items = new List<InventoryItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT id, product_name, quantity, updated_at FROM destination_inventory ORDER BY id ASC;";
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

    public async Task AddAsync(InventoryItem item)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            INSERT INTO destination_inventory (product_name, quantity, updated_at)
            VALUES (@product_name, @quantity, @updated_at);
        ";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@product_name", item.ProductName);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(InventoryItem item)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            UPDATE destination_inventory
            SET quantity = @quantity, updated_at = @updated_at
            WHERE id = @id;
        ";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@id", item.Id);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "DELETE FROM destination_inventory WHERE id = @id;";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ApplyChangesAsync(
        IEnumerable<InventoryItem> toAdd,
        IEnumerable<InventoryItem> toUpdate,
        IEnumerable<int> toDeleteIds)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Handle additions
            foreach (var item in toAdd)
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO destination_inventory (product_name, quantity, updated_at)
                    VALUES (@product_name, @quantity, @updated_at);
                ";
                insertCmd.Parameters.AddWithValue("@product_name", item.ProductName);
                insertCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                insertCmd.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o"));
                await insertCmd.ExecuteNonQueryAsync();
            }

            // Handle updates
            foreach (var item in toUpdate)
            {
                using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = @"
                    UPDATE destination_inventory
                    SET quantity = @quantity, updated_at = @updated_at
                    WHERE id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@id", item.Id);
                updateCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                updateCmd.Parameters.AddWithValue("@updated_at", item.UpdatedAt.ToString("o"));
                await updateCmd.ExecuteNonQueryAsync();
            }

            // Handle deletions
            foreach (var id in toDeleteIds)
            {
                using var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM destination_inventory WHERE id = @id;";
                deleteCmd.Parameters.AddWithValue("@id", id);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
