namespace StockSync.Data;

using Microsoft.Data.Sqlite;
using StockSync.Models;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public DatabaseInitializer(string connectionString, string dbPath)
    {
        _connectionString = connectionString;
        _dbPath = dbPath;
    }

    public void Initialize()
    {
        var dbDir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS attendees (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    status TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS print_jobs (
                    id TEXT PRIMARY KEY,
                    attendee_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    completed_at TEXT
                );

                CREATE TABLE IF NOT EXISTS webhook_events (
                    event_id TEXT PRIMARY KEY,
                    attendee_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    received_at TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();
        }

        // Schema migration helpers
        EnsureColumnExists(connection, "webhook_events", "attendee_id", "TEXT NOT NULL DEFAULT ''");

        // Seed default attendees if attendees table is empty
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM attendees;";
            var count = Convert.ToInt64(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var seedAttendees = new (string Id, string Name)[]
                {
                    ("A001", "Alice"),
                    ("A002", "Brian"),
                    ("A003", "Carol")
                };

                using var transaction = connection.BeginTransaction();
                using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO attendees (id, name, status, updated_at)
                    VALUES (@id, @name, @status, @updated_at);
                ";

                var paramId = insertCmd.Parameters.Add("@id", SqliteType.Text);
                var paramName = insertCmd.Parameters.Add("@name", SqliteType.Text);
                var paramStatus = insertCmd.Parameters.Add("@status", SqliteType.Text);
                var paramUpdated = insertCmd.Parameters.Add("@updated_at", SqliteType.Text);

                foreach (var attendee in seedAttendees)
                {
                    paramId.Value = attendee.Id;
                    paramName.Value = attendee.Name;
                    paramStatus.Value = AttendeeStatus.NotCheckedIn;
                    paramUpdated.Value = now;
                    insertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }
    }

    private static void EnsureColumnExists(SqliteConnection connection, string table, string column, string columnDef)
    {
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = pragmaCmd.ExecuteReader();
        var exists = false;
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        reader.Close();

        if (!exists)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDef};";
            alterCmd.ExecuteNonQuery();
        }
    }
}
