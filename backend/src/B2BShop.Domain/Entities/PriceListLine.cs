namespace B2BShop.Domain.Entities;

public class PriceListLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PriceListId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? VariantId { get; set; }
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = "USD";

    // Hidden rule: DiscountPercent stacks with volume tiers ONLY in v2 engine
    // Legacy engine treats these as mutually exclusive (the bug students find in task 2)
    public decimal? DiscountPercent { get; set; }

    public CompanyPriceList? PriceList { get; set; }
    public Article? Article { get; set; }
}
