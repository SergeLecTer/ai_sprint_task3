using B2BShop.Domain.Entities;

namespace B2BShop.Application.Pricing;

/// <summary>
/// New pricing engine — 7-layer price resolution.
/// Resolution order: base list -> company price list -> contract price -> volume/tier breaks
///                   -> order-total thresholds -> promotional discounts -> currency/rounding/VAT
/// NOTE: This comment is intentionally accurate. The legacy engine's comment was wrong.
/// </summary>
public class PricingEngineV2
{
    public PriceResult Calculate(PriceCalculationContext ctx)
    {
        var asOf = ctx.AsOf;
        var article = ctx.Article;
        var company = ctx.Company;
        var quantity = ctx.Quantity;

        // Layer 1: Base list price
        decimal unitPrice = article.BasePrice;
        string currency = article.BaseCurrencyCode;
        string priceType = "list";
        DateTime? validUntil = null;
        var appliedTiers = new List<TierInfo>();
        var appliedPromotions = new List<string>();

        // Layer 2: Company price list (highest-priority active list wins)
        var activePriceLists = ctx.PriceLists
            .Where(pl => pl.IsActive && pl.ValidFrom <= asOf && (pl.ValidTo == null || pl.ValidTo >= asOf))
            .OrderByDescending(pl => pl.Priority)
            .ThenByDescending(pl => pl.ValidFrom)
            .ToList();

        PriceListLine? matchedLine = null;
        CompanyPriceList? matchedList = null;
        foreach (var pl in activePriceLists)
        {
            var line = pl.Lines.FirstOrDefault(l => l.ArticleId == article.Id &&
                (ctx.VariantId == null || l.VariantId == ctx.VariantId || l.VariantId == null));
            if (line != null)
            {
                matchedLine = line;
                matchedList = pl;
                unitPrice = line.UnitPrice;
                currency = line.CurrencyCode;
                priceType = "company";
                validUntil = pl.ValidTo;
                break;
            }
        }

        // Layer 3: Contract price — overrides company price list
        var activeContracts = ctx.ContractPrices
            .Where(c => c.ArticleId == article.Id &&
                        c.ValidFrom <= asOf &&
                        (c.ValidTo == null || c.ValidTo >= asOf))
            .OrderBy(c => c.ContractUnitPrice)  // cheapest contract wins
            .ToList();

        ContractPrice? appliedContract = null;
        if (activeContracts.Count > 0)
        {
            appliedContract = activeContracts[0];
            unitPrice = ConvertCurrency(appliedContract.ContractUnitPrice, appliedContract.CurrencyCode, company.CurrencyCode, ctx.CurrencyRates, asOf);
            currency = company.CurrencyCode;
            priceType = "contract";
            validUntil = appliedContract.ValidTo;
        }

        // Layer 4: Volume/tier breaks
        // In v2: volume tiers CAN override contract price when AllowVolumeTierOverride=true
        var effectiveQuantity = quantity;

        // Check if aggregate-across-lines tiers apply
        var aggregateTiers = ctx.VolumeTiers
            .Where(t => t.ArticleId == article.Id && t.AggregateAcrossLines)
            .ToList();

        if (aggregateTiers.Any() && ctx.OrderQuantitiesByArticle.TryGetValue(article.Id, out var totalQty))
        {
            effectiveQuantity = totalQty + quantity;
        }

        // Find best tier for effective quantity (inclusive >= MinQuantity)
        var applicableTiers = ctx.VolumeTiers
            .Where(t => (t.ArticleId == article.Id || t.ArticleId == null) &&
                        t.MinQuantity <= effectiveQuantity &&
                        (t.MaxQuantity == null || t.MaxQuantity >= effectiveQuantity))
            .OrderByDescending(t => t.MinQuantity)
            .ToList();

        if (applicableTiers.Count > 0)
        {
            var bestTier = applicableTiers[0];
            decimal tierPrice = bestTier.IsPercentageDiscount
                ? unitPrice * (1 - bestTier.UnitPrice / 100m)
                : bestTier.UnitPrice;

            bool canApplyTier = appliedContract == null || appliedContract.AllowVolumeTierOverride;
            if (canApplyTier)
            {
                unitPrice = tierPrice;
                priceType = "volume";
            }

            // Always show tiers in result for UI display
            foreach (var tier in ctx.VolumeTiers.Where(t => t.ArticleId == article.Id || t.ArticleId == null).OrderBy(t => t.MinQuantity))
            {
                decimal displayTierPrice = tier.IsPercentageDiscount
                    ? article.BasePrice * (1 - tier.UnitPrice / 100m)
                    : tier.UnitPrice;
                appliedTiers.Add(new TierInfo
                {
                    MinQuantity = tier.MinQuantity,
                    MaxQuantity = tier.MaxQuantity,
                    UnitPrice = displayTierPrice
                });
            }
        }

        // Layer 5: Order-total thresholds (promotional discounts with MinOrderTotal)
        // This is checked per-line using estimated order total — deliberate approximation
        decimal estimatedOrderTotal = unitPrice * quantity;
        var orderTotalDiscounts = ctx.Promotions
            .Where(p => p.DiscountType == "OrderTotal" &&
                        p.IsActive &&
                        p.ValidFrom <= asOf &&
                        (p.ValidTo == null || p.ValidTo >= asOf) &&
                        p.MinOrderTotal.HasValue &&
                        estimatedOrderTotal >= p.MinOrderTotal.Value)
            .OrderByDescending(p => p.DiscountPercent)
            .FirstOrDefault();

        if (orderTotalDiscounts != null)
        {
            unitPrice = unitPrice * (1 - orderTotalDiscounts.DiscountPercent / 100m);
            appliedPromotions.Add(orderTotalDiscounts.Code);
            if (priceType == "list" || priceType == "company") priceType = "promotional";
        }

        // Layer 6: Promotional/campaign discounts (article/category specific)
        var articleDiscounts = ctx.Promotions
            .Where(p => (p.DiscountType == "Article" && p.ArticleId == article.Id) &&
                        p.IsActive &&
                        p.ValidFrom <= asOf &&
                        (p.ValidTo == null || p.ValidTo >= asOf))
            .OrderByDescending(p => p.DiscountPercent)
            .ToList();

        foreach (var promo in articleDiscounts)
        {
            if (!promo.IsStackable && appliedPromotions.Count > 0) break;
            unitPrice = unitPrice * (1 - promo.DiscountPercent / 100m);
            appliedPromotions.Add(promo.Code);
            priceType = "promotional";
        }

        // Layer 7: Currency conversion, rounding, tax
        decimal displayPrice = unitPrice;
        if (currency != company.CurrencyCode)
        {
            displayPrice = ConvertCurrency(unitPrice, currency, company.CurrencyCode, ctx.CurrencyRates, asOf);
            currency = company.CurrencyCode;
        }

        // Determine rounding mode from currency rate settings
        var rateConfig = ctx.CurrencyRates.FirstOrDefault(r => r.FromCurrency == currency && r.ToCurrency == company.CurrencyCode);
        string roundingMode = rateConfig?.RoundingMode ?? "PerLine";
        int decimalPlaces = rateConfig?.DecimalPlaces ?? 2;

        if (roundingMode == "PerLine")
        {
            displayPrice = Math.Round(displayPrice, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        // Tax calculation
        var taxRule = ctx.TaxRules
            .Where(t => (t.CompanyId == company.Id || t.CompanyId == null) &&
                        (t.CountryCode == company.CountryCode || string.IsNullOrEmpty(t.CountryCode)) &&
                        t.IsActive)
            .OrderByDescending(t => t.CompanyId.HasValue)  // company-specific tax overrides generic
            .FirstOrDefault();

        decimal taxRate = taxRule?.TaxRate ?? 0m;
        bool taxIncluded = taxRule?.IsIncludedInPrice ?? false;

        decimal taxAmount = 0;
        decimal totalWithTax = displayPrice;
        if (taxRate > 0)
        {
            if (taxIncluded)
            {
                taxAmount = displayPrice - (displayPrice / (1 + taxRate));
            }
            else
            {
                taxAmount = displayPrice * taxRate;
                totalWithTax = displayPrice + taxAmount;
            }
        }

        return new PriceResult
        {
            UnitPrice = displayPrice,
            DisplayPrice = displayPrice,
            OriginalPrice = article.BasePrice,
            CurrencyCode = currency,
            TaxRate = taxRate,
            TaxAmount = Math.Round(taxAmount, decimalPlaces),
            TotalWithTax = Math.Round(totalWithTax, decimalPlaces),
            PriceType = priceType,
            AppliedTiers = appliedTiers,
            AppliedPromotions = appliedPromotions,
            ValidUntil = validUntil
        };
    }

    private static decimal ConvertCurrency(decimal amount, string from, string to, List<CurrencyRate> rates, DateTime asOf)
    {
        if (from == to) return amount;

        var rate = rates
            .Where(r => r.FromCurrency == from && r.ToCurrency == to &&
                        r.EffectiveFrom <= asOf &&
                        (r.EffectiveTo == null || r.EffectiveTo >= asOf))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        if (rate == null)
        {
            // Fallback: try reverse rate
            var reverseRate = rates
                .Where(r => r.FromCurrency == to && r.ToCurrency == from)
                .FirstOrDefault();
            if (reverseRate != null)
                return amount / reverseRate.Rate;

            // No rate found — return original amount (deliberate silent failure for training)
            return amount;
        }

        return amount * rate.Rate;
    }
}
