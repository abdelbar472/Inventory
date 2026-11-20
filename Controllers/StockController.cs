using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Employee,Manager")]
public class StockController : ControllerBase
{
    private readonly AppDbContext _db;
    public StockController(AppDbContext db) => _db = db;

    private int MyId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Positive Delta = stock in, negative Delta = stock out.
    [HttpPost("movement")]
    public async Task<IActionResult> Move(StockMovementRequest request)
    {
        if (request.Delta == 0)
            return BadRequest(new { message = "Delta must not be zero" });

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var product = await _db.Products.FindAsync(request.ProductId);
        if (product is null) return NotFound();

        // Same atomic pattern as orders: never let stock go negative,
        // even if two employees adjust the same product at once.
        var updated = await _db.Products
            .Where(p => p.Id == request.ProductId && p.Quantity + request.Delta >= 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Quantity, p => p.Quantity + request.Delta));

        if (updated == 0)
            return BadRequest(new { message = "Not enough stock" });

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Delta = request.Delta,
            Note = request.Note,
            CreatedByUserId = MyId
        };
        _db.StockMovements.Add(movement);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(movement);
    }

    [HttpGet("movements")]
    public async Task<IActionResult> History([FromQuery] int? productId)
    {
        var query = _db.StockMovements.AsQueryable();
        if (productId.HasValue) query = query.Where(m => m.ProductId == productId.Value);

        return Ok(await query.OrderByDescending(m => m.CreatedAt).ToListAsync());
    }
}
