namespace B2BShop.Domain.Entities;

// Named "Article" in domain, "Product" in catalog service, "Item" in some legacy pricing code — intentional
public class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string UnitOfMeasure { get; set; } = "PCS";
    public int PackSize { get; set; } = 1;
    public decimal BasePrice { get; set; }
    public string BaseCurrencyCode { get; set; } = "USD";
    public decimal? Weight { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Attributes stored as JSON — e.g. {"color":"blue","material":"steel"}
    public string AttributesJson { get; set; } = "{}";

    // Dead property: was for ERP code mapping, no longer used
    public string? LegacyErpCode { get; set; }

    // Hidden rule: MinOrderQuantity is checked at order validation, not at catalog display
    public int MinOrderQuantity { get; set; } = 1;

    // Hidden rule: products with PackSize > 1 round quantities up to nearest pack
    public bool EnforcePackSizeRounding { get; set; } = true;

    public ProductCategory? Category { get; set; }
    public ICollection<ArticleVariant> Variants { get; set; } = new List<ArticleVariant>();
    public ICollection<ArticleStockLevel> StockLevels { get; set; } = new List<ArticleStockLevel>();
    public ICollection<CompanyProductAssignment> CompanyAssignments { get; set; } = new List<CompanyProductAssignment>();
    public ICollection<PriceListLine> PriceListLines { get; set; } = new List<PriceListLine>();
}
