using OrdersManagement.Models;

namespace OrdersManagement.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;

    public OrderService(IOrderRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<Order> GetAllOrders() =>
        _repository.GetAll();

    public Order? GetOrderById(int id) =>
        _repository.GetById(id);

    public Result<Order> CreateOrder(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return Result<Order>.Fail("Customer name is required.");

        if (string.IsNullOrWhiteSpace(request.ProductName))
            return Result<Order>.Fail("Product name is required.");

        if (request.Quantity <= 0)
            return Result<Order>.Fail("Quantity must be greater than zero.");

        if (request.UnitPrice <= 0)
            return Result<Order>.Fail("Unit price must be greater than zero.");

        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            ProductName  = request.ProductName.Trim(),
            Quantity     = request.Quantity,
            UnitPrice    = request.UnitPrice,
            Status       = OrderStatus.Pending
        };

        var created = _repository.Create(order);
        return Result<Order>.Ok(created);
    }

    public Result<Order> UpdateOrderStatus(int id, OrderStatus status)
    {
        var existing = _repository.GetById(id);
        if (existing is null)
            return Result<Order>.Fail($"Order with ID {id} not found.");

        if (existing.Status == OrderStatus.Cancelled)
            return Result<Order>.Fail("Cannot update a cancelled order.");

        if (existing.Status == OrderStatus.Completed && status != OrderStatus.Cancelled)
            return Result<Order>.Fail("A completed order can only be cancelled.");

        var updated = _repository.UpdateStatus(id, status);
        return Result<Order>.Ok(updated!);
    }

    public Result<bool> DeleteOrder(int id)
    {
        var existing = _repository.GetById(id);
        if (existing is null)
            return Result<bool>.Fail($"Order with ID {id} not found.");

        if (existing.Status != OrderStatus.Pending)
            return Result<bool>.Fail("Only pending orders can be deleted.");

        _repository.Delete(id);
        return Result<bool>.Ok(true);
    }

    public IEnumerable<Order> GetOrdersByStatus(OrderStatus status) =>
        _repository.GetByStatus(status);
}
