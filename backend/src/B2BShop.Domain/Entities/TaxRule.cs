namespace B2BShop.Domain.Entities;

public class TaxRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CountryCode { get; set; } = string.Empty;
    public string? RegionCode { get; set; }
    public string? ProductGroupCode { get; set; }
    public decimal TaxRate { get; set; }
    public string TaxType { get; set; } = "VAT";
    public bool IsIncludedInPrice { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Hidden rule: per-company tax override takes precedence when CompanyId is set
    public Guid? CompanyId { get; set; }
}
