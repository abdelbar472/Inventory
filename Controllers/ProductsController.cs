using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // any logged-in user (CX, Employee, Manager) may browse
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    private bool IsStaff =>
        User.IsInRole(nameof(Role.Employee)) || User.IsInRole(nameof(Role.Manager));

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _db.Products.AsQueryable();

        // Customers only see active products; staff sees everything.
        if (!IsStaff) query = query.Where(p => p.IsActive);

        return Ok(await query.OrderBy(p => p.Id).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();
        if (!product.IsActive && !IsStaff) return NotFound();

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = nameof(Role.Manager))]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Sku = request.Sku,
            Price = request.Price,
            Quantity = request.Quantity,
            LowStockAt = request.LowStockAt
        };

        _db.Products.Add(product);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "SKU already exists" });
        }

        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = nameof(Role.Manager))]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.Name = request.Name;
        product.Price = request.Price;
        product.Quantity = request.Quantity;
        product.LowStockAt = request.LowStockAt;
        product.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(product);
    }

    // Soft delete: we hide the product instead of deleting it, so old orders
    // still point to a valid product (remember OnDelete.Restrict).
    [HttpPatch("{id}/active")]
    [Authorize(Roles = nameof(Role.Manager))]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.IsActive = active;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
