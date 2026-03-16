using OrdersManagement.Models;
using OrdersManagement.Services;

namespace OrdersManagement.Tests;

public class InMemoryOrderRepositoryTests
{
    private InMemoryOrderRepository CreateRepository() => new();

    private Order MakeSampleOrder() => new()
    {
        CustomerName = "Test Customer",
        ProductName  = "PCB Board v2",
        Quantity     = 5,
        UnitPrice    = 120.00m
    };

    [Fact]
    public void Create_ShouldAssignIncrementalId()
    {
        var repo   = CreateRepository();
        var order1 = repo.Create(MakeSampleOrder());
        var order2 = repo.Create(MakeSampleOrder());

        Assert.Equal(1, order1.Id);
        Assert.Equal(2, order2.Id);
    }

    [Fact]
    public void GetAll_ShouldReturnAllCreatedOrders()
    {
        var repo = CreateRepository();
        repo.Create(MakeSampleOrder());
        repo.Create(MakeSampleOrder());

        var all = repo.GetAll().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetById_ExistingId_ShouldReturnOrder()
    {
        var repo    = CreateRepository();
        var created = repo.Create(MakeSampleOrder());

        var found = repo.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public void GetById_NonExistentId_ShouldReturnNull()
    {
        var repo  = CreateRepository();
        var found = repo.GetById(999);

        Assert.Null(found);
    }

    [Fact]
    public void UpdateStatus_ShouldChangeStatusAndSetUpdatedAt()
    {
        var repo    = CreateRepository();
        var created = repo.Create(MakeSampleOrder());

        var updated = repo.UpdateStatus(created.Id, OrderStatus.InProgress);

        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.InProgress, updated.Status);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public void UpdateStatus_NonExistentId_ShouldReturnNull()
    {
        var repo   = CreateRepository();
        var result = repo.UpdateStatus(999, OrderStatus.InProgress);

        Assert.Null(result);
    }

    [Fact]
    public void Delete_ExistingOrder_ShouldReturnTrueAndRemoveIt()
    {
        var repo    = CreateRepository();
        var created = repo.Create(MakeSampleOrder());

        var deleted = repo.Delete(created.Id);
        var found   = repo.GetById(created.Id);

        Assert.True(deleted);
        Assert.Null(found);
    }

    [Fact]
    public void Delete_NonExistentId_ShouldReturnFalse()
    {
        var repo   = CreateRepository();
        var result = repo.Delete(999);

        Assert.False(result);
    }

    [Fact]
    public void GetByStatus_ShouldReturnOnlyMatchingOrders()
    {
        var repo   = CreateRepository();
        var order1 = repo.Create(MakeSampleOrder());
        repo.Create(MakeSampleOrder());
        repo.UpdateStatus(order1.Id, OrderStatus.Completed);

        var completed = repo.GetByStatus(OrderStatus.Completed).ToList();
        var pending   = repo.GetByStatus(OrderStatus.Pending).ToList();

        Assert.Single(completed);
        Assert.Single(pending);
    }
}
