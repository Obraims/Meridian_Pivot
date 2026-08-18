namespace StockSync.Data;

using Microsoft.Data.Sqlite;

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
                CREATE TABLE IF NOT EXISTS source_inventory (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_name TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS destination_inventory (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_name TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sync_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    synced_at TEXT NOT NULL,
                    status TEXT NOT NULL,
                    details TEXT
                );
            ";
            command.ExecuteNonQuery();
        }

        // Seed source_inventory if empty
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM source_inventory;";
            var count = Convert.ToInt64(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                var now = DateTime.UtcNow.ToString("o");
                var seedData = new (string Name, int Quantity)[]
                {
                    ("Blue Shoes", 20),
                    ("Black Shirt", 15),
                    ("Red Cap", 8)
                };

                using var transaction = connection.BeginTransaction();
                using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO source_inventory (product_name, quantity, updated_at)
                    VALUES (@product_name, @quantity, @updated_at);
                ";

                var paramName = insertCmd.Parameters.Add("@product_name", SqliteType.Text);
                var paramQty = insertCmd.Parameters.Add("@quantity", SqliteType.Integer);
                var paramUpdated = insertCmd.Parameters.Add("@updated_at", SqliteType.Text);

                foreach (var item in seedData)
                {
                    paramName.Value = item.Name;
                    paramQty.Value = item.Quantity;
                    paramUpdated.Value = now;
                    insertCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }
    }
}
