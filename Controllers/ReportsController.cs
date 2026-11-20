using InventoryApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Models;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(Role.Manager))]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock() =>
        Ok(await _db.Products
            .Where(p => p.IsActive && p.Quantity < p.LowStockAt)
            .Select(p => new { p.Id, p.Name, p.Sku, p.Quantity, p.LowStockAt })
            .ToListAsync());

    // GroupBy + Sum is translated into a SQL GROUP BY.
    [HttpGet("sales")]
    public async Task<IActionResult> Sales() =>
        Ok(await _db.Orders
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status = g.Key.ToString(),
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Total)
            })
            .ToListAsync());
}
