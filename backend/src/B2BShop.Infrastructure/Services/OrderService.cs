using B2BShop.Application.Orders;
using B2BShop.Application.Pricing;
using B2BShop.Domain.Entities;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Infrastructure.Services;

public class OrderService
{
    private readonly AppDbContext _db;
    private readonly PricingEngineSelector _pricing;

    public OrderService(AppDbContext db, PricingEngineSelector pricing)
    {
        _db = db;
        _pricing = pricing;
    }

    public async Task<Order> CreateOrderAsync(Guid companyId, Guid userId, CreateOrderRequest req, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .Include(c => c.CheckoutConfig)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found");

        var priceLists = await _db.PriceLists
            .Include(pl => pl.Lines)
            .Include(pl => pl.VolumeTiers)
            .Where(pl => pl.CompanyId == companyId && pl.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var contractPrices = await _db.ContractPrices
            .Where(c => c.CompanyId == companyId && c.ValidFrom <= now && (c.ValidTo == null || c.ValidTo >= now))
            .ToListAsync(ct);

        var promotions = await _db.PromotionalDiscounts
            .Where(p => (p.CompanyId == companyId || p.CompanyId == null) && p.IsActive && p.ValidFrom <= now && (p.ValidTo == null || p.ValidTo >= now))
            .ToListAsync(ct);

        var currencyRates = await _db.CurrencyRates
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo >= now))
            .ToListAsync(ct);

        var taxRules = await _db.TaxRules
            .Where(t => (t.CompanyId == companyId || t.CompanyId == null) && t.IsActive)
            .ToListAsync(ct);

        var quantitiesByArticle = req.Lines.GroupBy(l => l.ArticleId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var orderLines = new List<OrderLine>();
        decimal subtotal = 0;

        foreach (var lineReq in req.Lines)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == lineReq.ArticleId, ct)
                ?? throw new InvalidOperationException($"Article {lineReq.ArticleId} not found");

            // Hidden rule: enforce pack size rounding
            int quantity = lineReq.Quantity;
            if (article.EnforcePackSizeRounding && article.PackSize > 1)
            {
                quantity = (int)Math.Ceiling((double)quantity / article.PackSize) * article.PackSize;
            }

            var priceResult = _pricing.Calculate(new PriceCalculationContext
            {
                Article = article,
                VariantId = lineReq.VariantId,
                Company = company,
                Quantity = quantity,
                PriceLists = priceLists,
                ContractPrices = contractPrices,
                VolumeTiers = priceLists.SelectMany(pl => pl.VolumeTiers).ToList(),
                Promotions = promotions,
                CurrencyRates = currencyRates,
                TaxRules = taxRules,
                OrderQuantitiesByArticle = quantitiesByArticle
            });

            decimal lineTotal = priceResult.UnitPrice * quantity;
            subtotal += lineTotal;

            orderLines.Add(new OrderLine
            {
                ArticleId = lineReq.ArticleId,
                ArticleCode = article.Code,
                ArticleName = article.Name,
                VariantId = lineReq.VariantId,
                Quantity = quantity,
                UnitOfMeasure = article.UnitOfMeasure,
                UnitPrice = priceResult.UnitPrice,
                OriginalUnitPrice = article.BasePrice,
                LineTotal = lineTotal,
                PriceType = priceResult.PriceType,
                Notes = lineReq.Notes
            });
        }

        // Check credit limit
        if (company.CreditLimit.HasValue && company.CurrentCreditUsed + subtotal > company.CreditLimit.Value)
            throw new InvalidOperationException("Order exceeds credit limit");

        bool requiresApproval = company.CheckoutConfig?.RequiresApproval == true &&
            (!company.CheckoutConfig.ApprovalSpendThreshold.HasValue || subtotal >= company.CheckoutConfig.ApprovalSpendThreshold.Value);

        var order = new Order
        {
            CompanyId = companyId,
            UserId = userId,
            OrderNumber = await GenerateOrderNumberAsync(ct),
            Status = OrderStatus.Draft,
            Lines = orderLines,
            Subtotal = subtotal,
            DiscountTotal = 0,
            TaxTotal = orderLines.Sum(l => 0m),  // Deliberate: tax not summed from lines
            GrandTotal = subtotal,
            CurrencyCode = company.CurrencyCode,
            DeliveryAddressId = req.DeliveryAddressId,
            PoNumber = req.PoNumber,
            RequestedDeliveryDate = req.RequestedDeliveryDate,
            Notes = req.Notes,
            ApprovalRequired = requiresApproval,
            PricingEngineVersion = company.PricingEngineVersion
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> SubmitOrderAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
            ?? throw new InvalidOperationException("Order not found");

        var targetStatus = order.ApprovalRequired ? OrderStatus.PendingApproval : OrderStatus.Approved;
        order.TransitionTo(targetStatus);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> ApproveOrderAsync(Guid orderId, Guid approverId, string? comment, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found");

        order.TransitionTo(OrderStatus.Approved);
        order.ApproverId = approverId;
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> CancelOrderAsync(Guid orderId, Guid userId, string reason, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found");

        order.TransitionTo(OrderStatus.Cancelled);
        order.Notes = (order.Notes ?? "") + $"\nCancelled: {reason}";
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> ReorderAsync(Guid sourceOrderId, Guid userId, CancellationToken ct = default)
    {
        var source = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == sourceOrderId, ct)
            ?? throw new InvalidOperationException("Source order not found");

        var req = new CreateOrderRequest
        {
            Lines = source.Lines.Select(l => new OrderLineRequest
            {
                ArticleId = l.ArticleId,
                VariantId = l.VariantId,
                Quantity = l.Quantity,
                Notes = l.Notes
            }).ToList(),
            Notes = $"Reorder of {source.OrderNumber}"
        };

        return await CreateOrderAsync(source.CompanyId, userId, req, ct);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        // Deliberate: not atomic — race condition possible under high concurrency (training scenario)
        var count = await _db.Orders.CountAsync(ct);
        return $"ORD-{DateTime.UtcNow:yyyyMM}-{count + 1:D5}";
    }
}
