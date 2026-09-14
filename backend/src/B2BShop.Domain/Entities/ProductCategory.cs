namespace B2BShop.Domain.Entities;

public class ProductCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductCategory? Parent { get; set; }
    public ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<CompanyCategoryAssignment> CompanyAssignments { get; set; } = new List<CompanyCategoryAssignment>();
}
