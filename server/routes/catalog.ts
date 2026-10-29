import { Hono } from 'hono'
import { camel, camelAll, query, queryOne, type Row } from '../db.js'
import { getCompanyId, requireAuth } from '../auth.js'
import { calculatePrice, type PriceContext } from '../pricing.js'

/** Loads everything the pricing engines need for a company (shared with order creation). */
export async function loadPriceData(companyId: string) {
  const now = new Date()
  const [lists, lines, tiers, contracts, promotions, rates, taxRules] = await Promise.all([
    query(`SELECT * FROM "PriceLists" WHERE "CompanyId" = $1 AND "IsActive" = TRUE`, [companyId]),
    query(`SELECT l.* FROM "PriceListLines" l JOIN "PriceLists" p ON p."Id" = l."PriceListId" WHERE p."CompanyId" = $1 AND p."IsActive" = TRUE`, [companyId]),
    query(`SELECT t.* FROM "VolumePriceTiers" t JOIN "PriceLists" p ON p."Id" = t."PriceListId" WHERE p."CompanyId" = $1 AND p."IsActive" = TRUE`, [companyId]),
    query(`SELECT * FROM "ContractPrices" WHERE "CompanyId" = $1 AND "ValidFrom" <= $2 AND ("ValidTo" IS NULL OR "ValidTo" >= $2)`, [companyId, now]),
    query(`SELECT * FROM "PromotionalDiscounts" WHERE ("CompanyId" = $1 OR "CompanyId" IS NULL) AND "IsActive" = TRUE AND "ValidFrom" <= $2 AND ("ValidTo" IS NULL OR "ValidTo" >= $2)`, [companyId, now]),
    query(`SELECT * FROM "CurrencyRates" WHERE "EffectiveFrom" <= $1 AND ("EffectiveTo" IS NULL OR "EffectiveTo" >= $1)`, [now]),
    query(`SELECT * FROM "TaxRules" WHERE ("CompanyId" = $1 OR "CompanyId" IS NULL) AND "IsActive" = TRUE`, [companyId]),
  ])
  const priceLists = lists.map((pl) => ({
    ...pl,
    lines: lines.filter((l) => l.PriceListId === pl.Id),
    volumeTiers: tiers.filter((t) => t.PriceListId === pl.Id),
  }))
  return { priceLists, contractPrices: contracts, volumeTiers: tiers, promotions, currencyRates: rates, taxRules }
}

type PriceData = Awaited<ReturnType<typeof loadPriceData>>

// Per-instance cache, 5 minutes — mirrors the in-memory cache of the .NET service
const priceCache = new Map<string, { at: number; data: PriceData }>()
async function cachedPriceData(companyId: string): Promise<PriceData> {
  const hit = priceCache.get(companyId)
  if (hit && Date.now() - hit.at < 5 * 60_000) return hit.data
  const data = await loadPriceData(companyId)
  priceCache.set(companyId, { at: Date.now(), data })
  return data
}

function getVisibility(article: Row, productAssignments: Row[], categoryAssignments: Row[], now: Date) {
  const within = (a: Row) =>
    (a.EffectiveFrom === null || new Date(a.EffectiveFrom) <= now) && (a.EffectiveTo === null || new Date(a.EffectiveTo) >= now)

  const individual = productAssignments.find((a) => a.ArticleId === article.Id && within(a))
  if (individual) {
    if (individual.IsExcluded) return { isVisible: false, isOrderable: false }
    return { isVisible: individual.IsVisible, isOrderable: individual.IsOrderable && individual.IsVisible }
  }
  const cat = categoryAssignments.find((a) => a.CategoryId === article.CategoryId && within(a))
  if (cat) {
    if (cat.IsExcluded) return { isVisible: false, isOrderable: false }
    return { isVisible: cat.IsVisible, isOrderable: cat.IsVisible }
  }
  return { isVisible: false, isOrderable: false } // default deny
}

const parseJson = (s: string | null, fallback: any) => {
  try {
    return s ? JSON.parse(s) : fallback
  } catch {
    return fallback
  }
}

function toPriceDto(r: ReturnType<typeof calculatePrice>) {
  return r
}

async function getCompany(companyId: string) {
  // global query filter: deactivated companies are hidden
  const company = await queryOne(`SELECT * FROM "Companies" WHERE "Id" = $1 AND "DeactivatedAt" IS NULL`, [companyId])
  if (!company) throw new Error('Company not found')
  const ui = await queryOne(`SELECT * FROM "UiConfigurations" WHERE "CompanyId" = $1`, [companyId])
  return { company, ui }
}

export const catalog = new Hono()
catalog.use('*', requireAuth)

catalog.get('/', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)

  const q = c.req.query()
  const categoryId = q.categoryId || null
  const search = q.search?.trim() ? q.search : null
  const page = parseInt(q.page ?? '1', 10) || 1
  const pageSize = parseInt(q.pageSize ?? '24', 10) || 24
  const sortBy = q.sortBy ?? null

  const { company, ui } = await getCompany(companyId)
  const priceData = await cachedPriceData(companyId)
  const now = new Date()

  const [productAssignments, categoryAssignments, articles] = await Promise.all([
    query(`SELECT * FROM "ProductAssignments" WHERE "CompanyId" = $1`, [companyId]),
    query(`SELECT * FROM "CategoryAssignments" WHERE "CompanyId" = $1`, [companyId]),
    query(
      `SELECT a.*, c."Name" AS "CategoryName" FROM "Articles" a LEFT JOIN "Categories" c ON c."Id" = a."CategoryId"
       WHERE a."IsActive" = TRUE
         AND ($1::uuid IS NULL OR a."CategoryId" = $1::uuid)
         AND ($2::text IS NULL OR a."Name" ILIKE '%' || $2 || '%' OR a."Code" ILIKE '%' || $2 || '%')`,
      [categoryId, search],
    ),
  ])

  const visible = articles
    .map((a) => ({ a, vis: getVisibility(a, productAssignments, categoryAssignments, now) }))
    .filter((x) => x.vis.isVisible)

  const byName = (x: any, y: any) => (x.a.Name < y.a.Name ? -1 : x.a.Name > y.a.Name ? 1 : 0)
  const sorted =
    sortBy === 'price_asc' ? visible.sort((x, y) => x.a.BasePrice - y.a.BasePrice)
    : sortBy === 'price_desc' ? visible.sort((x, y) => y.a.BasePrice - x.a.BasePrice)
    : sortBy === 'code' ? visible.sort((x, y) => (x.a.Code < y.a.Code ? -1 : 1))
    : visible.sort(byName)

  const total = sorted.length
  const paged = sorted.slice((page - 1) * pageSize, page * pageSize)

  const ids = paged.map((x) => x.a.Id)
  const stock = ui?.ShowStockLevel && ids.length
    ? await query(`SELECT "ArticleId", SUM("Quantity" - "ReservedQuantity")::int AS avail FROM "StockLevels" WHERE "ArticleId" = ANY($1) GROUP BY "ArticleId"`, [ids])
    : []

  const items = paged.map(({ a, vis }) => ({
    articleId: a.Id,
    code: a.Code,
    name: a.Name,
    description: null,
    categoryId: a.CategoryId,
    categoryName: a.CategoryName ?? '',
    unitOfMeasure: a.UnitOfMeasure,
    packSize: a.PackSize,
    imageUrl: a.ImageUrl,
    isOrderable: vis.isOrderable,
    stockLevel: ui?.ShowStockLevel ? (stock.find((s) => s.ArticleId === a.Id)?.avail ?? 0) : null,
    resolvedPrice: toPriceDto(calculatePrice({ article: a, company, quantity: 1, ...priceData } as PriceContext)),
    attributes: parseJson(a.AttributesJson, {}),
    variants: null,
  }))

  return c.json({ items, total, page, pageSize, totalPages: Math.ceil(total / pageSize) })
})

catalog.get('/categories', async (c) => {
  const rows = await query(`SELECT "Id","Code","Name","ParentId","SortOrder" FROM "Categories" WHERE "IsActive" = TRUE ORDER BY "SortOrder"`)
  return c.json(camelAll(rows))
})

catalog.get('/search', async (c) => {
  const q = c.req.query('q')
  if (!q || !q.trim()) return c.json([])
  const rows = await query(
    `SELECT "Id","Code","Name","CategoryId" FROM "Articles"
     WHERE "IsActive" = TRUE AND (strpos("Name", $1) > 0 OR strpos("Code", $1) > 0) LIMIT 20`,
    [q],
  )
  return c.json(camelAll(rows))
})

catalog.get('/:articleId/related', async (c) => {
  const article = await queryOne(`SELECT * FROM "Articles" WHERE "Id" = $1`, [c.req.param('articleId')])
  if (!article) return c.json({ message: 'Not found' }, 404)
  const rows = await query(
    `SELECT "Id","Code","Name","ImageUrl","BasePrice","BaseCurrencyCode" FROM "Articles"
     WHERE "CategoryId" = $1 AND "Id" <> $2 AND "IsActive" = TRUE LIMIT 8`,
    [article.CategoryId, article.Id],
  )
  return c.json(camelAll(rows))
})

catalog.get('/:articleId', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const articleId = c.req.param('articleId')

  const company = await queryOne(`SELECT * FROM "Companies" WHERE "Id" = $1 AND "DeactivatedAt" IS NULL`, [companyId])
  if (!company) return c.json({ message: 'Not found' }, 404)
  const ui = await queryOne(`SELECT * FROM "UiConfigurations" WHERE "CompanyId" = $1`, [companyId])
  const article = await queryOne(
    `SELECT a.*, c."Name" AS "CategoryName" FROM "Articles" a LEFT JOIN "Categories" c ON c."Id" = a."CategoryId"
     WHERE a."Id" = $1 AND a."IsActive" = TRUE`,
    [articleId],
  )
  if (!article) return c.json({ message: 'Not found' }, 404)

  const [variants, stock, priceData] = await Promise.all([
    query(`SELECT * FROM "ArticleVariants" WHERE "ArticleId" = $1`, [articleId]),
    ui?.ShowStockLevel
      ? queryOne(`SELECT COALESCE(SUM("Quantity" - "ReservedQuantity"),0)::int AS avail FROM "StockLevels" WHERE "ArticleId" = $1`, [articleId])
      : Promise.resolve(null),
    cachedPriceData(companyId),
  ])

  return c.json({
    articleId: article.Id,
    code: article.Code,
    name: article.Name,
    description: article.Description,
    categoryId: article.CategoryId,
    categoryName: article.CategoryName ?? '',
    unitOfMeasure: article.UnitOfMeasure,
    packSize: article.PackSize,
    imageUrl: article.ImageUrl,
    isOrderable: true,
    stockLevel: ui?.ShowStockLevel ? (stock?.avail ?? 0) : null,
    resolvedPrice: toPriceDto(calculatePrice({ article, company, quantity: 1, ...priceData } as PriceContext)),
    attributes: parseJson(article.AttributesJson, {}),
    variants: variants.map((v) => ({
      id: v.Id,
      articleId: v.ArticleId,
      variantCode: v.VariantCode,
      extraPrice: v.ExtraPrice,
      stock: v.Stock,
    })),
  })
})
