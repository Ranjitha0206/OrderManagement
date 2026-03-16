using OrdersManagement.Models;
using OrdersManagement.Services;

namespace OrdersManagement.Data;

public class SqliteOrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public SqliteOrderRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Data Source=orders.db";
    }

    private SqliteConnection Connect()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public IEnumerable<Order> GetAll()
    {
        using var conn = Connect();
        var rows = conn.Query("SELECT * FROM Orders ORDER BY CreatedAt DESC");
        return rows.Select(MapRow).ToList();
    }

    public Order? GetById(int id)
    {
        using var conn = Connect();
        var rows = conn.Query(
            "SELECT * FROM Orders WHERE Id = @Id",
            new Dictionary<string, object?> { ["@Id"] = id });
        return rows.Select(MapRow).FirstOrDefault();
    }

    public Order Create(Order order)
    {
        using var conn = Connect();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Orders (CustomerName, ProductName, Quantity, UnitPrice, Status, CreatedAt, UpdatedAt)
            VALUES (@CustomerName, @ProductName, @Quantity, @UnitPrice, @Status, @CreatedAt, @UpdatedAt);";

        cmd.Parameters["@CustomerName"] = order.CustomerName;
        cmd.Parameters["@ProductName"]  = order.ProductName;
        cmd.Parameters["@Quantity"]     = order.Quantity;
        cmd.Parameters["@UnitPrice"]    = order.UnitPrice;
        cmd.Parameters["@Status"]       = order.Status.ToString();
        cmd.Parameters["@CreatedAt"]    = order.CreatedAt.ToString("o");
        cmd.Parameters["@UpdatedAt"]    = order.UpdatedAt?.ToString("o");

        cmd.ExecuteNonQuery();
        order.Id = (int)cmd.LastInsertRowId();
        return order;
    }

    public Order? UpdateStatus(int id, OrderStatus status)
    {
        using var conn = Connect();
        conn.Execute(
            "UPDATE Orders SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
            new Dictionary<string, object?>
            {
                ["@Status"]    = status.ToString(),
                ["@UpdatedAt"] = DateTime.UtcNow.ToString("o"),
                ["@Id"]        = id
            });
        return GetById(id);
    }

    public bool Delete(int id)
    {
        using var conn = Connect();
        int affected = conn.Execute(
            "DELETE FROM Orders WHERE Id = @Id",
            new Dictionary<string, object?> { ["@Id"] = id });
        return affected > 0;
    }

    public IEnumerable<Order> GetByStatus(OrderStatus status)
    {
        using var conn = Connect();
        var rows = conn.Query(
            "SELECT * FROM Orders WHERE Status = @Status ORDER BY CreatedAt DESC",
            new Dictionary<string, object?> { ["@Status"] = status.ToString() });
        return rows.Select(MapRow).ToList();
    }

    private static Order MapRow(Dictionary<string, object?> row) => new()
    {
        Id           = (int)(long)row["Id"]!,
        CustomerName = (string)row["CustomerName"]!,
        ProductName  = (string)row["ProductName"]!,
        Quantity     = (int)(long)row["Quantity"]!,
        UnitPrice    = (decimal)(double)row["UnitPrice"]!,
        Status       = Enum.Parse<OrderStatus>((string)row["Status"]!),
        CreatedAt    = DateTime.Parse((string)row["CreatedAt"]!),
        UpdatedAt    = row["UpdatedAt"] is string s ? DateTime.Parse(s) : null
    };
}
