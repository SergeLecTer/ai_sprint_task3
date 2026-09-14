namespace B2BShop.Domain.Entities;

public class ShoppingList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public bool IsShared { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShoppingListLine> Lines { get; set; } = new List<ShoppingListLine>();
}

public class ShoppingListLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShoppingListId { get; set; }
    public Guid ArticleId { get; set; }
    public string ArticleCode { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }

    public ShoppingList? ShoppingList { get; set; }
}
