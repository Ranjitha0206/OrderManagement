using OrdersManagement.Models;

namespace OrdersManagement.Services;

public interface IOrderService
{
    IEnumerable<Order> GetAllOrders();
    Order? GetOrderById(int id);
    Result<Order> CreateOrder(CreateOrderRequest request);
    Result<Order> UpdateOrderStatus(int id, OrderStatus status);
    Result<bool> DeleteOrder(int id);
    IEnumerable<Order> GetOrdersByStatus(OrderStatus status);
}

public class Result<T>
{
    public bool Success { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static Result<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static Result<T> Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
