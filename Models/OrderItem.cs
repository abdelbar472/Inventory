using System.Text.Json.Serialization;

namespace InventoryApi.Models;

// A snapshot line: UnitPrice is copied from Product.Price at order time,
// so changing the product's price later does NOT rewrite history.
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    [JsonIgnore] public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
