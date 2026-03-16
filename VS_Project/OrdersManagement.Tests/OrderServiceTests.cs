using OrdersManagement.Models;
using OrdersManagement.Services;

namespace OrdersManagement.Tests;

public class OrderServiceTests
{
    private OrderService CreateService() =>
        new OrderService(new InMemoryOrderRepository());

    private CreateOrderRequest ValidRequest() => new()
    {
        CustomerName = "Acme Corp",
        ProductName  = "Double-Sided PCB",
        Quantity     = 10,
        UnitPrice    = 75.50m
    };

    // ── CreateOrder ──────────────────────────────────────────────

    [Fact]
    public void CreateOrder_ValidRequest_ShouldSucceedAndReturnOrder()
    {
        var service = CreateService();
        var result  = service.CreateOrder(ValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Acme Corp", result.Data.CustomerName);
        Assert.Equal(OrderStatus.Pending, result.Data.Status);
    }

    [Fact]
    public void CreateOrder_TotalPrice_ShouldBeQuantityTimesUnitPrice()
    {
        var service = CreateService();
        var result  = service.CreateOrder(ValidRequest()); // Qty=10, Price=75.50

        Assert.Equal(755.00m, result.Data!.TotalPrice);
    }

    [Theory]
    [InlineData("",    "PCB", 1,  10.0)]   // empty customer name
    [InlineData("  ",  "PCB", 1,  10.0)]   // whitespace customer name
    [InlineData("Acme", "",   1,  10.0)]   // empty product name
    [InlineData("Acme", "PCB", 0, 10.0)]   // zero quantity
    [InlineData("Acme", "PCB", -1, 10.0)]  // negative quantity
    [InlineData("Acme", "PCB", 1,  0.0)]   // zero price
    [InlineData("Acme", "PCB", 1, -5.0)]   // negative price
    public void CreateOrder_InvalidRequest_ShouldFail(
        string customer, string product, int qty, double price)
    {
        var service = CreateService();
        var request = new CreateOrderRequest
        {
            CustomerName = customer,
            ProductName  = product,
            Quantity     = qty,
            UnitPrice    = (decimal)price
        };

        var result = service.CreateOrder(request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    // ── UpdateOrderStatus ────────────────────────────────────────

    [Fact]
    public void UpdateOrderStatus_ValidTransition_ShouldSucceed()
    {
        var service = CreateService();
        var order   = service.CreateOrder(ValidRequest()).Data!;

        var result = service.UpdateOrderStatus(order.Id, OrderStatus.InProgress);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.InProgress, result.Data!.Status);
    }

    [Fact]
    public void UpdateOrderStatus_NonExistentOrder_ShouldFail()
    {
        var service = CreateService();
        var result  = service.UpdateOrderStatus(999, OrderStatus.InProgress);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateOrderStatus_CancelledOrder_ShouldFail()
    {
        var service = CreateService();
        var order   = service.CreateOrder(ValidRequest()).Data!;
        service.UpdateOrderStatus(order.Id, OrderStatus.Cancelled);

        var result = service.UpdateOrderStatus(order.Id, OrderStatus.InProgress);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateOrderStatus_CompletedOrder_CanOnlyBeCancelled()
    {
        var service = CreateService();
        var order   = service.CreateOrder(ValidRequest()).Data!;
        service.UpdateOrderStatus(order.Id, OrderStatus.Completed);

        // Any status other than Cancelled should fail
        var failResult = service.UpdateOrderStatus(order.Id, OrderStatus.InProgress);
        Assert.False(failResult.Success);

        // Cancelling a completed order should succeed
        var order2 = service.CreateOrder(ValidRequest()).Data!;
        service.UpdateOrderStatus(order2.Id, OrderStatus.Completed);
        var cancelResult = service.UpdateOrderStatus(order2.Id, OrderStatus.Cancelled);
        Assert.True(cancelResult.Success);
    }

    // ── DeleteOrder ──────────────────────────────────────────────

    [Fact]
    public void DeleteOrder_PendingOrder_ShouldSucceed()
    {
        var service = CreateService();
        var order   = service.CreateOrder(ValidRequest()).Data!;

        var result = service.DeleteOrder(order.Id);

        Assert.True(result.Success);
        Assert.Null(service.GetOrderById(order.Id));
    }

    [Fact]
    public void DeleteOrder_NonPendingOrder_ShouldFail()
    {
        var service = CreateService();
        var order   = service.CreateOrder(ValidRequest()).Data!;
        service.UpdateOrderStatus(order.Id, OrderStatus.InProgress);

        var result = service.DeleteOrder(order.Id);

        Assert.False(result.Success);
        Assert.Contains("pending", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeleteOrder_NonExistentOrder_ShouldFail()
    {
        var service = CreateService();
        var result  = service.DeleteOrder(999);

        Assert.False(result.Success);
    }

    // ── GetOrdersByStatus ────────────────────────────────────────

    [Fact]
    public void GetOrdersByStatus_ShouldReturnFilteredOrders()
    {
        var service = CreateService();
        service.CreateOrder(ValidRequest());
        var o2 = service.CreateOrder(ValidRequest()).Data!;
        service.UpdateOrderStatus(o2.Id, OrderStatus.InProgress);

        var pending    = service.GetOrdersByStatus(OrderStatus.Pending).ToList();
        var inProgress = service.GetOrdersByStatus(OrderStatus.InProgress).ToList();

        Assert.Single(pending);
        Assert.Single(inProgress);
    }
}
