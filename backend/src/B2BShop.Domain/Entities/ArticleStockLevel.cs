namespace B2BShop.Domain.Entities;

public class ArticleStockLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid? VariantId { get; set; }
    public string WarehouseCode { get; set; } = "DEFAULT";
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int AvailableQuantity => Quantity - ReservedQuantity;

    public Article? Article { get; set; }
}
