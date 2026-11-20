using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models.Dtos;

// DTO = Data Transfer Object. These describe the shapes we accept/return over
// HTTP. Note that NO DTO contains PasswordHash — that field never leaves the DB.

public record LoginRequest([EmailAddress] string Email, string Password);
public record RegisterRequest([EmailAddress] string Email, string Password, string Name);
public record LoginResponse(string Token, string Name, string Role, int UserId);

public record CreateProductRequest(string Name, string Sku, decimal Price, int Quantity, int LowStockAt);
public record UpdateProductRequest(string Name, decimal Price, int Quantity, int LowStockAt, bool IsActive);

public record OrderItemRequest(int ProductId, [Range(1, int.MaxValue)] int Quantity);
public record CreateOrderRequest([MinLength(1)] List<OrderItemRequest> Items);
public record UpdateOrderStatusRequest(OrderStatus Status);

public record StockMovementRequest(int ProductId, int Delta, string? Note);

public record CreateUserRequest(string Email, string Password, string Name, Role Role);
public record UpdateRoleRequest(Role Role);
public record UserResponse(int Id, string Email, string Name, string Role);
