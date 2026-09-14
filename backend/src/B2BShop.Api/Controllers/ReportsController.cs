using System.Security.Claims;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        // Order stats
        var ordersThisMonth = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= monthStart)
            .CountAsync();

        var ordersLastMonth = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= lastMonthStart && o.CreatedAt < monthStart)
            .CountAsync();

        var totalSpendThisMonth = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= monthStart)
            .SumAsync(o => (decimal?)o.GrandTotal) ?? 0m;

        var totalSpendLastMonth = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= lastMonthStart && o.CreatedAt < monthStart)
            .SumAsync(o => (decimal?)o.GrandTotal) ?? 0m;

        var pendingApprovalCount = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.Status == OrderStatus.PendingApproval)
            .CountAsync();

        var openOrdersCount = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.Status != OrderStatus.Fulfilled
                && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Returned)
            .CountAsync();

        // Top articles by quantity (this month)
        var topArticles = await _db.OrderLines
            .Where(ol => ol.Order != null && ol.Order.CompanyId == companyId && ol.Order.CreatedAt >= monthStart)
            .GroupBy(ol => new { ol.ArticleId, ol.ArticleCode, ol.ArticleName })
            .Select(g => new {
                articleId = g.Key.ArticleId,
                articleCode = g.Key.ArticleCode,
                articleName = g.Key.ArticleName,
                totalQuantity = g.Sum(ol => ol.Quantity),
                totalSpend = g.Sum(ol => ol.LineTotal)
            })
            .OrderByDescending(x => x.totalSpend)
            .Take(5)
            .ToListAsync();

        // Order status distribution
        var statusBreakdown = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= lastMonthStart)
            .GroupBy(o => o.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToListAsync();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var notifications = await _db.Notifications
            .Where(n => n.UserId == currentUserId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new { n.Id, n.Title, n.Message, n.Type, n.CreatedAt })
            .ToListAsync();

        return Ok(new {
            ordersThisMonth,
            ordersLastMonth,
            ordersMonthDelta = ordersLastMonth == 0 ? 0 : (double)(ordersThisMonth - ordersLastMonth) / ordersLastMonth * 100,
            totalSpendThisMonth,
            totalSpendLastMonth,
            spendMonthDelta = totalSpendLastMonth == 0 ? 0 : (double)(totalSpendThisMonth - totalSpendLastMonth) / (double)totalSpendLastMonth * 100,
            pendingApprovalCount,
            openOrdersCount,
            topArticles,
            statusBreakdown,
            notifications
        });
    }

    [HttpGet("spend-analysis")]
    public async Task<IActionResult> SpendAnalysis([FromQuery] int months = 12)
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var cutoff = DateTime.UtcNow.AddMonths(-months);
        var data = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.CreatedAt >= cutoff)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new {
                year = g.Key.Year,
                month = g.Key.Month,
                orderCount = g.Count(),
                totalSpend = g.Sum(o => o.GrandTotal)
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync();

        return Ok(data);
    }

    private Guid? GetCompanyId()
    {
        var actingStr = User.FindFirstValue("actingCompanyId");
        if (!string.IsNullOrEmpty(actingStr) && Guid.TryParse(actingStr, out var actingId))
            return actingId;

        var companyStr = User.FindFirstValue("companyId");
        if (Guid.TryParse(companyStr, out var id))
            return id;

        return null;
    }
}
