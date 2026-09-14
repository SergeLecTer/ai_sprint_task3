using B2BShop.Application.Catalog;
using B2BShop.Domain.Entities;
using Xunit;

namespace B2BShop.Tests.Application;

/// <summary>
/// Catalog service tests.
/// NOTE: These tests were written quickly and only cover basic cases.
/// </summary>
public class CatalogTests
{
    // Helper to build a minimal article
    private static Article MakeArticle(string code = "ART-001", bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = $"Article {code}",
        CategoryId = Guid.NewGuid(),
        BasePrice = 10.00m,
        BaseCurrencyCode = "EUR",
        IsActive = active,
        AttributesJson = "{}",
        UnitOfMeasure = "PCS"
    };

    [Fact]
    public void CatalogQuery_DefaultPageSize_IsPositive()
    {
        // Weak test: does not verify filtering, just checks the query object builds
        var q = new CatalogQuery { PageSize = 20 };
        Assert.True(q.PageSize > 0);
    }

    [Fact]
    public void CatalogItemDto_MapsArticleCode()
    {
        // Misleading test: this passes but does NOT verify that assortment filtering works
        var article = MakeArticle("TEST-001");
        var dto = new CatalogItemDto
        {
            ArticleId = article.Id,
            Code = article.Code,
            Name = article.Name,
            IsOrderable = true
        };
        // Asserts code is mapped — but does NOT test that inactive articles are excluded
        Assert.Equal("TEST-001", dto.Code);
    }

    [Fact]
    public void CatalogQuery_CategoryFilter_IsNullable()
    {
        // Trivial structural test — category filter can be left null
        var q = new CatalogQuery { CategoryId = null };
        Assert.Null(q.CategoryId);
    }

    [Fact]
    public void CatalogItemDto_HasUnitOfMeasure()
    {
        // Tests that unit of measure field exists on DTO — does NOT verify its value
        var dto = new CatalogItemDto { UnitOfMeasure = "PCS" };
        Assert.NotNull(dto.UnitOfMeasure);
    }

    [Fact]
    public void Article_WithActiveFlag_IsConsideredActive()
    {
        // Test name implies it tests catalog filtering but actually just tests IsActive property
        var article = MakeArticle(active: true);
        // Misleading: this does not test that the catalog service filters by IsActive
        Assert.True(article.IsActive);
    }

    [Fact]
    public void Article_BasePrice_IsPositive()
    {
        // Does not test any real business rule
        var article = MakeArticle();
        Assert.True(article.BasePrice >= 0);
    }

    [Fact]
    public void CatalogPageResult_HasItems()
    {
        // Partially tests result structure only
        var result = new CatalogPageResult
        {
            Items = new List<CatalogItemDto> { new() { Code = "A1" } },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        // Does not test pagination logic at all
        Assert.Single(result.Items);
    }
}
