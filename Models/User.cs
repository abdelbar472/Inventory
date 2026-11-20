using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InventoryApi.Models;

public class User
{
    public int Id { get; set; }

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    // NEVER send this to clients. It is a PBKDF2 hash, not the real password.
    public string PasswordHash { get; set; } = "";

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public Role Role { get; set; } = Role.Customer;

    // Navigation properties: EF fills these with related rows.
    [JsonIgnore] public List<Order> Orders { get; set; } = new();
    [JsonIgnore] public List<StockMovement> StockMovements { get; set; } = new();
}
