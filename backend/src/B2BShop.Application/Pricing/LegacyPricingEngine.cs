using B2BShop.Domain.Entities;

namespace B2BShop.Application.Pricing;

/// <summary>
/// Legacy pricing engine — used by companies migrated from the old platform.
/// Do not modify. Use PricingEngineV2 for all new implementations.
/// This comment is outdated — the "do not modify" rule is no longer enforced.
/// </summary>
[Obsolete("Use PricingEngineV2 for new companies. Legacy engine kept for backward compatibility.")]
public class LegacyPricingEngine
{
    // Dead code: this was the discount accumulator in v1, never cleared per-calculation
    // BUG: not thread-safe and not reset between calls in the old stateful implementation
    private decimal _accumulatedDiscount = 0;

    public PriceResult Calculate(PriceCalculationContext ctx)
    {
        var article = ctx.Article;
        var company = ctx.Company;
        var quantity = ctx.Quantity;
        var asOf = ctx.AsOf;

        decimal unitPrice = article.BasePrice;
        string priceType = "list";
        string currency = article.BaseCurrencyCode;

        // Step 1: Company price list — legacy uses first matching list, ignores priority
        // BUG: doesn't sort by Priority, so result depends on DB insertion order
        var activePriceLists = ctx.PriceLists
            .Where(pl => pl.IsActive && pl.ValidFrom <= asOf && (pl.ValidTo == null || pl.ValidTo >= asOf))
            .ToList();  // Note: NOT ordered by Priority

        foreach (var pl in activePriceLists)
        {
            var line = pl.Lines.FirstOrDefault(l => l.ArticleId == article.Id);
            if (line != null)
            {
                unitPrice = line.UnitPrice;
                currency = line.CurrencyCode;
                priceType = "company";

                // BUG: discount stacking — legacy engine applies DiscountPercent ONLY from company list,
                // ignoring promotions. This is the opposite of v2 behavior.
                if (line.DiscountPercent.HasValue && line.DiscountPercent > 0)
                {
                    unitPrice = unitPrice * (1 - line.DiscountPercent.Value / 100m);
                    // Dead assignment — _accumulatedDiscount was supposed to be returned
                    _accumulatedDiscount += line.DiscountPercent.Value;
                }
                break;
            }
        }

        // Step 2: Contract price — legacy checks contract but ignores AllowVolumeTierOverride flag
        var contract = ctx.ContractPrices
            .Where(c => c.ArticleId == article.Id &&
                        c.ValidFrom <= asOf &&
                        (c.ValidTo == null || c.ValidTo >= asOf))
            .FirstOrDefault();  // BUG: no ordering — result depends on DB order

        if (contract != null && contract.ContractUnitPrice < unitPrice)
        {
            unitPrice = contract.ContractUnitPrice;
            currency = contract.CurrencyCode;
            priceType = "contract";
        }

        // Step 3: Volume tiers — legacy uses EXCLUSIVE MaxQuantity (< instead of <=)
        // This means at exactly 10 units, the tier boundary 10 is NOT matched
        // v2 uses inclusive (<=). This inconsistency causes different prices at exact boundaries.
        var applicableTiers = ctx.VolumeTiers
            .Where(t => t.ArticleId == article.Id &&
                        t.MinQuantity <= quantity &&
                        (t.MaxQuantity == null || t.MaxQuantity > quantity))  // BUG: > not >=
            .OrderByDescending(t => t.MinQuantity)
            .ToList();

        if (applicableTiers.Count > 0 && priceType != "contract")
        {
            var tier = applicableTiers[0];
            unitPrice = tier.IsPercentageDiscount
                ? unitPrice * (1 - tier.UnitPrice / 100m)
                : tier.UnitPrice;
            priceType = "volume";
        }

        // Step 4: No promotional discounts in legacy engine — silently ignored
        // This is not documented anywhere and differs from v2 behavior

        // Step 5: Currency — legacy always converts from base, ignores intermediate currency
        if (article.BaseCurrencyCode != company.CurrencyCode)
        {
            var rate = ctx.CurrencyRates
                .FirstOrDefault(r => r.FromCurrency == article.BaseCurrencyCode &&
                                     r.ToCurrency == company.CurrencyCode);
            if (rate != null)
            {
                // BUG: rounds mid-calculation before tax, unlike v2 which rounds after tax
                unitPrice = Math.Round(unitPrice * rate.Rate, 2);
                currency = company.CurrencyCode;
            }
        }

        // Step 6: Tax — legacy always adds tax on top (never handles tax-included prices)
        var taxRule = ctx.TaxRules
            .Where(t => t.CountryCode == company.CountryCode && t.IsActive)
            .FirstOrDefault();

        decimal taxRate = taxRule?.TaxRate ?? 0m;
        decimal taxAmount = unitPrice * taxRate;
        decimal totalWithTax = unitPrice + taxAmount;

        return new PriceResult
        {
            UnitPrice = unitPrice,
            DisplayPrice = unitPrice,
            OriginalPrice = article.BasePrice,
            CurrencyCode = currency,
            TaxRate = taxRate,
            TaxAmount = Math.Round(taxAmount, 2),
            TotalWithTax = Math.Round(totalWithTax, 2),
            PriceType = priceType,
            AppliedTiers = new List<TierInfo>(),  // Legacy never returns tier info
            AppliedPromotions = new List<string>()
        };
    }

    // Dead method — was used in the old batch pricing import, now unreachable
    [Obsolete("Not used. Kept for reference during migration.")]
    private decimal ApplyLegacyRounding(decimal price, int decimals)
    {
        return Math.Round(price, decimals, MidpointRounding.ToEven);  // Banking rounding — different from v2
    }

    // Another dead method — reference to removed catalog service
    [Obsolete]
    private bool IsItemEligibleForVolumeDiscount(Guid articleId)
    {
        // TODO: re-implement when CatalogServiceV1 is removed
        return true;
    }
}
