namespace B2BShop.Domain.Entities;

// Product groups are used for volume tier aggregation — not visible in UI
// This is a hidden concept students must discover through pricing behavior
public class ProductGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ArticleProductGroupMembership> Members { get; set; } = new List<ArticleProductGroupMembership>();
}

public class ArticleProductGroupMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid ProductGroupId { get; set; }

    public Article? Article { get; set; }
    public ProductGroup? ProductGroup { get; set; }
}
