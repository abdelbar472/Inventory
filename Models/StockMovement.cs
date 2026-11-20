using System.Text.Json.Serialization;

namespace InventoryApi.Models;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [JsonIgnore] public Product? Product { get; set; }

    public int Delta { get; set; }   // + in, - out
    public string? Note { get; set; }

    // Who caused this movement (customer order or employee adjustment)
    public int CreatedByUserId { get; set; }
    [JsonIgnore] public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
