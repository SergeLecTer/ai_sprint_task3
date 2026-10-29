import type { Row } from './db.js'

export interface TierInfo {
  minQuantity: number
  maxQuantity: number | null
  unitPrice: number
  label?: string | null
}

export interface PriceResult {
  unitPrice: number
  displayPrice: number
  originalPrice: number
  currencyCode: string
  taxRate: number
  taxAmount: number
  totalWithTax: number
  priceType: string
  appliedTiers: TierInfo[]
  appliedPromotions: string[]
  validUntil: Date | null
}

export interface PriceContext {
  article: Row
  variantId?: string | null
  company: Row
  quantity: number
  /** price lists, each with `lines` attached */
  priceLists: Row[]
  contractPrices: Row[]
  volumeTiers: Row[]
  promotions: Row[]
  currencyRates: Row[]
  taxRules: Row[]
  orderQuantitiesByArticle?: Map<string, number>
  asOf?: Date
}

/** Math.Round(x, d, MidpointRounding.AwayFromZero) */
export function roundAway(x: number, d: number): number {
  const f = 10 ** d
  const y = Math.abs(x) * f
  return (Math.sign(x) * Math.round(y + 1e-9)) / f
}

/** Math.Round(x, d) — .NET default is banker's rounding (ToEven) */
export function roundEven(x: number, d: number): number {
  const f = 10 ** d
  const y = x * f
  const fl = Math.floor(y)
  const diff = y - fl
  if (Math.abs(diff - 0.5) < 1e-9) return (fl % 2 === 0 ? fl : fl + 1) / f
  return Math.round(y) / f
}

const ts = (d: Date | null | undefined) => (d ? new Date(d).getTime() : null)
const validAt = (from: Date | null, to: Date | null, asOf: Date) =>
  (from === null || new Date(from).getTime() <= asOf.getTime()) &&
  (to === null || new Date(to).getTime() >= asOf.getTime())

export function calculatePrice(ctx: PriceContext): PriceResult {
  return ctx.company.PricingEngineVersion === 'legacy' ? legacy(ctx) : v2(ctx)
}

// ---------------------------------------------------------------------------
// Legacy engine — deliberately bug-compatible with LegacyPricingEngine.cs
// ---------------------------------------------------------------------------
function legacy(ctx: PriceContext): PriceResult {
  const { article, company, quantity } = ctx
  const asOf = ctx.asOf ?? new Date()

  let unitPrice: number = article.BasePrice
  let priceType = 'list'
  let currency: string = article.BaseCurrencyCode

  // first matching list, NOT ordered by priority
  for (const pl of ctx.priceLists) {
    if (!(pl.IsActive && validAt(pl.ValidFrom, pl.ValidTo, asOf))) continue
    const line = (pl.lines as Row[]).find((l) => l.ArticleId === article.Id)
    if (line) {
      unitPrice = line.UnitPrice
      currency = line.CurrencyCode
      priceType = 'company'
      if (line.DiscountPercent != null && line.DiscountPercent > 0) {
        unitPrice = unitPrice * (1 - line.DiscountPercent / 100)
      }
      break
    }
  }

  const contract = ctx.contractPrices.find(
    (c) => c.ArticleId === article.Id && validAt(c.ValidFrom, c.ValidTo, asOf),
  )
  if (contract && contract.ContractUnitPrice < unitPrice) {
    unitPrice = contract.ContractUnitPrice
    currency = contract.CurrencyCode
    priceType = 'contract'
  }

  // exclusive MaxQuantity (> instead of >=)
  const tiers = ctx.volumeTiers
    .filter(
      (t) =>
        t.ArticleId === article.Id &&
        t.MinQuantity <= quantity &&
        (t.MaxQuantity === null || t.MaxQuantity > quantity),
    )
    .sort((a, b) => b.MinQuantity - a.MinQuantity)
  if (tiers.length > 0 && priceType !== 'contract') {
    const tier = tiers[0]
    unitPrice = tier.IsPercentageDiscount ? unitPrice * (1 - tier.UnitPrice / 100) : tier.UnitPrice
    priceType = 'volume'
  }

  // no promotions in legacy

  if (article.BaseCurrencyCode !== company.CurrencyCode) {
    const rate = ctx.currencyRates.find(
      (r) => r.FromCurrency === article.BaseCurrencyCode && r.ToCurrency === company.CurrencyCode,
    )
    if (rate) {
      unitPrice = roundEven(unitPrice * rate.Rate, 2)
      currency = company.CurrencyCode
    }
  }

  const taxRule = ctx.taxRules.find((t) => t.CountryCode === company.CountryCode && t.IsActive)
  const taxRate: number = taxRule?.TaxRate ?? 0
  const taxAmount = unitPrice * taxRate
  const totalWithTax = unitPrice + taxAmount

  return {
    unitPrice,
    displayPrice: unitPrice,
    originalPrice: article.BasePrice,
    currencyCode: currency,
    taxRate,
    taxAmount: roundEven(taxAmount, 2),
    totalWithTax: roundEven(totalWithTax, 2),
    priceType,
    appliedTiers: [],
    appliedPromotions: [],
    validUntil: null,
  }
}

// ---------------------------------------------------------------------------
// V2 engine — 7 layers, port of PricingEngineV2.cs
// ---------------------------------------------------------------------------
function convertCurrency(amount: number, from: string, to: string, rates: Row[], asOf: Date): number {
  if (from === to) return amount
  const rate = rates
    .filter((r) => r.FromCurrency === from && r.ToCurrency === to && validAt(r.EffectiveFrom, r.EffectiveTo, asOf))
    .sort((a, b) => ts(b.EffectiveFrom)! - ts(a.EffectiveFrom)!)[0]
  if (!rate) {
    const reverse = rates.find((r) => r.FromCurrency === to && r.ToCurrency === from)
    if (reverse) return amount / reverse.Rate
    return amount // silent failure, as in the original
  }
  return amount * rate.Rate
}

function v2(ctx: PriceContext): PriceResult {
  const asOf = ctx.asOf ?? new Date()
  const { article, company, quantity } = ctx

  // Layer 1
  let unitPrice: number = article.BasePrice
  let currency: string = article.BaseCurrencyCode
  let priceType = 'list'
  let validUntil: Date | null = null
  const appliedTiers: TierInfo[] = []
  const appliedPromotions: string[] = []

  // Layer 2: highest-priority active list
  const lists = ctx.priceLists
    .filter((pl) => pl.IsActive && validAt(pl.ValidFrom, pl.ValidTo, asOf))
    .sort((a, b) => b.Priority - a.Priority || ts(b.ValidFrom)! - ts(a.ValidFrom)!)
  for (const pl of lists) {
    const line = (pl.lines as Row[]).find(
      (l) => l.ArticleId === article.Id && (!ctx.variantId || l.VariantId === ctx.variantId || l.VariantId === null),
    )
    if (line) {
      unitPrice = line.UnitPrice
      currency = line.CurrencyCode
      priceType = 'company'
      validUntil = pl.ValidTo
      break
    }
  }

  // Layer 3: cheapest active contract wins
  const contracts = ctx.contractPrices
    .filter((c) => c.ArticleId === article.Id && validAt(c.ValidFrom, c.ValidTo, asOf))
    .sort((a, b) => a.ContractUnitPrice - b.ContractUnitPrice)
  let appliedContract: Row | null = null
  if (contracts.length > 0) {
    appliedContract = contracts[0]
    unitPrice = convertCurrency(appliedContract.ContractUnitPrice, appliedContract.CurrencyCode, company.CurrencyCode, ctx.currencyRates, asOf)
    currency = company.CurrencyCode
    priceType = 'contract'
    validUntil = appliedContract.ValidTo
  }

  // Layer 4: volume tiers (inclusive)
  let effectiveQuantity = quantity
  const hasAggregate = ctx.volumeTiers.some((t) => t.ArticleId === article.Id && t.AggregateAcrossLines)
  const totalQty = ctx.orderQuantitiesByArticle?.get(article.Id)
  if (hasAggregate && totalQty !== undefined) effectiveQuantity = totalQty + quantity

  const applicable = ctx.volumeTiers
    .filter(
      (t) =>
        (t.ArticleId === article.Id || t.ArticleId === null) &&
        t.MinQuantity <= effectiveQuantity &&
        (t.MaxQuantity === null || t.MaxQuantity >= effectiveQuantity),
    )
    .sort((a, b) => b.MinQuantity - a.MinQuantity)
  if (applicable.length > 0) {
    const best = applicable[0]
    const tierPrice = best.IsPercentageDiscount ? unitPrice * (1 - best.UnitPrice / 100) : best.UnitPrice
    if (appliedContract === null || appliedContract.AllowVolumeTierOverride) {
      unitPrice = tierPrice
      priceType = 'volume'
    }
    for (const t of ctx.volumeTiers
      .filter((t) => t.ArticleId === article.Id || t.ArticleId === null)
      .sort((a, b) => a.MinQuantity - b.MinQuantity)) {
      appliedTiers.push({
        minQuantity: t.MinQuantity,
        maxQuantity: t.MaxQuantity,
        unitPrice: t.IsPercentageDiscount ? article.BasePrice * (1 - t.UnitPrice / 100) : t.UnitPrice,
        label: null,
      })
    }
  }

  // Layer 5: order-total threshold promotions (per-line estimate)
  const estimatedOrderTotal = unitPrice * quantity
  const orderTotal = ctx.promotions
    .filter(
      (p) =>
        p.DiscountType === 'OrderTotal' &&
        p.IsActive &&
        validAt(p.ValidFrom, p.ValidTo, asOf) &&
        p.MinOrderTotal != null &&
        estimatedOrderTotal >= p.MinOrderTotal,
    )
    .sort((a, b) => b.DiscountPercent - a.DiscountPercent)[0]
  if (orderTotal) {
    unitPrice = unitPrice * (1 - orderTotal.DiscountPercent / 100)
    appliedPromotions.push(orderTotal.Code)
    if (priceType === 'list' || priceType === 'company') priceType = 'promotional'
  }

  // Layer 6: article promotions
  const articlePromos = ctx.promotions
    .filter((p) => p.DiscountType === 'Article' && p.ArticleId === article.Id && p.IsActive && validAt(p.ValidFrom, p.ValidTo, asOf))
    .sort((a, b) => b.DiscountPercent - a.DiscountPercent)
  for (const promo of articlePromos) {
    if (!promo.IsStackable && appliedPromotions.length > 0) break
    unitPrice = unitPrice * (1 - promo.DiscountPercent / 100)
    appliedPromotions.push(promo.Code)
    priceType = 'promotional'
  }

  // Layer 7: currency, rounding, tax
  let displayPrice = unitPrice
  if (currency !== company.CurrencyCode) {
    displayPrice = convertCurrency(unitPrice, currency, company.CurrencyCode, ctx.currencyRates, asOf)
    currency = company.CurrencyCode
  }

  const rateConfig = ctx.currencyRates.find((r) => r.FromCurrency === currency && r.ToCurrency === company.CurrencyCode)
  const roundingMode: string = rateConfig?.RoundingMode ?? 'PerLine'
  const decimalPlaces: number = rateConfig?.DecimalPlaces ?? 2
  if (roundingMode === 'PerLine') displayPrice = roundAway(displayPrice, decimalPlaces)

  const taxRule = ctx.taxRules
    .filter(
      (t) =>
        (t.CompanyId === company.Id || t.CompanyId === null) &&
        (t.CountryCode === company.CountryCode || !t.CountryCode) &&
        t.IsActive,
    )
    .sort((a, b) => Number(b.CompanyId !== null) - Number(a.CompanyId !== null))[0]
  const taxRate: number = taxRule?.TaxRate ?? 0
  const taxIncluded: boolean = taxRule?.IsIncludedInPrice ?? false

  let taxAmount = 0
  let totalWithTax = displayPrice
  if (taxRate > 0) {
    if (taxIncluded) {
      taxAmount = displayPrice - displayPrice / (1 + taxRate)
    } else {
      taxAmount = displayPrice * taxRate
      totalWithTax = displayPrice + taxAmount
    }
  }

  return {
    unitPrice: displayPrice,
    displayPrice,
    originalPrice: article.BasePrice,
    currencyCode: currency,
    taxRate,
    taxAmount: roundAway(taxAmount, decimalPlaces),
    totalWithTax: roundAway(totalWithTax, decimalPlaces),
    priceType,
    appliedTiers,
    appliedPromotions,
    validUntil,
  }
}
