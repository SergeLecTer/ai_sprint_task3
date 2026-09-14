using System.Security.Claims;
using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/quotes")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly AppDbContext _db;

    public QuotesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var (companyId, _) = GetContext();
        var q = _db.Quotes
            .Include(qt => qt.Lines)
            .Where(qt => qt.CompanyId == companyId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<QuoteStatus>(status, true, out var s))
            q = q.Where(qt => qt.Status == s);

        var quotes = await q.OrderByDescending(qt => qt.CreatedAt).ToListAsync();
        return Ok(quotes.Select(MapQuote));
    }

    [HttpGet("{quoteId}")]
    public async Task<IActionResult> Get(Guid quoteId)
    {
        var (companyId, _) = GetContext();
        var quote = await _db.Quotes
            .Include(qt => qt.Lines)
            .FirstOrDefaultAsync(qt => qt.Id == quoteId && qt.CompanyId == companyId);

        if (quote == null) return NotFound();
        return Ok(MapQuote(quote));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteRequest req)
    {
        var (companyId, userId) = GetContext();
        var quoteCount = await _db.Quotes.CountAsync();

        var lines = new List<QuoteLine>();
        foreach (var lineReq in req.Lines)
        {
            var article = await _db.Articles.FindAsync(lineReq.ArticleId);
            if (article == null) return BadRequest(new { message = $"Article {lineReq.ArticleId} not found" });

            lines.Add(new QuoteLine
            {
                Id = Guid.NewGuid(),
                ArticleId = lineReq.ArticleId,
                ArticleCode = article.Code,
                ArticleName = article.Name,
                RequestedQuantity = lineReq.Quantity,
                RequestedPrice = lineReq.RequestedPrice,
                Notes = lineReq.Notes
            });
        }

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = $"QUO-{DateTime.UtcNow:yyyyMM}-{quoteCount + 1:D4}",
            CompanyId = companyId,
            UserId = userId,
            Status = QuoteStatus.Draft,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = lines
        };

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { quoteId = quote.Id }, MapQuote(quote));
    }

    [HttpPost("{quoteId}/submit")]
    public async Task<IActionResult> Submit(Guid quoteId)
    {
        var (companyId, _) = GetContext();
        var quote = await _db.Quotes
            .Include(qt => qt.Lines)
            .FirstOrDefaultAsync(qt => qt.Id == quoteId && qt.CompanyId == companyId);

        if (quote == null) return NotFound();
        if (quote.Status != QuoteStatus.Draft)
            return BadRequest(new { message = "Only draft quotes can be submitted" });

        quote.Status = QuoteStatus.Submitted;
        quote.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(MapQuote(quote));
    }

    [HttpPost("{quoteId}/accept")]
    public async Task<IActionResult> Accept(Guid quoteId)
    {
        var (companyId, _) = GetContext();
        var quote = await _db.Quotes
            .Include(qt => qt.Lines)
            .FirstOrDefaultAsync(qt => qt.Id == quoteId && qt.CompanyId == companyId);

        if (quote == null) return NotFound();
        if (quote.Status != QuoteStatus.Reviewing)
            return BadRequest(new { message = "Only reviewed quotes can be accepted" });
        if (quote.ValidUntil.HasValue && quote.ValidUntil < DateTime.UtcNow)
            return BadRequest(new { message = "Quote has expired" });

        quote.Status = QuoteStatus.Accepted;
        quote.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(MapQuote(quote));
    }

    [HttpPost("{quoteId}/reject")]
    public async Task<IActionResult> Reject(Guid quoteId)
    {
        var (companyId, _) = GetContext();
        var quote = await _db.Quotes
            .Include(qt => qt.Lines)
            .FirstOrDefaultAsync(qt => qt.Id == quoteId && qt.CompanyId == companyId);

        if (quote == null) return NotFound();
        quote.Status = QuoteStatus.Rejected;
        quote.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(MapQuote(quote));
    }

    private static object MapQuote(Quote qt) => new {
        qt.Id,
        qt.QuoteNumber,
        qt.CompanyId,
        qt.UserId,
        status = qt.Status.ToString().ToLower(),
        qt.ValidUntil,
        qt.Notes,
        qt.CreatedAt,
        qt.UpdatedAt,
        lines = qt.Lines.Select(l => new {
            l.Id,
            l.ArticleId,
            l.ArticleCode,
            l.ArticleName,
            requestedQuantity = l.RequestedQuantity,
            requestedPrice = l.RequestedPrice,
            offeredPrice = l.OfferedPrice,
            l.Notes
        })
    };

    private (Guid companyId, Guid userId) GetContext()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var companyIdStr = User.FindFirstValue("actingCompanyId") ?? User.FindFirstValue("companyId");
        return (Guid.Parse(companyIdStr!), userId);
    }
}

public class CreateQuoteRequest
{
    public List<QuoteLineRequest> Lines { get; set; } = new();
    public string? Notes { get; set; }
}

public class QuoteLineRequest
{
    public Guid ArticleId { get; set; }
    public int Quantity { get; set; }
    public decimal? RequestedPrice { get; set; }
    public string? Notes { get; set; }
}
