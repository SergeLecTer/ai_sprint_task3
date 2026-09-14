using System.Security.Claims;
using B2BShop.Application.Catalog;
using B2BShop.Infrastructure.Persistence;
using B2BShop.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalog;
    private readonly AppDbContext _db;

    public CatalogController(CatalogService catalog, AppDbContext db)
    {
        _catalog = catalog;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalog(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? sortBy = null)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var result = await _catalog.GetCatalogPageAsync(companyId.Value, new CatalogQuery
        {
            CategoryId = categoryId,
            Search = search,
            InStockOnly = inStockOnly,
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy
        });

        return Ok(result);
    }

    [HttpGet("{articleId}")]
    public async Task<IActionResult> GetItem(Guid articleId)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var item = await _catalog.GetItemAsync(companyId.Value, articleId);
        if (item == null) return NotFound();

        return Ok(item);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Code, c.Name, c.ParentId, c.SortOrder })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<object>());

        // Direct DB search — not using assortment filtering (deliberate bug for students to find)
        var results = await _db.Articles
            .Where(a => a.IsActive && (a.Name.Contains(q) || a.Code.Contains(q)))
            .Take(20)
            .Select(a => new { a.Id, a.Code, a.Name, a.CategoryId })
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("{articleId}/related")]
    public async Task<IActionResult> GetRelated(Guid articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return NotFound();

        // Related = same category, excluding self
        var related = await _db.Articles
            .Where(a => a.CategoryId == article.CategoryId && a.Id != articleId && a.IsActive)
            .Take(8)
            .ToListAsync();

        // Deliberate: related items are NOT filtered by assortment for the current company
        return Ok(related.Select(a => new { a.Id, a.Code, a.Name, a.ImageUrl, a.BasePrice, a.BaseCurrencyCode }));
    }

    private Guid? GetCompanyId()
    {
        var companyIdClaim = User.FindFirstValue("companyId");
        var actingCompanyIdClaim = User.FindFirstValue("actingCompanyId");

        // SalesRep acting on behalf of company takes precedence
        if (!string.IsNullOrEmpty(actingCompanyIdClaim) && Guid.TryParse(actingCompanyIdClaim, out var actingId))
            return actingId;

        if (Guid.TryParse(companyIdClaim, out var id))
            return id;

        return null;
    }
}
