namespace B2BShop.Domain.Entities;

public class CompanyUiConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }

    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#3b82f6";
    public string SecondaryColor { get; set; } = "#1d4ed8";

    // "grid" | "list" | "table"
    public string CatalogLayout { get; set; } = "grid";
    public bool ShowStockLevel { get; set; } = false;
    public bool ShowProductCode { get; set; } = true;

    // JSON: {"nav.catalog":"Products","btn.addToCart":"Order"}
    public string CustomLabelsJson { get; set; } = "{}";

    // JSON array of enabled section keys
    public string EnabledSectionsJson { get; set; } = "[\"catalog\",\"orders\",\"approvals\",\"lists\",\"quotes\",\"dashboard\"]";

    public string DateFormat { get; set; } = "MM/DD/YYYY";
    public string NumberFormat { get; set; } = "dot";  // "dot" | "comma"

    public Company? Company { get; set; }
}
