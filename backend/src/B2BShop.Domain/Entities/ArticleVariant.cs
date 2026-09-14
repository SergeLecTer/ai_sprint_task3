namespace B2BShop.Domain.Entities;

public class ArticleVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}";
    public decimal ExtraPrice { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;

    public Article? Article { get; set; }
}
