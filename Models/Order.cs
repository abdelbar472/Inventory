using System.Text.Json.Serialization;

namespace InventoryApi.Models;

public class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    [JsonIgnore] public User Customer { get; set; } = null!;   // EF sets this when loading with Include

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}
