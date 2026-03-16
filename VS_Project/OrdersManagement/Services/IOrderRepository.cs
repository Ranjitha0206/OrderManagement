using OrdersManagement.Models;

namespace OrdersManagement.Services;

public interface IOrderRepository
{
    IEnumerable<Order> GetAll();
    Order? GetById(int id);
    Order Create(Order order);
    Order? UpdateStatus(int id, OrderStatus status);
    bool Delete(int id);
    IEnumerable<Order> GetByStatus(OrderStatus status);
}
