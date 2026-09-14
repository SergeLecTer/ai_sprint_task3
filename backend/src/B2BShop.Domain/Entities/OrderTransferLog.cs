namespace B2BShop.Domain.Entities;

public class OrderTransferLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid IntegrationConfigId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // "success" | "failed" | "pending" | "dead_letter"
    public string? ErrorMessage { get; set; }
    public string? ResponsePayload { get; set; }
    public int? HttpStatusCode { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgementId { get; set; }

    public Order? Order { get; set; }
}
