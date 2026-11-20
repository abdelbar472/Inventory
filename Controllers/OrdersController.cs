using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Models.Dtos;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrdersController(AppDbContext db) => _db = db;

    // Read the user id from the JWT claim we put in the token at login.
    private int MyId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsStaff =>
        User.IsInRole(nameof(Role.Employee)) || User.IsInRole(nameof(Role.Manager));

    // CUSTOMER: buy products. Stock is deducted and a history line written
    // in the same SaveChangesAsync (EF wraps it in one DB transaction).
    [HttpPost]
    [Authorize(Roles = nameof(Role.Customer))]
    public async Task<IActionResult> Place(CreateOrderRequest request)
    {
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

        var order = new Order { CustomerId = MyId };

        foreach (var item in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null || !product.IsActive)
                return BadRequest(new { message = $"Product {item.ProductId} is unavailable" });
            if (product.Quantity < item.Quantity)
                return BadRequest(new { message = $"Not enough stock for {product.Name}" });

            product.Quantity -= item.Quantity;                       // update stock
            order.Items.Add(new OrderItem                            // snapshot price
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
            order.Total += product.Price * item.Quantity;

            _db.StockMovements.Add(new StockMovement                  // audit trail
            {
                ProductId = product.Id,
                Delta = -item.Quantity,
                Note = "Order placed",
                CreatedByUserId = MyId
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        AppMetrics.OrdersPlaced.Inc();   // +1 to the Prometheus counter

        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    // Customers get only their own orders. Staff sees all orders.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _db.Orders.Include(o => o.Items).ThenInclude(i => i.Product).AsQueryable();
        if (!IsStaff) query = query.Where(o => o.CustomerId == MyId);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound();
        if (!IsStaff && order.CustomerId != MyId) return Forbid();

        return Ok(order);
    }

    // EMPLOYEE / MANAGER: move an order through its lifecycle.
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Employee,Manager")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        order.Status = request.Status;
        await _db.SaveChangesAsync();
        return Ok(order);
    }
}
