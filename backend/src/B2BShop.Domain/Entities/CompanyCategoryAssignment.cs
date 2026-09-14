namespace B2BShop.Domain.Entities;

public class CompanyCategoryAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsExcluded { get; set; } = false;
    public bool IsVisible { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public Company? Company { get; set; }
    public ProductCategory? Category { get; set; }
}
