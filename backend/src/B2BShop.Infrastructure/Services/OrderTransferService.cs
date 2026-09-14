using B2BShop.Application.Worker;
using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2BShop.Infrastructure.Services;

/// <summary>
/// Processes approved orders and transfers them to external systems via configured transports.
/// Transport, format, retry and scheduling are all driven by DB config per company.
/// </summary>
public class OrderTransferService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderTransferService> _logger;

    public OrderTransferService(AppDbContext db, ILogger<OrderTransferService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProcessPendingTransfersAsync(CancellationToken ct = default)
    {
        // Find approved orders that haven't been transferred yet
        var pendingOrders = await _db.Orders
            .Include(o => o.Lines)
            .Where(o => o.Status == OrderStatus.Approved && o.TransferredAt == null)
            .Take(50) // Process in batches
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} orders pending transfer", pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            if (ct.IsCancellationRequested) break;
            await TransferOrderAsync(order, ct);
        }
    }

    private async Task TransferOrderAsync(Order order, CancellationToken ct)
    {
        var integrationConfig = await _db.IntegrationConfigs
            .FirstOrDefaultAsync(i => i.CompanyId == order.CompanyId && i.IsActive, ct);

        if (integrationConfig == null)
        {
            _logger.LogWarning("No active integration config for company {CompanyId}, order {OrderNumber}",
                order.CompanyId, order.OrderNumber);
            return;
        }

        // Determine current attempt number
        var previousAttempts = await _db.TransferLogs
            .CountAsync(l => l.OrderId == order.Id, ct);
        var attemptNumber = previousAttempts + 1;

        if (OrderTransferPolicy.ShouldDeadLetter(attemptNumber))
        {
            _logger.LogError("Order {OrderNumber} exceeded max retry attempts, moving to dead letter",
                order.OrderNumber);

            await LogTransferAttemptAsync(order.Id, integrationConfig.Id, attemptNumber,
                "dead_letter", "Exceeded maximum retry attempts", null, ct);
            return;
        }

        // Check if we should delay this retry
        if (previousAttempts > 0)
        {
            var lastAttempt = await _db.TransferLogs
                .Where(l => l.OrderId == order.Id)
                .OrderByDescending(l => l.AttemptedAt)
                .FirstOrDefaultAsync(ct);

            if (lastAttempt != null)
            {
                var nextRetryAt = lastAttempt.AttemptedAt + OrderTransferPolicy.GetRetryDelay(previousAttempts);
                if (DateTime.UtcNow < nextRetryAt)
                {
                    _logger.LogDebug("Order {OrderNumber} retry not yet due (next at {NextRetry})",
                        order.OrderNumber, nextRetryAt);
                    return;
                }
            }
        }

        // Serialize the order
        var payload = integrationConfig.Format switch {
            "xml" => OrderSerializer.ToXml(order),
            "csv" => OrderSerializer.ToCsv(order),
            "edi" => OrderSerializer.ToEdi(order),
            _ => OrderSerializer.ToJson(order)  // Default JSON
        };

        // Transmit via configured transport
        var (success, errorMessage, response) = await TransmitAsync(integrationConfig, payload, order, ct);

        await LogTransferAttemptAsync(order.Id, integrationConfig.Id, attemptNumber,
            success ? "success" : "failed", errorMessage, response, ct);

        if (success)
        {
            order.TransitionTo(OrderStatus.Transferred);
            integrationConfig.LastTransferAt = DateTime.UtcNow;
            integrationConfig.LastTransferStatus = "success";
            integrationConfig.LastErrorMessage = null;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Order {OrderNumber} transferred successfully via {Transport}",
                order.OrderNumber, integrationConfig.Transport);
        }
        else
        {
            integrationConfig.LastTransferStatus = "failed";
            integrationConfig.LastErrorMessage = errorMessage;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning("Order {OrderNumber} transfer failed (attempt {Attempt}): {Error}",
                order.OrderNumber, attemptNumber, errorMessage);
        }
    }

    private async Task<(bool success, string? error, string? response)> TransmitAsync(
        IntegrationConfig config, string payload, Order order, CancellationToken ct)
    {
        // Simulate failure if configured
        if (config.SimulatedFailurePercent.HasValue && config.SimulatedFailurePercent > 0)
        {
            var roll = Random.Shared.Next(0, 100);
            if (roll < config.SimulatedFailurePercent.Value)
            {
                return (false, $"Simulated failure (configured at {config.SimulatedFailurePercent}%)", null);
            }
        }

        // Simulate delay if configured
        if (config.SimulatedDelayMs.HasValue && config.SimulatedDelayMs > 0)
        {
            await Task.Delay(config.SimulatedDelayMs.Value, ct);
        }

        return config.Transport switch {
            "http" => await HttpTransmitStubAsync(config, payload, order, ct),
            "sftp" => await SftpTransmitStubAsync(config, payload, order),
            "fileshare" => await FileShareTransmitStubAsync(config, payload, order),
            "email" => await EmailTransmitStubAsync(config, payload, order),
            _ => (false, $"Unknown transport: {config.Transport}", null)
        };
    }

    private static async Task<(bool, string?, string?)> HttpTransmitStubAsync(
        IntegrationConfig config, string payload, Order order, CancellationToken ct)
    {
        // Stub: log that we would POST to the configured endpoint
        // In real implementation, would parse config.TransportConfigJson for endpoint URL
        await Task.CompletedTask;
        var ackId = $"ACK-{order.OrderNumber}-{DateTime.UtcNow.Ticks}";
        return (true, null, $"{{\"status\":\"accepted\",\"acknowledgementId\":\"{ackId}\"}}");
    }

    private static async Task<(bool, string?, string?)> SftpTransmitStubAsync(
        IntegrationConfig config, string payload, Order order)
    {
        // Stub: pretend we uploaded the file to SFTP
        await Task.CompletedTask;
        return (true, null, $"File uploaded: orders/{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.{config.Format}");
    }

    private static async Task<(bool, string?, string?)> FileShareTransmitStubAsync(
        IntegrationConfig config, string payload, Order order)
    {
        // Stub: write to local temp directory to simulate file share drop
        await Task.CompletedTask;
        var filename = $"order_{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.{config.Format}";
        var path = Path.Combine(Path.GetTempPath(), "b2bshop_transfers", filename);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, payload);
            return (true, null, $"Written to {path}");
        }
        catch (Exception ex)
        {
            return (false, $"File write failed: {ex.Message}", null);
        }
    }

    private static async Task<(bool, string?, string?)> EmailTransmitStubAsync(
        IntegrationConfig config, string payload, Order order)
    {
        // Stub: log the email that would be sent
        await Task.CompletedTask;
        return (true, null, $"Email stub: would send order {order.OrderNumber} to configured recipient");
    }

    private async Task LogTransferAttemptAsync(
        Guid orderId, Guid integrationConfigId, int attemptNumber,
        string status, string? errorMessage, string? response, CancellationToken ct)
    {
        _db.TransferLogs.Add(new OrderTransferLog
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            IntegrationConfigId = integrationConfigId,
            AttemptNumber = attemptNumber,
            AttemptedAt = DateTime.UtcNow,
            Status = status,
            ErrorMessage = errorMessage,
            ResponsePayload = response
        });
        await _db.SaveChangesAsync(ct);
    }
}
