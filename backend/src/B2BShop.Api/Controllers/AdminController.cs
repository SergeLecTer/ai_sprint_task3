using System.Text.Json;
using B2BShop.Domain.Enums;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "PlatformAdmin,CompanyAdmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db) => _db = db;

    [HttpGet("companies")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> ListCompanies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Note: HasQueryFilter filters deactivated companies — admin bypasses via IgnoreQueryFilters
        var q = _db.Companies.IgnoreQueryFilters();
        var total = await q.CountAsync();
        var items = await q.OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new {
                c.Id,
                c.Code,
                c.Name,
                c.CurrencyCode,
                c.IsActive,
                c.PricingEngineVersion,
                deactivatedAt = c.DeactivatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("price-lists")]
    public async Task<IActionResult> ListPriceLists([FromQuery] Guid? companyId)
    {
        var q = _db.PriceLists.AsQueryable();
        if (companyId.HasValue)
            q = q.Where(pl => pl.CompanyId == companyId.Value);

        var items = await q.OrderByDescending(pl => pl.ValidFrom)
            .Select(pl => new {
                pl.Id,
                pl.Name,
                pl.CompanyId,
                pl.Priority,
                pl.IsActive,
                pl.ValidFrom,
                pl.ValidTo,
                lineCount = pl.Lines.Count
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("integrations")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> ListIntegrations()
    {
        var integrations = await _db.IntegrationConfigs
            .Select(i => new {
                i.Id,
                i.CompanyId,
                i.Transport,
                i.Format,
                i.IsActive,
                i.Schedule,
                i.SimulatedFailurePercent,
                i.SimulatedDelayMs,
                i.LastTransferAt,
                i.LastTransferStatus
            })
            .ToListAsync();

        return Ok(integrations);
    }

    [HttpPut("companies/{companyId}/ui-config")]
    public async Task<IActionResult> UpdateUiConfig(Guid companyId, [FromBody] UpdateUiConfigRequest req)
    {
        var config = await _db.UiConfigurations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (config == null)
        {
            config = new B2BShop.Domain.Entities.CompanyUiConfiguration
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CustomLabelsJson = "{}",
                EnabledSectionsJson = "[]"
            };
            _db.UiConfigurations.Add(config);
        }

        if (req.LogoUrl != null) config.LogoUrl = req.LogoUrl;
        if (req.PrimaryColor != null) config.PrimaryColor = req.PrimaryColor;
        if (req.SecondaryColor != null) config.SecondaryColor = req.SecondaryColor;
        if (req.CatalogLayout != null) config.CatalogLayout = req.CatalogLayout;
        if (req.ShowStockLevel.HasValue) config.ShowStockLevel = req.ShowStockLevel.Value;
        if (req.ShowProductCode.HasValue) config.ShowProductCode = req.ShowProductCode.Value;
        if (req.CustomLabels != null) config.CustomLabelsJson = JsonSerializer.Serialize(req.CustomLabels);
        if (req.EnabledSections != null) config.EnabledSectionsJson = JsonSerializer.Serialize(req.EnabledSections);

        await _db.SaveChangesAsync();
        return Ok(config);
    }

    [HttpPut("companies/{companyId}/checkout-config")]
    public async Task<IActionResult> UpdateCheckoutConfig(Guid companyId, [FromBody] UpdateCheckoutConfigRequest req)
    {
        var config = await _db.CheckoutConfigs
            .Include(c => c.Steps)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (config == null)
        {
            config = new B2BShop.Domain.Entities.CompanyCheckoutConfig
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PaymentTermsJson = "[]"
            };
            _db.CheckoutConfigs.Add(config);
        }

        if (req.RequiresApproval.HasValue) config.RequiresApproval = req.RequiresApproval.Value;
        if (req.ApprovalSpendThreshold.HasValue) config.ApprovalSpendThreshold = req.ApprovalSpendThreshold.Value;
        if (req.AllowFreeAddressEntry.HasValue) config.AllowFreeAddressEntry = req.AllowFreeAddressEntry.Value;
        if (req.RequirePoNumber.HasValue) config.RequirePoNumber = req.RequirePoNumber.Value;
        if (req.PaymentTerms != null) config.PaymentTermsJson = JsonSerializer.Serialize(req.PaymentTerms);

        await _db.SaveChangesAsync();
        return Ok(config);
    }

    [HttpGet("audit-log")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(entityType))
            q = q.Where(a => a.EntityType == entityType);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }
}

public class UpdateUiConfigRequest
{
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? CatalogLayout { get; set; }
    public bool? ShowStockLevel { get; set; }
    public bool? ShowProductCode { get; set; }
    public Dictionary<string, string>? CustomLabels { get; set; }
    public List<string>? EnabledSections { get; set; }
}

public class UpdateCheckoutConfigRequest
{
    public bool? RequiresApproval { get; set; }
    public decimal? ApprovalSpendThreshold { get; set; }
    public bool? AllowFreeAddressEntry { get; set; }
    public bool? RequirePoNumber { get; set; }
    public List<string>? PaymentTerms { get; set; }
}
