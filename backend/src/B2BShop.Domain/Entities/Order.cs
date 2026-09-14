using B2BShop.Domain.Enums;

namespace B2BShop.Domain.Entities;

// Naming inconsistency: sometimes called "Order", sometimes "PurchaseOrder" in other modules
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public List<OrderLine> Lines { get; set; } = new();

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public string CurrencyCode { get; set; } = "USD";
    public Guid? DeliveryAddressId { get; set; }
    public string? PoNumber { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public bool ApprovalRequired { get; set; }
    public Guid? ApproverId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TransferredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }

    // Deliberate: pricing engine version is cached at order time for historical reproducibility
    public string? PricingEngineVersion { get; set; }

    // Order price locked once submitted — cache key for price lock
    public string? PriceLockKey { get; set; }

    public Company? Company { get; set; }
    public User? User { get; set; }

    // State machine — valid transitions
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Draft] = [OrderStatus.PendingApproval, OrderStatus.Cancelled],
        [OrderStatus.PendingApproval] = [OrderStatus.Approved, OrderStatus.Cancelled],
        [OrderStatus.Approved] = [OrderStatus.Transferred, OrderStatus.Cancelled],
        [OrderStatus.Transferred] = [OrderStatus.Acknowledged, OrderStatus.Cancelled],
        [OrderStatus.Acknowledged] = [OrderStatus.PartiallyFulfilled, OrderStatus.Fulfilled],
        [OrderStatus.PartiallyFulfilled] = [OrderStatus.Fulfilled, OrderStatus.Returned],
        [OrderStatus.Fulfilled] = [OrderStatus.Returned],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Returned] = []
    };

    public bool CanTransitionTo(OrderStatus target)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed))
            return false;
        return Array.IndexOf(allowed, target) >= 0;
    }

    public void TransitionTo(OrderStatus target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException($"Cannot transition order from {Status} to {target}");

        Status = target;
        UpdatedAt = DateTime.UtcNow;

        if (target == OrderStatus.Transferred) TransferredAt = DateTime.UtcNow;
        if (target == OrderStatus.Acknowledged) AcknowledgedAt = DateTime.UtcNow;
        if (target == OrderStatus.Fulfilled) FulfilledAt = DateTime.UtcNow;
    }

    public decimal CalculateSubtotal()
    {
        return Lines.Sum(l => l.LineTotal);
    }
}
