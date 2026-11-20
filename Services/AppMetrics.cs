using Prometheus;

namespace InventoryApi.Services;

// Custom business metrics. Prometheus reads these from /metrics.
public static class AppMetrics
{
    // Counter = only goes up. Perfect for "how many orders ever".
    public static readonly Counter OrdersPlaced = Metrics.CreateCounter(
        "store_orders_total",
        "Total number of orders placed.");

    // Gauge = goes up and down. Perfect for "current stock alerts".
    public static readonly Gauge LowStockProducts = Metrics.CreateGauge(
        "store_low_stock_products",
        "Number of active products below their low-stock threshold.");
}
