namespace B2BShop.Domain.Entities;

public class CompanyPriceList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    // Priority when multiple price lists are active — higher wins
    // Hidden rule: when two lists have equal priority, the one with more recent ValidFrom wins
    public int Priority { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public Company? Company { get; set; }
    public ICollection<PriceListLine> Lines { get; set; } = new List<PriceListLine>();
    public ICollection<VolumePriceTier> VolumeTiers { get; set; } = new List<VolumePriceTier>();
}
