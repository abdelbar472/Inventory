using InventoryApi.Models;
using InventoryApi.Services;

namespace InventoryApi.Data;

// Called on startup after EnsureCreated. Only seeds when the Users table is empty.
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        db.Users.AddRange(
            new User { Email = "cx@shop.test", PasswordHash = PasswordHelper.Hash("Cx123!"), Name = "Cassy Customer", Role = Role.Customer },
            new User { Email = "emp@shop.test", PasswordHash = PasswordHelper.Hash("Emp123!"), Name = "Evan Employee", Role = Role.Employee },
            new User { Email = "mgr@shop.test", PasswordHash = PasswordHelper.Hash("Mgr123!"), Name = "Mona Manager", Role = Role.Manager }
        );

        db.Products.AddRange(
            new Product { Name = "Notebook", Sku = "NB-001", Price = 4.50m, Quantity = 40, LowStockAt = 5 },
            new Product { Name = "Pen pack", Sku = "PN-002", Price = 2.00m, Quantity = 3, LowStockAt = 5 },
            new Product { Name = "Desk lamp", Sku = "DL-003", Price = 24.99m, Quantity = 12, LowStockAt = 3 }
        );

        db.SaveChanges();

        // One sample order so staff endpoints have data to show immediately.
        var notebook = db.Products.Single(p => p.Sku == "NB-001");
        notebook.Quantity -= 1;
        db.Orders.Add(new Order
        {
            CustomerId = db.Users.Single(u => u.Role == Role.Customer).Id,
            Status = OrderStatus.Pending,
            Total = notebook.Price,
            Items = new List<OrderItem>
            {
                new() { ProductId = notebook.Id, Quantity = 1, UnitPrice = notebook.Price }
            }
        });
        db.StockMovements.Add(new StockMovement
        {
            ProductId = notebook.Id,
            Delta = -1,
            Note = "Seed order",
            CreatedByUserId = db.Users.Single(u => u.Role == Role.Customer).Id
        });
        db.SaveChanges();
    }
}
