using B2BShop.Application.Catalog;
using B2BShop.Application.Pricing;
using B2BShop.Domain.Entities;
using B2BShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace B2BShop.Infrastructure.Services;

public class CatalogService
{
    private readonly AppDbContext _db;
    private readonly PricingEngineSelector _pricing;
    private readonly AssortmentEngine _assortment;
    private readonly IMemoryCache _cache;

    // Cache duration — deliberately undocumented; stale catalog entries are a training scenario
    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PriceCacheDuration = TimeSpan.FromMinutes(5);

    public CatalogService(AppDbContext db, PricingEngineSelector pricing, AssortmentEngine assortment, IMemoryCache cache)
    {
        _db = db;
        _pricing = pricing;
        _assortment = assortment;
        _cache = cache;
    }

    public async Task<CatalogPageResult> GetCatalogPageAsync(
        Guid companyId, CatalogQuery query, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .Include(c => c.UiConfig)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found");

        var priceCtx = await LoadPriceContextAsync(company, ct);

        var now = DateTime.UtcNow;
        var productAssignments = await _db.ProductAssignments
            .Where(a => a.CompanyId == companyId)
            .ToListAsync(ct);
        var categoryAssignments = await _db.CategoryAssignments
            .Where(a => a.CompanyId == companyId)
            .ToListAsync(ct);

        var articlesQuery = _db.Articles
            .Include(a => a.Category)
            .Include(a => a.StockLevels)
            .Include(a => a.Variants)
            .Where(a => a.IsActive);

        if (query.CategoryId.HasValue)
            articlesQuery = articlesQuery.Where(a => a.CategoryId == query.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            articlesQuery = articlesQuery.Where(a => a.Name.Contains(query.Search) || a.Code.Contains(query.Search));

        var articles = await articlesQuery.ToListAsync(ct);

        var visibleArticles = articles
            .Select(a => new {
                Article = a,
                Visibility = _assortment.GetVisibility(a, productAssignments, categoryAssignments, now)
            })
            .Where(x => x.Visibility.IsVisible)
            .ToList();

        var sorted = query.SortBy switch {
            "price_asc" => visibleArticles.OrderBy(x => x.Article.BasePrice).ToList(),
            "price_desc" => visibleArticles.OrderByDescending(x => x.Article.BasePrice).ToList(),
            "code" => visibleArticles.OrderBy(x => x.Article.Code).ToList(),
            _ => visibleArticles.OrderBy(x => x.Article.Name).ToList()
        };

        int total = sorted.Count;
        int pageSize = query.PageSize ?? 24;
        int page = query.Page ?? 1;
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = paged.Select(x => {
            var priceResult = _pricing.Calculate(new PriceCalculationContext
            {
                Article = x.Article,
                Company = company,
                Quantity = 1,
                PriceLists = priceCtx.PriceLists,
                ContractPrices = priceCtx.ContractPrices,
                VolumeTiers = priceCtx.VolumeTiers,
                Promotions = priceCtx.Promotions,
                CurrencyRates = priceCtx.CurrencyRates,
                TaxRules = priceCtx.TaxRules
            });

            return new CatalogItemDto
            {
                ArticleId = x.Article.Id,
                Code = x.Article.Code,
                Name = x.Article.Name,
                CategoryId = x.Article.CategoryId,
                CategoryName = x.Article.Category?.Name ?? "",
                UnitOfMeasure = x.Article.UnitOfMeasure,
                PackSize = x.Article.PackSize,
                ImageUrl = x.Article.ImageUrl,
                IsOrderable = x.Visibility.IsOrderable,
                StockLevel = company.UiConfig?.ShowStockLevel == true
                    ? x.Article.StockLevels.Sum(s => s.AvailableQuantity)
                    : (int?)null,
                ResolvedPrice = priceResult,
                Attributes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(x.Article.AttributesJson) ?? new()
            };
        }).ToList();

        return new CatalogPageResult
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public async Task<CatalogItemDto?> GetItemAsync(Guid companyId, Guid articleId, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .Include(c => c.UiConfig)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company == null) return null;

        var article = await _db.Articles
            .Include(a => a.Category)
            .Include(a => a.StockLevels)
            .Include(a => a.Variants)
            .FirstOrDefaultAsync(a => a.Id == articleId && a.IsActive, ct);
        if (article == null) return null;

        var priceCtx = await LoadPriceContextAsync(company, ct);
        var priceResult = _pricing.Calculate(new PriceCalculationContext
        {
            Article = article,
            Company = company,
            Quantity = 1,
            PriceLists = priceCtx.PriceLists,
            ContractPrices = priceCtx.ContractPrices,
            VolumeTiers = priceCtx.VolumeTiers,
            Promotions = priceCtx.Promotions,
            CurrencyRates = priceCtx.CurrencyRates,
            TaxRules = priceCtx.TaxRules
        });

        return new CatalogItemDto
        {
            ArticleId = article.Id,
            Code = article.Code,
            Name = article.Name,
            Description = article.Description,
            CategoryId = article.CategoryId,
            CategoryName = article.Category?.Name ?? "",
            UnitOfMeasure = article.UnitOfMeasure,
            PackSize = article.PackSize,
            ImageUrl = article.ImageUrl,
            IsOrderable = true,
            StockLevel = company.UiConfig?.ShowStockLevel == true
                ? article.StockLevels.Sum(s => s.AvailableQuantity)
                : null,
            ResolvedPrice = priceResult,
            Attributes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(article.AttributesJson) ?? new(),
            Variants = article.Variants.Select(v => new VariantDto
            {
                Id = v.Id,
                ArticleId = v.ArticleId,
                VariantCode = v.VariantCode,
                ExtraPrice = v.ExtraPrice,
                Stock = v.Stock
            }).ToList()
        };
    }

    private async Task<PriceContext> LoadPriceContextAsync(Company company, CancellationToken ct)
    {
        var cacheKey = $"price-ctx-{company.Id}";
        if (_cache.TryGetValue(cacheKey, out PriceContext? cached) && cached != null)
            return cached;

        var now = DateTime.UtcNow;
        var priceLists = await _db.PriceLists
            .Include(pl => pl.Lines)
            .Include(pl => pl.VolumeTiers)
            .Where(pl => pl.CompanyId == company.Id && pl.IsActive)
            .ToListAsync(ct);

        var contractPrices = await _db.ContractPrices
            .Where(c => c.CompanyId == company.Id && c.ValidFrom <= now && (c.ValidTo == null || c.ValidTo >= now))
            .ToListAsync(ct);

        var volumeTiers = priceLists.SelectMany(pl => pl.VolumeTiers).ToList();

        var promotions = await _db.PromotionalDiscounts
            .Where(p => (p.CompanyId == company.Id || p.CompanyId == null) &&
                        p.IsActive && p.ValidFrom <= now && (p.ValidTo == null || p.ValidTo >= now))
            .ToListAsync(ct);

        var currencyRates = await _db.CurrencyRates
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo >= now))
            .ToListAsync(ct);

        var taxRules = await _db.TaxRules
            .Where(t => (t.CompanyId == company.Id || t.CompanyId == null) && t.IsActive)
            .ToListAsync(ct);

        var ctx = new PriceContext
        {
            PriceLists = priceLists,
            ContractPrices = contractPrices,
            VolumeTiers = volumeTiers,
            Promotions = promotions,
            CurrencyRates = currencyRates,
            TaxRules = taxRules
        };

        _cache.Set(cacheKey, ctx, PriceCacheDuration);
        return ctx;
    }

    private class PriceContext
    {
        public List<CompanyPriceList> PriceLists { get; set; } = new();
        public List<ContractPrice> ContractPrices { get; set; } = new();
        public List<VolumePriceTier> VolumeTiers { get; set; } = new();
        public List<PromotionalDiscount> Promotions { get; set; } = new();
        public List<CurrencyRate> CurrencyRates { get; set; } = new();
        public List<TaxRule> TaxRules { get; set; } = new();
    }
}
