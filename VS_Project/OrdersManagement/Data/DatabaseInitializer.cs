namespace OrdersManagement.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IConfiguration config, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Data Source=orders.db";
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing database...");
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS Orders (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerName TEXT NOT NULL,
                ProductName  TEXT NOT NULL,
                Quantity     INTEGER NOT NULL,
                UnitPrice    REAL NOT NULL,
                Status       TEXT NOT NULL DEFAULT 'Pending',
                CreatedAt    TEXT NOT NULL,
                UpdatedAt    TEXT
            );
        ");

        _logger.LogInformation("Database initialized.");
    }
}
