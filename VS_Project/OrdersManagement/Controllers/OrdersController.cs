using Microsoft.AspNetCore.Mvc;
using OrdersManagement.Models;
using OrdersManagement.Services;

namespace OrdersManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // GET /api/orders
    [HttpGet]
    public IActionResult GetAll([FromQuery] OrderStatus? status)
    {
        var orders = status.HasValue
            ? _orderService.GetOrdersByStatus(status.Value)
            : _orderService.GetAllOrders();
        return Ok(orders);
    }

    // GET /api/orders/{id}
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var order = _orderService.GetOrderById(id);
        return order is null
            ? NotFound(new { message = $"Order {id} not found." })
            : Ok(order);
    }

    // POST /api/orders
    [HttpPost]
    public IActionResult Create([FromBody] CreateOrderRequest request)
    {
        var result = _orderService.CreateOrder(request);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    // PATCH /api/orders/{id}/status
    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = _orderService.UpdateOrderStatus(id, request.Status);
        if (!result.Success)
            return result.ErrorMessage!.Contains("not found")
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
        return Ok(result.Data);
    }

    // DELETE /api/orders/{id}
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var result = _orderService.DeleteOrder(id);
        if (!result.Success)
            return result.ErrorMessage!.Contains("not found")
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
        return NoContent();
    }
}
