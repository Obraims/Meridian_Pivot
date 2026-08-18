namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly string _connectionString;

    public SyncHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> AddAsync(SyncHistory record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            INSERT INTO sync_history (synced_at, status, details)
            VALUES (@synced_at, @status, @details);
            SELECT last_insert_rowid();
        ";

        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@synced_at", record.SyncedAt.ToString("o"));
        command.Parameters.AddWithValue("@status", record.Status);
        command.Parameters.AddWithValue("@details", (object?)record.Details ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<SyncHistory>> GetAllAsync()
    {
        var list = new List<SyncHistory>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT id, synced_at, status, details FROM sync_history ORDER BY id DESC;";
        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new SyncHistory
            {
                Id = reader.GetInt32(0),
                SyncedAt = DateTime.Parse(reader.GetString(1)),
                Status = reader.GetString(2),
                Details = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return list;
    }
}
