using B2BShop.Application.Pricing;
using B2BShop.Domain.Entities;
using Xunit;

namespace B2BShop.Tests.Pricing;

public class PricingEngineV2Tests
{
    private static Article MakeArticle(decimal basePrice) => new()
    {
        Id = Guid.NewGuid(),
        Code = "ART-001",
        Name = "Test Article",
        BasePrice = basePrice,
        BaseCurrencyCode = "USD"
    };

    [Fact]
    public void CalculatePrice_UsesBaseWhenNoOverrides()
    {
        var engine = new PricingEngineV2();
        var article = MakeArticle(100.00m);
        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "TEST", CurrencyCode = "USD" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList>(),
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);

        Assert.Equal(100.00m, result.UnitPrice);
        Assert.Equal("list", result.PriceType);
    }

    [Fact]
    public void CalculatePrice_CompanyPriceListOverridesBasePrice()
    {
        var engine = new PricingEngineV2();
        var articleId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "USD" };
        var priceList = new CompanyPriceList
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "USD",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = null,
            Priority = 0,
            Lines = new List<PriceListLine>
            {
                new() { ArticleId = articleId, UnitPrice = 85m, CurrencyCode = "USD" }
            }
        };

        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "TEST", CurrencyCode = "USD" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList> { priceList },
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);
        Assert.Equal(85m, result.UnitPrice);
        Assert.Equal("company", result.PriceType);
    }

    [Fact]
    public void CalculatePrice_VolumeTierReducesPriceAtThreshold()
    {
        var engine = new PricingEngineV2();
        var articleId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "USD" };
        var priceList = new CompanyPriceList
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "USD",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Lines = new List<PriceListLine>
            {
                new() { ArticleId = articleId, UnitPrice = 80m, CurrencyCode = "USD" }
            },
            VolumeTiers = new List<VolumePriceTier>
            {
                new() { ArticleId = articleId, MinQuantity = 10, MaxQuantity = null, UnitPrice = 70m }
            }
        };

        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "TEST", CurrencyCode = "USD" },
            Quantity = 10,  // exactly at tier threshold
            PriceLists = new List<CompanyPriceList> { priceList },
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = priceList.VolumeTiers.ToList(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);
        Assert.Equal(70m, result.UnitPrice);
        Assert.Equal("volume", result.PriceType);
    }

    [Fact]
    public void CalculatePrice_ContractPriceOverridesCompanyPriceList()
    {
        var engine = new PricingEngineV2();
        var articleId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "USD" };
        var priceList = new CompanyPriceList
        {
            CurrencyCode = "USD",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Lines = new List<PriceListLine>
            {
                new() { ArticleId = articleId, UnitPrice = 85m, CurrencyCode = "USD" }
            }
        };
        var contract = new ContractPrice
        {
            CompanyId = companyId,
            ArticleId = articleId,
            ContractUnitPrice = 75m,
            CurrencyCode = "USD",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = null
        };

        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Id = companyId, Code = "TEST", CurrencyCode = "USD" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList> { priceList },
            ContractPrices = new List<ContractPrice> { contract },
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);
        Assert.Equal(75m, result.UnitPrice);
        Assert.Equal("contract", result.PriceType);
    }
}
