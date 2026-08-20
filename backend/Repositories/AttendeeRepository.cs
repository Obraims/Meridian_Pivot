namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class AttendeeRepository : IAttendeeRepository
{
    private readonly string _connectionString;

    public AttendeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Attendee?> GetByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, status, updated_at FROM attendees WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Attendee
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Status = reader.GetString(2),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<Attendee>> GetAllAsync()
    {
        var list = new List<Attendee>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, status, updated_at FROM attendees ORDER BY id ASC;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Attendee
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Status = reader.GetString(2),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return list;
    }

    public async Task UpdateStatusAsync(string id, string status, DateTime updatedAt)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE attendees
            SET status = @status, updated_at = @updatedAt
            WHERE id = @id;
        ";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@updatedAt", updatedAt.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE attendees
            SET status = @status, updated_at = @updatedAt;
        ";
        command.Parameters.AddWithValue("@status", AttendeeStatus.NotCheckedIn);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }
}
