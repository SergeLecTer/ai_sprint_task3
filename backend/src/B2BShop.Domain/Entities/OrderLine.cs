namespace B2BShop.Domain.Entities;

public class OrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    // Note: "Article" naming used here, but "Product" in catalog module — intentional inconsistency
    public Guid ArticleId { get; set; }
    public string ArticleCode { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "PCS";
    public decimal UnitPrice { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    // Which pricing engine produced this price — tracked per line
    public string? PriceType { get; set; }
    public string? AppliedPromotionCode { get; set; }

    public Order? Order { get; set; }
    public Article? Article { get; set; }
}
