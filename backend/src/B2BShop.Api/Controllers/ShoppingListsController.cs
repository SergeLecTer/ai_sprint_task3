using System.Security.Claims;
using B2BShop.Domain.Entities;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/shopping-lists")]
[Authorize]
public class ShoppingListsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ShoppingListsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var (companyId, userId) = GetContext();
        var lists = await _db.ShoppingLists
            .Include(sl => sl.Lines)
            .Where(sl => sl.CompanyId == companyId && (sl.UserId == userId || sl.IsShared))
            .OrderByDescending(sl => sl.CreatedAt)
            .ToListAsync();

        return Ok(lists.Select(sl => new {
            sl.Id,
            sl.Name,
            sl.IsShared,
            ownerId = sl.UserId,
            sl.CreatedAt,
            lineCount = sl.Lines.Count,
            lines = sl.Lines.Select(l => new {
                l.Id,
                l.ArticleId,
                l.ArticleCode,
                l.ArticleName,
                l.Quantity,
                l.Notes
            })
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShoppingListRequest req)
    {
        var (companyId, userId) = GetContext();

        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Name = req.Name,
            IsShared = req.IsShared,
            CreatedAt = DateTime.UtcNow,
            Lines = new List<ShoppingListLine>()
        };

        _db.ShoppingLists.Add(list);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { listId = list.Id }, list);
    }

    [HttpGet("{listId}")]
    public async Task<IActionResult> Get(Guid listId)
    {
        var (companyId, userId) = GetContext();
        var list = await _db.ShoppingLists
            .Include(sl => sl.Lines)
            .FirstOrDefaultAsync(sl => sl.Id == listId && sl.CompanyId == companyId);

        if (list == null) return NotFound();
        if (list.UserId != userId && !list.IsShared) return Forbid();

        return Ok(new {
            list.Id,
            list.Name,
            list.IsShared,
            ownerId = list.UserId,
            list.CreatedAt,
            lines = list.Lines.Select(l => new {
                l.Id,
                l.ArticleId,
                l.ArticleCode,
                l.ArticleName,
                l.Quantity,
                l.Notes
            })
        });
    }

    [HttpPost("{listId}/lines")]
    public async Task<IActionResult> AddLine(Guid listId, [FromBody] AddLineRequest req)
    {
        var (companyId, userId) = GetContext();
        var list = await _db.ShoppingLists
            .Include(sl => sl.Lines)
            .FirstOrDefaultAsync(sl => sl.Id == listId && sl.CompanyId == companyId);

        if (list == null) return NotFound();
        if (list.UserId != userId) return Forbid();

        var article = await _db.Articles.FindAsync(req.ArticleId);
        if (article == null) return BadRequest(new { message = "Article not found" });

        var existing = list.Lines.FirstOrDefault(l => l.ArticleId == req.ArticleId);
        if (existing != null)
        {
            existing.Quantity += req.Quantity;
        }
        else
        {
            list.Lines.Add(new ShoppingListLine
            {
                Id = Guid.NewGuid(),
                ShoppingListId = listId,
                ArticleId = req.ArticleId,
                ArticleCode = article.Code,
                ArticleName = article.Name,
                Quantity = req.Quantity,
                Notes = req.Notes
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Line added" });
    }

    [HttpDelete("{listId}/lines/{lineId}")]
    public async Task<IActionResult> RemoveLine(Guid listId, Guid lineId)
    {
        var (companyId, userId) = GetContext();
        var list = await _db.ShoppingLists
            .Include(sl => sl.Lines)
            .FirstOrDefaultAsync(sl => sl.Id == listId && sl.CompanyId == companyId);

        if (list == null) return NotFound();
        if (list.UserId != userId) return Forbid();

        var line = list.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null) return NotFound();

        list.Lines.Remove(line);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{listId}")]
    public async Task<IActionResult> Delete(Guid listId)
    {
        var (companyId, userId) = GetContext();
        var list = await _db.ShoppingLists
            .FirstOrDefaultAsync(sl => sl.Id == listId && sl.CompanyId == companyId);

        if (list == null) return NotFound();
        if (list.UserId != userId) return Forbid();

        _db.ShoppingLists.Remove(list);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private (Guid companyId, Guid userId) GetContext()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var companyIdStr = User.FindFirstValue("actingCompanyId") ?? User.FindFirstValue("companyId");
        return (Guid.Parse(companyIdStr!), userId);
    }
}

public class CreateShoppingListRequest
{
    public string Name { get; set; } = "";
    public bool IsShared { get; set; }
}

public class AddLineRequest
{
    public Guid ArticleId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
