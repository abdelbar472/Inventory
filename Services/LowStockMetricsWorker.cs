using InventoryApi.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Services;

// A BackgroundService runs alongside the web server. Every 10 seconds it
// asks the database for the current low-stock count and updates the gauge.
public class LowStockMetricsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public LowStockMetricsWorker(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        await UpdateAsync(stoppingToken);   // don't wait 10s for the first value

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await UpdateAsync(stoppingToken);
    }

    private async Task UpdateAsync(CancellationToken token)
    {
        // Services injected into a hosted service must be resolved from a
        // scope — DbContext is scoped, not singleton.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Products.CountAsync(
            p => p.IsActive && p.Quantity < p.LowStockAt, token);

        AppMetrics.LowStockProducts.Set(count);
    }
}
