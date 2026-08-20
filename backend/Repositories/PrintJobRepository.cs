namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class PrintJobRepository : IPrintJobRepository
{
    private readonly string _connectionString;

    public PrintJobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task CreateAsync(PrintJob job)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO print_jobs (id, attendee_id, status, created_at, completed_at)
            VALUES (@id, @attendeeId, @status, @createdAt, @completedAt);
        ";
        command.Parameters.AddWithValue("@id", job.Id);
        command.Parameters.AddWithValue("@attendeeId", job.AttendeeId);
        command.Parameters.AddWithValue("@status", job.Status);
        command.Parameters.AddWithValue("@createdAt", job.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@completedAt", (object?)job.CompletedAt?.ToString("o") ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<PrintJob?> GetByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, attendee_id, status, created_at, completed_at FROM print_jobs WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PrintJob
            {
                Id = reader.GetString(0),
                AttendeeId = reader.GetString(1),
                Status = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                CompletedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<PrintJob>> GetAllAsync()
    {
        var list = new List<PrintJob>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, attendee_id, status, created_at, completed_at FROM print_jobs ORDER BY created_at DESC;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PrintJob
            {
                Id = reader.GetString(0),
                AttendeeId = reader.GetString(1),
                Status = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                CompletedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
            });
        }

        return list;
    }

    public async Task UpdateStatusAsync(string id, string status, DateTime? completedAt)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE print_jobs
            SET status = @status, completed_at = @completedAt
            WHERE id = @id;
        ";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@completedAt", (object?)completedAt?.ToString("o") ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM print_jobs;";
        await command.ExecuteNonQueryAsync();
    }
}
