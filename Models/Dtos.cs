using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models.Dtos;

// DTO = Data Transfer Object. These describe the shapes we accept/return over
// HTTP. Note that NO DTO contains PasswordHash — that field never leaves the DB.
// [ApiController] validates these automatically and returns 400 before the
// action method runs.

public record LoginRequest(
    [EmailAddress] string Email,
    [Required] string Password);

public record RegisterRequest(
    [EmailAddress] string Email,
    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Password needs 8+ chars with upper, lower and digit.")]
    string Password,
    [Required, MaxLength(100)] string Name);

public record LoginResponse(string Token, string Name, string Role, int UserId);

public record CreateProductRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(50)] string Sku,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Price,
    [Range(0, int.MaxValue)] int Quantity,
    [Range(0, int.MaxValue)] int LowStockAt);

public record UpdateProductRequest(
    [Required, MaxLength(100)] string Name,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Price,
    [Range(0, int.MaxValue)] int Quantity,
    [Range(0, int.MaxValue)] int LowStockAt,
    bool IsActive);

public record OrderItemRequest(int ProductId, [Range(1, int.MaxValue)] int Quantity);
public record CreateOrderRequest([MinLength(1)] List<OrderItemRequest> Items);
public record UpdateOrderStatusRequest(OrderStatus Status);

public record StockMovementRequest(
    int ProductId,
    [Range(-1000000, 1000000, ErrorMessage = "Delta must be a non-zero whole number.")]
    int Delta,
    [MaxLength(200)] string? Note)
{
    // Range allows 0, so reject it explicitly in the controller with a custom
    // check. Keeping simple rules in attributes keeps controllers readable.
}

public record CreateUserRequest(
    [EmailAddress] string Email,
    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Password needs 8+ chars with upper, lower and digit.")]
    string Password,
    [Required, MaxLength(100)] string Name,
    Role Role);

public record UpdateRoleRequest(Role Role);
public record UserResponse(int Id, string Email, string Name, string Role);
