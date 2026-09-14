namespace B2BShop.Domain.Entities;

// Deliberately named "Company" here but "Account" in some integration code and "Tenant" in config — intentional
public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string CountryCode { get; set; } = "US";
    public string CurrencyCode { get; set; } = "USD";
    public string LanguageCode { get; set; } = "en";
    public bool IsActive { get; set; } = true;

    // DB flag selects which pricing engine to use — the core of hidden pricing behavior
    public string PricingEngineVersion { get; set; } = "v2";

    // Credit limit — used in checkout but value only meaningful with understanding of company config
    public decimal? CreditLimit { get; set; }
    public decimal CurrentCreditUsed { get; set; }

    // Feature flags — some are dead (never checked in current code)
    public bool EnableQuotes { get; set; } = true;
    public bool EnableShoppingLists { get; set; } = true;
    public bool EnableBulkUpload { get; set; } = false;
    // Dead field: was used in v1 integration, now always ignored
    public bool LegacyIntegrationEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }

    public CompanyUiConfiguration? UiConfig { get; set; }
    public CompanyCheckoutConfig? CheckoutConfig { get; set; }
    public ICollection<CompanyPriceList> PriceLists { get; set; } = new List<CompanyPriceList>();
    public ICollection<CompanyProductAssignment> ProductAssignments { get; set; } = new List<CompanyProductAssignment>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<IntegrationConfig> IntegrationConfigs { get; set; } = new List<IntegrationConfig>();
}
