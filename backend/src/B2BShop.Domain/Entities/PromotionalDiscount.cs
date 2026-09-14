namespace B2BShop.Domain.Entities;

public class PromotionalDiscount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CompanyId { get; set; }  // null = applies to all companies
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Hidden rule: when DiscountType = "OrderTotal", applies when order subtotal >= MinOrderTotal
    // When DiscountType = "Article", applies per line based on ArticleId / CategoryId
    public string DiscountType { get; set; } = "Article";  // "Article" | "OrderTotal" | "Category"
    public Guid? ArticleId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinOrderTotal { get; set; }

    public decimal DiscountPercent { get; set; }
    public bool IsStackable { get; set; } = false;  // Hidden: most promotions don't stack with price list discounts

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public int? MaxUsesPerCompany { get; set; }
    public int UsedCount { get; set; }

    public Company? Company { get; set; }
}
