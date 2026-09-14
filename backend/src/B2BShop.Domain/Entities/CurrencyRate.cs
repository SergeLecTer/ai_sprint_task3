namespace B2BShop.Domain.Entities;

public class CurrencyRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int DecimalPlaces { get; set; } = 2;

    // Hidden rule: RoundingMode affects whether individual lines are rounded or total-per-order
    // "PerLine" | "PerOrder" — default "PerLine" matches legacy engine, "PerOrder" is v2 default
    public string RoundingMode { get; set; } = "PerLine";
    public MidpointRounding MidpointRoundingRule { get; set; } = MidpointRounding.AwayFromZero;

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
