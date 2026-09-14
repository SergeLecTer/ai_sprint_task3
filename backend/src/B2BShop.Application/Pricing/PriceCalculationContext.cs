using B2BShop.Domain.Entities;

namespace B2BShop.Application.Pricing;

public class PriceCalculationContext
{
    public required Article Article { get; set; }
    public Guid? VariantId { get; set; }
    public required Company Company { get; set; }
    public int Quantity { get; set; } = 1;

    // Pre-loaded from DB for the company
    public List<CompanyPriceList> PriceLists { get; set; } = new();
    public List<ContractPrice> ContractPrices { get; set; } = new();
    public List<VolumePriceTier> VolumeTiers { get; set; } = new();
    public List<PromotionalDiscount> Promotions { get; set; } = new();
    public List<CurrencyRate> CurrencyRates { get; set; } = new();
    public List<TaxRule> TaxRules { get; set; } = new();

    // For aggregate tier calculations — quantities of other lines in same order
    public Dictionary<Guid, int> OrderQuantitiesByArticle { get; set; } = new();
    public Dictionary<Guid, int> OrderQuantitiesByProductGroup { get; set; } = new();

    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}

public class PriceResult
{
    public decimal UnitPrice { get; set; }
    public decimal DisplayPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalWithTax { get; set; }
    public string PriceType { get; set; } = "list";
    public List<TierInfo> AppliedTiers { get; set; } = new();
    public List<string> AppliedPromotions { get; set; } = new();
    public DateTime? ValidUntil { get; set; }
}

public class TierInfo
{
    public int MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Label { get; set; }
}
