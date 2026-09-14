using B2BShop.Infrastructure.Services;

namespace B2BShop.Worker;

/// <summary>
/// Background worker that polls for pending order transfers and dispatches them.
/// Polling interval is fixed at 30 seconds — not configurable (deliberate simplification).
/// </summary>
public class OrderTransferWorker : BackgroundService
{
    private readonly ILogger<OrderTransferWorker> _logger;
    private readonly IServiceProvider _services;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public OrderTransferWorker(ILogger<OrderTransferWorker> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderTransferWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var transferService = scope.ServiceProvider.GetRequiredService<OrderTransferService>();
                await transferService.ProcessPendingTransfersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful stop
                break;
            }
            catch (Exception ex)
            {
                // Log but don't crash the worker — retry next cycle
                _logger.LogError(ex, "Unhandled error in transfer cycle");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("OrderTransferWorker stopped");
    }
}
