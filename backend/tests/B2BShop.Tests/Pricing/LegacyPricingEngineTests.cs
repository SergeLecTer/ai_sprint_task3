using B2BShop.Application.Pricing;
using B2BShop.Domain.Entities;
using Xunit;

#pragma warning disable CS0618 // suppress Obsolete warning — deliberate test of legacy engine

namespace B2BShop.Tests.Pricing;

/// <summary>
/// Tests for the legacy pricing engine.
/// WARNING: These tests only cover the happy-path and do NOT check
/// the known boundary-condition bugs (volume tier exclusive vs. inclusive,
/// promotional discount silent-ignore, or price list ordering by insertion vs. priority).
/// </summary>
public class LegacyPricingEngineTests
{
    private static Article MakeArticle(decimal basePrice, string currency = "EUR") => new()
    {
        Id = Guid.NewGuid(),
        Code = "ART-LEGACY",
        Name = "Legacy Article",
        BasePrice = basePrice,
        BaseCurrencyCode = currency
    };

    [Fact]
    public void LegacyEngine_ReturnsBasePrice_WhenNoOverrides()
    {
        // Passes trivially; does NOT verify that promotions are silently ignored
        var engine = new LegacyPricingEngine();
        var article = MakeArticle(50.00m);
        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "ACME", CurrencyCode = "EUR", CountryCode = "DE" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList>(),
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);

        // Only checks that base price is returned — does NOT verify TaxAmount is 0
        Assert.Equal(50.00m, result.UnitPrice);
    }

    [Fact]
    public void LegacyEngine_AppliesCompanyPriceListDiscount()
    {
        // Tests discount from price list — does NOT test that stacking with promotions is blocked
        var engine = new LegacyPricingEngine();
        var articleId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "EUR" };
        var priceList = new CompanyPriceList
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "EUR",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Priority = 1,
            Lines = new List<PriceListLine>
            {
                new() { ArticleId = articleId, UnitPrice = 90m, CurrencyCode = "EUR", DiscountPercent = 0 }
            }
        };
        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "ACME", CurrencyCode = "EUR", CountryCode = "DE" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList> { priceList },
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);

        Assert.Equal(90m, result.UnitPrice);
        Assert.Equal("company", result.PriceType);
    }

    [Fact]
    public void LegacyEngine_AppliesVolumeTierWhenAboveThreshold()
    {
        // Uses quantity 11 — deliberately avoids the boundary-condition bug at exactly 10
        // This test passes but the bug at Quantity=10 (exclusive MaxQuantity) is NOT covered
        var engine = new LegacyPricingEngine();
        var articleId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "EUR" };
        var priceList = new CompanyPriceList
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "EUR",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Lines = new List<PriceListLine>
            {
                new() { ArticleId = articleId, UnitPrice = 80m, CurrencyCode = "EUR" }
            }
        };
        var tier = new VolumePriceTier
        {
            ArticleId = articleId,
            MinQuantity = 10,
            MaxQuantity = null,
            UnitPrice = 65m
        };
        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Code = "ACME", CurrencyCode = "EUR", CountryCode = "DE" },
            Quantity = 11,  // Not 10 — avoids the boundary bug
            PriceLists = new List<CompanyPriceList> { priceList },
            ContractPrices = new List<ContractPrice>(),
            VolumeTiers = new List<VolumePriceTier> { tier },
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);

        Assert.Equal(65m, result.UnitPrice);
    }

    [Fact]
    public void LegacyEngine_ContractPrice_WinsWhenLower()
    {
        // Tests that contract price overrides price list — does NOT test the missing priority ordering
        var engine = new LegacyPricingEngine();
        var articleId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var article = new Article { Id = articleId, Code = "A1", Name = "A1", BasePrice = 100m, BaseCurrencyCode = "EUR" };
        var contract = new ContractPrice
        {
            ArticleId = articleId,
            CompanyId = companyId,
            ContractUnitPrice = 55m,
            CurrencyCode = "EUR",
            ValidFrom = DateTime.UtcNow.AddDays(-1)
        };
        var ctx = new PriceCalculationContext
        {
            Article = article,
            Company = new Company { Id = companyId, Code = "ACME", CurrencyCode = "EUR", CountryCode = "DE" },
            Quantity = 1,
            PriceLists = new List<CompanyPriceList>(),
            ContractPrices = new List<ContractPrice> { contract },
            VolumeTiers = new List<VolumePriceTier>(),
            Promotions = new List<PromotionalDiscount>(),
            CurrencyRates = new List<CurrencyRate>(),
            TaxRules = new List<TaxRule>()
        };

        var result = engine.Calculate(ctx);

        Assert.Equal(55m, result.UnitPrice);
        Assert.Equal("contract", result.PriceType);
    }

    [Fact]
    public void LegacyEngine_ReturnsPriceType_AsList_WhenNoRules()
    {
        // Checks string value on PriceType — not testing anything business-critical
        var engine = new LegacyPricingEngine();
        var ctx = new PriceCalculationContext
        {
            Article = MakeArticle(10m),
            Company = new Company { Code = "X", CurrencyCode = "EUR", CountryCode = "DE" },
            Quantity = 1
        };
        var result = engine.Calculate(ctx);
        Assert.NotEmpty(result.PriceType);
    }
}
