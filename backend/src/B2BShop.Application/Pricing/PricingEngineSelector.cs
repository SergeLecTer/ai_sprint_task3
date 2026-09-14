using B2BShop.Domain.Entities;

namespace B2BShop.Application.Pricing;

/// <summary>
/// Selects the appropriate pricing engine based on the per-company DB flag.
/// The flag is in Company.PricingEngineVersion ("legacy" | "v2").
/// </summary>
public class PricingEngineSelector
{
    private readonly PricingEngineV2 _v2 = new();
#pragma warning disable CS0618
    private readonly LegacyPricingEngine _legacy = new();
#pragma warning restore CS0618

    public PriceResult Calculate(PriceCalculationContext ctx)
    {
        return ctx.Company.PricingEngineVersion == "legacy"
            ? _legacy.Calculate(ctx)
            : _v2.Calculate(ctx);
    }
}
