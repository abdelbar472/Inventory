using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InventoryApi.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, MaxLength(50)]
    public string Sku { get; set; } = "";

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, int.MaxValue)]
    public int LowStockAt { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    [JsonIgnore] public List<StockMovement> Movements { get; set; } = new();
    [JsonIgnore] public List<OrderItem> OrderItems { get; set; } = new();
}
