using System.Security.Claims;
using System.Text.Json;
using B2BShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2BShop.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompaniesController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCompany()
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var company = await _db.Companies
            .Include(c => c.UiConfig)
            .FirstOrDefaultAsync(c => c.Id == companyId);

        if (company == null) return NotFound();

        return Ok(new {
            company.Id,
            company.Code,
            company.Name,
            company.CurrencyCode,
            company.LanguageCode,
            company.IsActive,
            company.PricingEngineVersion,
            uiConfig = company.UiConfig == null ? null : new {
                company.UiConfig.LogoUrl,
                company.UiConfig.PrimaryColor,
                company.UiConfig.SecondaryColor,
                catalogLayout = company.UiConfig.CatalogLayout,
                showStockLevel = company.UiConfig.ShowStockLevel,
                showProductCode = company.UiConfig.ShowProductCode,
                customLabels = JsonSerializer.Deserialize<Dictionary<string, string>>(company.UiConfig.CustomLabelsJson),
                enabledSections = JsonSerializer.Deserialize<List<string>>(company.UiConfig.EnabledSectionsJson),
                company.UiConfig.DateFormat,
                company.UiConfig.NumberFormat
            }
        });
    }

    [HttpGet("me/ui-config")]
    public async Task<IActionResult> GetUiConfig()
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var uiConfig = await _db.UiConfigurations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (uiConfig == null) return Ok(new {});

        return Ok(new {
            uiConfig.LogoUrl,
            primaryColor = uiConfig.PrimaryColor,
            secondaryColor = uiConfig.SecondaryColor,
            catalogLayout = uiConfig.CatalogLayout,
            showStockLevel = uiConfig.ShowStockLevel,
            showProductCode = uiConfig.ShowProductCode,
            customLabels = JsonSerializer.Deserialize<Dictionary<string, string>>(uiConfig.CustomLabelsJson),
            enabledSections = JsonSerializer.Deserialize<List<string>>(uiConfig.EnabledSectionsJson),
            uiConfig.DateFormat,
            numberFormat = uiConfig.NumberFormat
        });
    }

    [HttpGet("me/addresses")]
    public async Task<IActionResult> GetAddresses()
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var addresses = await _db.DeliveryAddresses
            .Where(a => a.CompanyId == companyId && a.IsActive)
            .ToListAsync();

        return Ok(addresses);
    }

    [HttpGet("me/checkout-config")]
    public async Task<IActionResult> GetCheckoutConfig()
    {
        var companyId = GetCompanyId();
        if (companyId == null) return Unauthorized();

        var config = await _db.CheckoutConfigs
            .Include(c => c.Steps)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (config == null)
        {
            // Default config if none in DB
            return Ok(new {
                companyId,
                steps = new[] {
                    new { stepType = "cart_review", label = "Review", isRequired = true, sortOrder = 1, config = new {} },
                    new { stepType = "delivery_address", label = "Delivery", isRequired = true, sortOrder = 2, config = new {} },
                    new { stepType = "confirmation", label = "Confirm", isRequired = true, sortOrder = 3, config = new {} }
                },
                requiresApproval = false,
                approvalSpendThreshold = (decimal?)null,
                paymentTerms = new[] { "Net 30" },
                allowFreeAddressEntry = false,
                requirePoNumber = false,
                confirmationChannel = "email"
            });
        }

        return Ok(new {
            companyId = config.CompanyId,
            steps = config.Steps.OrderBy(s => s.SortOrder).Select(s => new {
                stepType = s.StepType,
                label = s.Label,
                isRequired = s.IsRequired,
                sortOrder = s.SortOrder,
                config = JsonSerializer.Deserialize<object>(s.ConfigJson)
            }),
            requiresApproval = config.RequiresApproval,
            approvalSpendThreshold = config.ApprovalSpendThreshold,
            paymentTerms = JsonSerializer.Deserialize<List<string>>(config.PaymentTermsJson),
            allowFreeAddressEntry = config.AllowFreeAddressEntry,
            requirePoNumber = config.RequirePoNumber,
            confirmationChannel = config.ConfirmationChannel
        });
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
