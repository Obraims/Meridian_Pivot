namespace StockSync.Repositories;

using Microsoft.Data.Sqlite;

public class WebhookEventRepository : IWebhookEventRepository
{
    private readonly string _connectionString;

    public WebhookEventRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> ExistsAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return false;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM webhook_events WHERE event_id = @eventId;";
        command.Parameters.AddWithValue("@eventId", eventId);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task RecordAsync(string eventId, string attendeeId, string status)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO webhook_events (event_id, attendee_id, status, received_at)
            VALUES (@eventId, @attendeeId, @status, @receivedAt);
        ";

        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@attendeeId", attendeeId ?? string.Empty);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@receivedAt", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM webhook_events;";
        await command.ExecuteNonQueryAsync();
    }
}
