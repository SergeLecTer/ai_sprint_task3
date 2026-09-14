using B2BShop.Domain.Enums;

namespace B2BShop.Domain.Entities;

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}

public class QuoteLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }
    public Guid ArticleId { get; set; }
    public string ArticleCode { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public decimal? RequestedPrice { get; set; }
    public decimal? OfferedPrice { get; set; }
    public string? Notes { get; set; }

    public Quote? Quote { get; set; }
}
