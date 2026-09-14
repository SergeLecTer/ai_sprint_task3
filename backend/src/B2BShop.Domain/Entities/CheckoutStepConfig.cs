namespace B2BShop.Domain.Entities;

public class CheckoutStepConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CheckoutConfigId { get; set; }
    public string StepType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }
    // JSON additional config per step type
    public string ConfigJson { get; set; } = "{}";

    public CompanyCheckoutConfig? CheckoutConfig { get; set; }
}
