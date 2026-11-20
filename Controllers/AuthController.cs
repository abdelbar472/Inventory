using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Models.Dtos;
using InventoryApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]   // 10 login/register attempts per IP per minute
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    // ASP.NET injects the registered DbContext and config automatically (DI).
    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !PasswordHelper.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        return Ok(new LoginResponse(CreateToken(user), user.Name, user.Role.ToString(), user.Id));
    }

    // Self-service signup always creates a Customer. Managers create staff via /api/users.
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email already used" });

        var user = new User
        {
            Email = request.Email,
            PasswordHash = PasswordHelper.Hash(request.Password),
            Name = request.Name,
            Role = Role.Customer
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(CreateToken(user), user.Name, user.Role.ToString(), user.Id));
    }

    private string CreateToken(User user)
    {
        // Claims = facts baked into the signed token. The server trusts them
        // on later requests because only we hold the signing key.
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())   // [Authorize(Roles="...")] reads this
        ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpiryMinutes"]!)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
