using B2BShop.Domain.Enums;

namespace B2BShop.Domain.Entities;

// Sometimes called "Account" in old code — intentional inconsistency
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Buyer;
    public Guid? CompanyId { get; set; }

    // SalesRep can act on behalf of a company
    public Guid? ActingOnBehalfOfCompanyId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Approval spending limit — only meaningful for Approver role
    public decimal? ApprovalSpendingLimit { get; set; }

    public Company? Company { get; set; }
}
