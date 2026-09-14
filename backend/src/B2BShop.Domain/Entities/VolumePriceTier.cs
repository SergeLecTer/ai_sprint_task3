namespace B2BShop.Domain.Entities;

public class VolumePriceTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PriceListId { get; set; }
    public Guid? ArticleId { get; set; }

    // Hidden rule: when ArticleId is null, tier applies to all items in the same product group
    public Guid? ProductGroupId { get; set; }

    // Hidden rule: tier applies when quantity >= MinQuantity AND (MaxQuantity is null OR quantity <= MaxQuantity)
    // EXCEPT: legacy engine uses strictly less-than for MaxQuantity boundary
    public int MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Hidden rule: when IsPercentageDiscount = true, UnitPrice is treated as % off base
    public bool IsPercentageDiscount { get; set; } = false;

    // Hidden rule: AggregateAcrossLines = true means qty is summed per product group across all order lines
    // Default false = per individual line
    public bool AggregateAcrossLines { get; set; } = false;

    public CompanyPriceList? PriceList { get; set; }
    public Article? Article { get; set; }
}
