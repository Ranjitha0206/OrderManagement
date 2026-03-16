using OrdersManagement.Models;

namespace OrdersManagement.Services;

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = new();
    private int _nextId = 1;

    public IEnumerable<Order> GetAll() => _orders.ToList();

    public Order? GetById(int id) =>
        _orders.FirstOrDefault(o => o.Id == id);

    public Order Create(Order order)
    {
        order.Id = _nextId++;
        order.CreatedAt = DateTime.UtcNow;
        _orders.Add(order);
        return order;
    }

    public Order? UpdateStatus(int id, OrderStatus status)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order is null) return null;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        return order;
    }

    public bool Delete(int id)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order is null) return false;
        _orders.Remove(order);
        return true;
    }

    public IEnumerable<Order> GetByStatus(OrderStatus status) =>
        _orders.Where(o => o.Status == status).ToList();
}
