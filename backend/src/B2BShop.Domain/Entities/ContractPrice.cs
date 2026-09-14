namespace B2BShop.Domain.Entities;

public class ContractPrice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ArticleId { get; set; }
    public decimal ContractUnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? ContractReference { get; set; }

    // Hidden rule: contract price takes precedence over company price list
    // but NOT over volume tiers when AggregateAcrossLines is true in the v2 engine
    public bool AllowVolumeTierOverride { get; set; } = false;

    public Company? Company { get; set; }
    public Article? Article { get; set; }
}
