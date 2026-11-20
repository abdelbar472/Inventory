using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Models.Dtos;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(Role.Manager))]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    // Project to a DTO so PasswordHash is never serialized into a response.
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Users
            .Select(u => new UserResponse(u.Id, u.Email, u.Name, u.Role.ToString()))
            .ToListAsync());

    // Manager creates staff accounts (customers normally self-register).
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email already used" });

        var user = new User
        {
            Email = request.Email,
            PasswordHash = PasswordHelper.Hash(request.Password),
            Name = request.Name,
            Role = request.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new UserResponse(user.Id, user.Email, user.Name, user.Role.ToString()));
    }

    [HttpPatch("{id}/role")]
    public async Task<IActionResult> ChangeRole(int id, UpdateRoleRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.Role = request.Role;
        await _db.SaveChangesAsync();

        return Ok(new UserResponse(user.Id, user.Email, user.Name, user.Role.ToString()));
    }
}
