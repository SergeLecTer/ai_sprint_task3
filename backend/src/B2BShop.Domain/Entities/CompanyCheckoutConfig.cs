namespace B2BShop.Domain.Entities;

public class CompanyCheckoutConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public bool RequiresApproval { get; set; } = false;
    public decimal? ApprovalSpendThreshold { get; set; }
    public bool AllowFreeAddressEntry { get; set; } = false;
    public bool RequirePoNumber { get; set; } = false;
    public bool AllowSplitDelivery { get; set; } = false;
    // "email" | "webhook" | "none"
    public string ConfirmationChannel { get; set; } = "email";
    // JSON array: ["Net 30","Net 60","Prepayment"]
    public string PaymentTermsJson { get; set; } = "[\"Net 30\"]";

    public Company? Company { get; set; }
    public ICollection<CheckoutStepConfig> Steps { get; set; } = new List<CheckoutStepConfig>();
}
