namespace B2BShop.Application.Orders;

// Request/response models live in Application layer
// Actual OrderService implementation lives in B2BShop.Infrastructure.Services
// to avoid circular dependency (Application <- Infrastructure)

public class CreateOrderRequest
{
    public List<OrderLineRequest> Lines { get; set; } = new();
    public Guid? DeliveryAddressId { get; set; }
    public string? PoNumber { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

public class OrderLineRequest
{
    public Guid ArticleId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
