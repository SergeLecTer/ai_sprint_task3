namespace B2BShop.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    // Inconsistent: some audit entries use "CreatedAt" instead of "OccurredAt" in the DB
    // This is one of the deliberate inconsistencies students will find
}
