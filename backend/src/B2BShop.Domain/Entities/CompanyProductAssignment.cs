namespace B2BShop.Domain.Entities;

public class CompanyProductAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ArticleId { get; set; }

    // Hidden rule: IsExcluded=true overrides any category-level inclusion
    // Priority: individual exclusion > individual inclusion > category assignment
    public bool IsExcluded { get; set; } = false;
    public bool IsOrderable { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public Company? Company { get; set; }
    public Article? Article { get; set; }
}
