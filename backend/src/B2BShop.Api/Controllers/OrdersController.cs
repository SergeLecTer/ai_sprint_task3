using System.Security.Claims;
using B2BShop.Application.Orders;
using B2BShop.Infrastructure.Persistence;
using B2BShop.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly AppDbContext _db;

    public OrdersController(OrderService orders, AppDbContext db)
    {
        _orders = orders;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var (companyId, userId) = GetContext();
        try
        {
            var order = await _orders.CreateOrderAsync(companyId, userId, req);
            return CreatedAtAction(nameof(Get), new { orderId = order.Id }, MapOrder(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> Get(Guid orderId)
    {
        var (companyId, userId) = GetContext();
        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) return NotFound();
        return Ok(MapOrder(order));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (companyId, _) = GetContext();
        var q = _db.Orders.Where(o => o.CompanyId == companyId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<B2BShop.Domain.Enums.OrderStatus>(
            string.Concat(status.Split('_').Select(s => char.ToUpper(s[0]) + s[1..])), out var s))
        {
            q = q.Where(o => o.Status == s);
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Lines)
            .ToListAsync();

        return Ok(new {
            items = items.Select(MapOrder),
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("pending-approval")]
    public async Task<IActionResult> PendingApproval()
    {
        var (companyId, _) = GetContext();
        var orders = await _db.Orders
            .Include(o => o.Lines)
            .Where(o => o.CompanyId == companyId && o.Status == B2BShop.Domain.Enums.OrderStatus.PendingApproval)
            .ToListAsync();

        return Ok(orders.Select(MapOrder));
    }

    [HttpPost("{orderId}/submit")]
    public async Task<IActionResult> Submit(Guid orderId)
    {
        var (_, userId) = GetContext();
        try
        {
            var order = await _orders.SubmitOrderAsync(orderId, userId);
            return Ok(MapOrder(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId}/approve")]
    public async Task<IActionResult> Approve(Guid orderId, [FromBody] ApproveRequest? req)
    {
        var (_, userId) = GetContext();
        try
        {
            var order = await _orders.ApproveOrderAsync(orderId, userId, req?.Comment);
            return Ok(MapOrder(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId}/reject")]
    public async Task<IActionResult> Reject(Guid orderId, [FromBody] RejectRequest req)
    {
        var (_, userId) = GetContext();
        try
        {
            var order = await _orders.CancelOrderAsync(orderId, userId, req.Reason);
            return Ok(MapOrder(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId, [FromBody] CancelRequest req)
    {
        var (_, userId) = GetContext();
        try
        {
            var order = await _orders.CancelOrderAsync(orderId, userId, req.Reason);
            return Ok(MapOrder(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{orderId}/reorder")]
    public async Task<IActionResult> Reorder(Guid orderId)
    {
        var (_, userId) = GetContext();
        var newOrder = await _orders.ReorderAsync(orderId, userId);
        return CreatedAtAction(nameof(Get), new { orderId = newOrder.Id }, MapOrder(newOrder));
    }

    private (Guid companyId, Guid userId) GetContext()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var companyIdStr = User.FindFirstValue("actingCompanyId") ?? User.FindFirstValue("companyId");
        var companyId = Guid.Parse(companyIdStr!);
        return (companyId, userId);
    }

    private static object MapOrder(B2BShop.Domain.Entities.Order o) => new {
        o.Id,
        o.OrderNumber,
        o.CompanyId,
        o.UserId,
        status = o.Status.ToString().ToLower().Replace("pendingapproval", "pending_approval")
                                               .Replace("partiallyfulfilled", "partially_fulfilled"),
        lines = o.Lines.Select(l => new {
            l.Id,
            l.ArticleId,
            l.ArticleCode,
            l.ArticleName,
            l.Quantity,
            l.UnitOfMeasure,
            l.UnitPrice,
            l.LineTotal,
            l.DiscountPercent,
            l.Notes
        }),
        o.Subtotal,
        o.DiscountTotal,
        o.TaxTotal,
        o.GrandTotal,
        o.CurrencyCode,
        o.DeliveryAddressId,
        o.PoNumber,
        o.ApprovalRequired,
        o.Notes,
        createdAt = o.CreatedAt,
        updatedAt = o.UpdatedAt,
        transferredAt = o.TransferredAt,
        acknowledgedAt = o.AcknowledgedAt
    };
}

public record ApproveRequest(string? Comment);
public record RejectRequest(string Reason);
public record CancelRequest(string Reason);
