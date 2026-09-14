using B2BShop.Application.Pricing;

namespace B2BShop.Application.Catalog;

// DTOs and query models live here in Application layer
// The actual CatalogService implementation lives in B2BShop.Infrastructure.Services
// to avoid circular dependency (Application <- Infrastructure)

public class CatalogQuery
{
    public Guid? CategoryId { get; set; }
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? SortBy { get; set; }
}

public class CatalogPageResult
{
    public List<CatalogItemDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CatalogItemDto
{
    public Guid ArticleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int PackSize { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsOrderable { get; set; }
    public int? StockLevel { get; set; }
    public PriceResult ResolvedPrice { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<VariantDto> Variants { get; set; } = new();
}

public class VariantDto
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    public decimal ExtraPrice { get; set; }
    public int Stock { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}
