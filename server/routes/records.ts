import { Hono } from 'hono'
import { camel, camelAll, query, queryOne, withTransaction, type Row } from '../db.js'
import { getCompanyId, getUser, requireAuth, requireRoles } from '../auth.js'

const ctx = (c: any) => ({ companyId: getCompanyId(c)!, userId: getUser(c).userId })

// ---------------------------------------------------------------- quotes
const QUOTE_STATUS = ['Draft', 'Submitted', 'Reviewing', 'Accepted', 'Rejected', 'Expired']

const mapQuote = (q: Row, lines: Row[]) => ({
  id: q.Id,
  quoteNumber: q.QuoteNumber,
  companyId: q.CompanyId,
  userId: q.UserId,
  status: QUOTE_STATUS[q.Status].toLowerCase(),
  validUntil: q.ValidUntil,
  notes: q.Notes,
  createdAt: q.CreatedAt,
  updatedAt: q.UpdatedAt,
  lines: lines.map((l) => ({
    id: l.Id,
    articleId: l.ArticleId,
    articleCode: l.ArticleCode,
    articleName: l.ArticleName,
    requestedQuantity: l.RequestedQuantity,
    requestedPrice: l.RequestedPrice,
    offeredPrice: l.OfferedPrice,
    notes: l.Notes,
  })),
})

async function loadQuote(id: string, companyId: string) {
  const q = await queryOne(`SELECT * FROM "Quotes" WHERE "Id" = $1 AND "CompanyId" = $2`, [id, companyId])
  if (!q) return null
  return { q, lines: await query(`SELECT * FROM "QuoteLines" WHERE "QuoteId" = $1`, [id]) }
}

export const quotes = new Hono()
quotes.use('*', requireAuth)

quotes.get('/', async (c) => {
  const { companyId } = ctx(c)
  const status = c.req.query('status')
  const idx = status ? QUOTE_STATUS.findIndex((s) => s.toLowerCase() === status.toLowerCase()) : -1
  const rows = await query(
    `SELECT * FROM "Quotes" WHERE "CompanyId" = $1 ${idx >= 0 ? `AND "Status" = ${idx}` : ''} ORDER BY "CreatedAt" DESC`,
    [companyId],
  )
  const lines = rows.length ? await query(`SELECT * FROM "QuoteLines" WHERE "QuoteId" = ANY($1)`, [rows.map((r) => r.Id)]) : []
  return c.json(rows.map((q) => mapQuote(q, lines.filter((l) => l.QuoteId === q.Id))))
})

quotes.get('/:quoteId', async (c) => {
  const r = await loadQuote(c.req.param('quoteId'), ctx(c).companyId)
  return r ? c.json(mapQuote(r.q, r.lines)) : c.json({ message: 'Not found' }, 404)
})

quotes.post('/', async (c) => {
  const { companyId, userId } = ctx(c)
  const body = (await c.req.json()) as { lines: { articleId: string; quantity: number; requestedPrice?: number; notes?: string }[]; notes?: string }
  const count = (await queryOne(`SELECT COUNT(*)::int n FROM "Quotes"`))!.n
  const articles: Row[] = []
  for (const l of body.lines ?? []) {
    const a = await queryOne(`SELECT * FROM "Articles" WHERE "Id" = $1`, [l.articleId])
    if (!a) return c.json({ message: `Article ${l.articleId} not found` }, 400)
    articles.push(a)
  }
  const now = new Date()
  const number = `QUO-${now.getUTCFullYear()}${String(now.getUTCMonth() + 1).padStart(2, '0')}-${String(count + 1).padStart(4, '0')}`
  const result = await withTransaction(async (cl) => {
    const { rows } = await cl.query(
      `INSERT INTO "Quotes" ("Id","QuoteNumber","CompanyId","UserId","Status","ValidUntil","Notes","CreatedAt","UpdatedAt")
       VALUES (gen_random_uuid(),$1,$2,$3,0,NOW() + INTERVAL '30 days',$4,NOW(),NOW()) RETURNING *`,
      [number, companyId, userId, body.notes ?? null],
    )
    const lines: Row[] = []
    for (let i = 0; i < articles.length; i++) {
      const l = body.lines[i]
      const { rows: lr } = await cl.query(
        `INSERT INTO "QuoteLines" ("Id","QuoteId","ArticleId","ArticleCode","ArticleName","RequestedQuantity","RequestedPrice","Notes")
         VALUES (gen_random_uuid(),$1,$2,$3,$4,$5,$6,$7) RETURNING *`,
        [rows[0].Id, l.articleId, articles[i].Code, articles[i].Name, l.quantity, l.requestedPrice ?? null, l.notes ?? null],
      )
      lines.push(lr[0])
    }
    return { q: rows[0], lines }
  })
  return c.json(mapQuote(result.q, result.lines), 201)
})

async function setQuoteStatus(c: any, guard: (q: Row) => string | null, next: number) {
  const r = await loadQuote(c.req.param('quoteId'), ctx(c).companyId)
  if (!r) return c.json({ message: 'Not found' }, 404)
  const err = guard(r.q)
  if (err) return c.json({ message: err }, 400)
  const q = await queryOne(`UPDATE "Quotes" SET "Status" = $2, "UpdatedAt" = NOW() WHERE "Id" = $1 RETURNING *`, [r.q.Id, next])
  return c.json(mapQuote(q!, r.lines))
}

quotes.post('/:quoteId/submit', (c) => setQuoteStatus(c, (q) => (q.Status !== 0 ? 'Only draft quotes can be submitted' : null), 1))
quotes.post('/:quoteId/accept', (c) =>
  setQuoteStatus(c, (q) => (q.Status !== 2 ? 'Only reviewed quotes can be accepted' : q.ValidUntil && new Date(q.ValidUntil) < new Date() ? 'Quote has expired' : null), 3),
)
quotes.post('/:quoteId/reject', (c) => setQuoteStatus(c, () => null, 4))

// ---------------------------------------------------------------- shopping lists
const mapLine = (l: Row) => ({ id: l.Id, articleId: l.ArticleId, articleCode: l.ArticleCode, articleName: l.ArticleName, quantity: l.Quantity, notes: l.Notes })

export const shoppingLists = new Hono()
shoppingLists.use('*', requireAuth)

shoppingLists.get('/', async (c) => {
  const { companyId, userId } = ctx(c)
  const lists = await query(
    `SELECT * FROM "ShoppingLists" WHERE "CompanyId" = $1 AND ("UserId" = $2 OR "IsShared" = TRUE) ORDER BY "CreatedAt" DESC`,
    [companyId, userId],
  )
  const lines = lists.length ? await query(`SELECT * FROM "ShoppingListLines" WHERE "ShoppingListId" = ANY($1)`, [lists.map((l) => l.Id)]) : []
  return c.json(
    lists.map((sl) => {
      const ls = lines.filter((l) => l.ShoppingListId === sl.Id)
      return { id: sl.Id, name: sl.Name, isShared: sl.IsShared, ownerId: sl.UserId, createdAt: sl.CreatedAt, lineCount: ls.length, lines: ls.map(mapLine) }
    }),
  )
})

shoppingLists.post('/', async (c) => {
  const { companyId, userId } = ctx(c)
  const body = (await c.req.json()) as { name?: string; isShared?: boolean }
  const row = await queryOne(
    `INSERT INTO "ShoppingLists" ("Id","Name","CompanyId","UserId","IsShared","CreatedAt") VALUES (gen_random_uuid(),$1,$2,$3,$4,NOW()) RETURNING *`,
    [body.name ?? '', companyId, userId, !!body.isShared],
  )
  return c.json({ ...camel(row), lines: [] }, 201)
})

shoppingLists.get('/:listId', async (c) => {
  const { companyId, userId } = ctx(c)
  const sl = await queryOne(`SELECT * FROM "ShoppingLists" WHERE "Id" = $1 AND "CompanyId" = $2`, [c.req.param('listId'), companyId])
  if (!sl) return c.json({ message: 'Not found' }, 404)
  if (sl.UserId !== userId && !sl.IsShared) return c.body(null, 403)
  const lines = await query(`SELECT * FROM "ShoppingListLines" WHERE "ShoppingListId" = $1`, [sl.Id])
  return c.json({ id: sl.Id, name: sl.Name, isShared: sl.IsShared, ownerId: sl.UserId, createdAt: sl.CreatedAt, lines: lines.map(mapLine) })
})

async function ownList(c: any) {
  const { companyId, userId } = ctx(c)
  const sl = await queryOne(`SELECT * FROM "ShoppingLists" WHERE "Id" = $1 AND "CompanyId" = $2`, [c.req.param('listId'), companyId])
  if (!sl) return { res: c.json({ message: 'Not found' }, 404) }
  if (sl.UserId !== userId) return { res: c.body(null, 403) }
  return { sl }
}

shoppingLists.post('/:listId/lines', async (c) => {
  const { sl, res } = await ownList(c)
  if (res) return res
  const body = (await c.req.json()) as { articleId: string; quantity: number; notes?: string }
  const article = await queryOne(`SELECT * FROM "Articles" WHERE "Id" = $1`, [body.articleId])
  if (!article) return c.json({ message: 'Article not found' }, 400)
  const existing = await queryOne(`SELECT * FROM "ShoppingListLines" WHERE "ShoppingListId" = $1 AND "ArticleId" = $2`, [sl!.Id, body.articleId])
  if (existing) {
    await query(`UPDATE "ShoppingListLines" SET "Quantity" = "Quantity" + $2 WHERE "Id" = $1`, [existing.Id, body.quantity])
  } else {
    await query(
      `INSERT INTO "ShoppingListLines" ("Id","ShoppingListId","ArticleId","ArticleCode","ArticleName","Quantity","Notes") VALUES (gen_random_uuid(),$1,$2,$3,$4,$5,$6)`,
      [sl!.Id, body.articleId, article.Code, article.Name, body.quantity, body.notes ?? null],
    )
  }
  return c.json({ message: 'Line added' })
})

shoppingLists.delete('/:listId/lines/:lineId', async (c) => {
  const { sl, res } = await ownList(c)
  if (res) return res
  const del = await query(`DELETE FROM "ShoppingListLines" WHERE "Id" = $1 AND "ShoppingListId" = $2 RETURNING "Id"`, [c.req.param('lineId'), sl!.Id])
  return del.length ? c.body(null, 204) : c.json({ message: 'Not found' }, 404)
})

shoppingLists.delete('/:listId', async (c) => {
  const { sl, res } = await ownList(c)
  if (res) return res
  await query(`DELETE FROM "ShoppingListLines" WHERE "ShoppingListId" = $1`, [sl!.Id])
  await query(`DELETE FROM "ShoppingLists" WHERE "Id" = $1`, [sl!.Id])
  return c.body(null, 204)
})

// ---------------------------------------------------------------- admin
export const admin = new Hono()
admin.use('*', requireAuth, requireRoles('PlatformAdmin', 'CompanyAdmin'))

admin.get('/companies', requireRoles('PlatformAdmin'), async (c) => {
  const page = parseInt(c.req.query('page') ?? '1', 10) || 1
  const pageSize = parseInt(c.req.query('pageSize') ?? '20', 10) || 20
  const total = (await queryOne(`SELECT COUNT(*)::int n FROM "Companies"`))!.n
  const items = await query(
    `SELECT "Id","Code","Name","CurrencyCode","IsActive","PricingEngineVersion","DeactivatedAt" AS "deactivatedAt" FROM "Companies" ORDER BY "Name" OFFSET $1 LIMIT $2`,
    [(page - 1) * pageSize, pageSize],
  )
  return c.json({ items: items.map(camel), total, page, pageSize })
})

admin.get('/price-lists', async (c) => {
  const companyId = c.req.query('companyId')
  const rows = await query(
    `SELECT p."Id",p."Name",p."CompanyId",p."Priority",p."IsActive",p."ValidFrom",p."ValidTo",
            (SELECT COUNT(*)::int FROM "PriceListLines" l WHERE l."PriceListId" = p."Id") AS "lineCount"
     FROM "PriceLists" p WHERE ($1::uuid IS NULL OR p."CompanyId" = $1::uuid) ORDER BY p."ValidFrom" DESC`,
    [companyId || null],
  )
  return c.json(camelAll(rows))
})

admin.get('/integrations', requireRoles('PlatformAdmin'), async (c) =>
  c.json(
    camelAll(
      await query(`SELECT "Id","CompanyId","Transport","Format","IsActive","Schedule","SimulatedFailurePercent","SimulatedDelayMs","LastTransferAt","LastTransferStatus" FROM "IntegrationConfigs"`),
    ),
  ),
)

admin.put('/companies/:companyId/ui-config', async (c) => {
  const companyId = c.req.param('companyId')
  const b = (await c.req.json()) as Row
  let cfg = await queryOne(`SELECT * FROM "UiConfigurations" WHERE "CompanyId" = $1`, [companyId])
  if (!cfg) {
    cfg = await queryOne(
      `INSERT INTO "UiConfigurations" ("Id","CompanyId","CustomLabelsJson","EnabledSectionsJson","ShowStockLevel","ShowProductCode")
       VALUES (gen_random_uuid(),$1,'{}','[]',FALSE,FALSE) RETURNING *`,
      [companyId],
    )
  }
  const sets: string[] = []
  const params: any[] = [cfg!.Id]
  const set = (col: string, v: any) => {
    params.push(v)
    sets.push(`"${col}" = $${params.length}`)
  }
  if (b.logoUrl != null) set('LogoUrl', b.logoUrl)
  if (b.primaryColor != null) set('PrimaryColor', b.primaryColor)
  if (b.secondaryColor != null) set('SecondaryColor', b.secondaryColor)
  if (b.catalogLayout != null) set('CatalogLayout', b.catalogLayout)
  if (b.showStockLevel != null) set('ShowStockLevel', b.showStockLevel)
  if (b.showProductCode != null) set('ShowProductCode', b.showProductCode)
  if (b.customLabels != null) set('CustomLabelsJson', JSON.stringify(b.customLabels))
  if (b.enabledSections != null) set('EnabledSectionsJson', JSON.stringify(b.enabledSections))
  const row = sets.length ? await queryOne(`UPDATE "UiConfigurations" SET ${sets.join(', ')} WHERE "Id" = $1 RETURNING *`, params) : cfg
  return c.json(camel(row))
})

admin.put('/companies/:companyId/checkout-config', async (c) => {
  const companyId = c.req.param('companyId')
  const b = (await c.req.json()) as Row
  let cfg = await queryOne(`SELECT * FROM "CheckoutConfigs" WHERE "CompanyId" = $1`, [companyId])
  if (!cfg) {
    cfg = await queryOne(
      `INSERT INTO "CheckoutConfigs" ("Id","CompanyId","PaymentTermsJson","RequiresApproval","AllowFreeAddressEntry","RequirePoNumber","AllowSplitDelivery","ConfirmationChannel")
       VALUES (gen_random_uuid(),$1,'[]',FALSE,FALSE,FALSE,FALSE,'email') RETURNING *`,
      [companyId],
    )
  }
  const sets: string[] = []
  const params: any[] = [cfg!.Id]
  const set = (col: string, v: any) => {
    params.push(v)
    sets.push(`"${col}" = $${params.length}`)
  }
  if (b.requiresApproval != null) set('RequiresApproval', b.requiresApproval)
  if (b.approvalSpendThreshold != null) set('ApprovalSpendThreshold', b.approvalSpendThreshold)
  if (b.allowFreeAddressEntry != null) set('AllowFreeAddressEntry', b.allowFreeAddressEntry)
  if (b.requirePoNumber != null) set('RequirePoNumber', b.requirePoNumber)
  if (b.paymentTerms != null) set('PaymentTermsJson', JSON.stringify(b.paymentTerms))
  const row = sets.length ? await queryOne(`UPDATE "CheckoutConfigs" SET ${sets.join(', ')} WHERE "Id" = $1 RETURNING *`, params) : cfg
  return c.json(camel(row))
})

admin.get('/audit-log', requireRoles('PlatformAdmin'), async (c) => {
  const entityType = c.req.query('entityType') || null
  const page = parseInt(c.req.query('page') ?? '1', 10) || 1
  const pageSize = parseInt(c.req.query('pageSize') ?? '50', 10) || 50
  const total = (await queryOne(`SELECT COUNT(*)::int n FROM "AuditLogs" WHERE ($1::text IS NULL OR "EntityType" = $1)`, [entityType]))!.n
  const items = await query(
    `SELECT * FROM "AuditLogs" WHERE ($1::text IS NULL OR "EntityType" = $1) ORDER BY "OccurredAt" DESC OFFSET $2 LIMIT $3`,
    [entityType, (page - 1) * pageSize, pageSize],
  )
  return c.json({ items: camelAll(items), total, page, pageSize })
})
