import { Hono } from 'hono'
import { query, queryOne, withTransaction, type Row } from '../db.js'
import { getCompanyId, getUser, requireAuth } from '../auth.js'
import { calculatePrice } from '../pricing.js'
import { loadPriceData } from './catalog.js'

export const STATUS = ['Draft', 'PendingApproval', 'Approved', 'Transferred', 'Acknowledged', 'PartiallyFulfilled', 'Fulfilled', 'Cancelled', 'Returned'] as const
type Status = (typeof STATUS)[number]
const S = (s: Status) => STATUS.indexOf(s)

const ALLOWED: Record<Status, Status[]> = {
  Draft: ['PendingApproval', 'Cancelled'],
  PendingApproval: ['Approved', 'Cancelled'],
  Approved: ['Transferred', 'Cancelled'],
  Transferred: ['Acknowledged', 'Cancelled'],
  Acknowledged: ['PartiallyFulfilled', 'Fulfilled'],
  PartiallyFulfilled: ['Fulfilled', 'Returned'],
  Fulfilled: ['Returned'],
  Cancelled: [],
  Returned: [],
}

class BadRequest extends Error {}

/** Applies Order.TransitionTo: validates, sets status and the matching timestamps. */
export function transitionSql(order: Row, target: Status) {
  const current = STATUS[order.Status]
  if (!ALLOWED[current].includes(target)) throw new BadRequest(`Cannot transition order from ${current} to ${target}`)
  const sets = [`"Status" = ${S(target)}`, `"UpdatedAt" = NOW()`]
  if (target === 'Transferred') sets.push(`"TransferredAt" = NOW()`)
  if (target === 'Acknowledged') sets.push(`"AcknowledgedAt" = NOW()`)
  if (target === 'Fulfilled') sets.push(`"FulfilledAt" = NOW()`)
  return sets.join(', ')
}

const statusLabel = (n: number) =>
  STATUS[n].toLowerCase().replace('pendingapproval', 'pending_approval').replace('partiallyfulfilled', 'partially_fulfilled')

const mapOrder = (o: Row, lines: Row[]) => ({
  id: o.Id,
  orderNumber: o.OrderNumber,
  companyId: o.CompanyId,
  userId: o.UserId,
  status: statusLabel(o.Status),
  lines: lines.map((l) => ({
    id: l.Id,
    articleId: l.ArticleId,
    articleCode: l.ArticleCode,
    articleName: l.ArticleName,
    quantity: l.Quantity,
    unitOfMeasure: l.UnitOfMeasure,
    unitPrice: l.UnitPrice,
    lineTotal: l.LineTotal,
    discountPercent: l.DiscountPercent,
    notes: l.Notes,
  })),
  subtotal: o.Subtotal,
  discountTotal: o.DiscountTotal,
  taxTotal: o.TaxTotal,
  grandTotal: o.GrandTotal,
  currencyCode: o.CurrencyCode,
  deliveryAddressId: o.DeliveryAddressId,
  poNumber: o.PoNumber,
  approvalRequired: o.ApprovalRequired,
  notes: o.Notes,
  createdAt: o.CreatedAt,
  updatedAt: o.UpdatedAt,
  transferredAt: o.TransferredAt,
  acknowledgedAt: o.AcknowledgedAt,
})

async function withLines(orders: Row[]) {
  if (orders.length === 0) return []
  const lines = await query(`SELECT * FROM "OrderLines" WHERE "OrderId" = ANY($1)`, [orders.map((o) => o.Id)])
  return orders.map((o) => mapOrder(o, lines.filter((l) => l.OrderId === o.Id)))
}

interface CreateOrderRequest {
  lines: { articleId: string; variantId?: string | null; quantity: number; notes?: string | null }[]
  deliveryAddressId?: string | null
  poNumber?: string | null
  requestedDeliveryDate?: string | null
  notes?: string | null
}

export async function createOrder(companyId: string, userId: string, req: CreateOrderRequest) {
  const company = await queryOne(`SELECT * FROM "Companies" WHERE "Id" = $1 AND "DeactivatedAt" IS NULL`, [companyId])
  if (!company) throw new BadRequest('Company not found')
  const checkout = await queryOne(`SELECT * FROM "CheckoutConfigs" WHERE "CompanyId" = $1`, [companyId])
  const priceData = await loadPriceData(companyId)

  const qtyByArticle = new Map<string, number>()
  for (const l of req.lines) qtyByArticle.set(l.articleId, (qtyByArticle.get(l.articleId) ?? 0) + l.quantity)

  const lines: Row[] = []
  let subtotal = 0
  for (const l of req.lines) {
    const article = await queryOne(`SELECT * FROM "Articles" WHERE "Id" = $1`, [l.articleId])
    if (!article) throw new BadRequest(`Article ${l.articleId} not found`)

    let quantity = l.quantity
    if (article.EnforcePackSizeRounding && article.PackSize > 1) {
      quantity = Math.ceil(quantity / article.PackSize) * article.PackSize
    }

    const price = calculatePrice({
      article,
      variantId: l.variantId,
      company,
      quantity,
      ...priceData,
      orderQuantitiesByArticle: qtyByArticle,
    })
    const lineTotal = price.unitPrice * quantity
    subtotal += lineTotal
    lines.push({
      ArticleId: l.articleId,
      ArticleCode: article.Code,
      ArticleName: article.Name,
      VariantId: l.variantId ?? null,
      Quantity: quantity,
      UnitOfMeasure: article.UnitOfMeasure,
      UnitPrice: price.unitPrice,
      OriginalUnitPrice: article.BasePrice,
      LineTotal: lineTotal,
      PriceType: price.priceType,
      Notes: l.notes ?? null,
    })
  }

  if (company.CreditLimit != null && company.CurrentCreditUsed + subtotal > company.CreditLimit) {
    throw new BadRequest('Order exceeds credit limit')
  }

  const requiresApproval =
    checkout?.RequiresApproval === true &&
    (checkout.ApprovalSpendThreshold == null || subtotal >= checkout.ApprovalSpendThreshold)

  return withTransaction(async (c) => {
    // not atomic by design (see original: race under concurrency)
    const { rows } = await c.query(`SELECT COUNT(*)::int AS n FROM "Orders"`)
    const now = new Date()
    const orderNumber = `ORD-${now.getUTCFullYear()}${String(now.getUTCMonth() + 1).padStart(2, '0')}-${String(rows[0].n + 1).padStart(5, '0')}`

    const { rows: oRows } = await c.query(
      `INSERT INTO "Orders" ("Id","OrderNumber","CompanyId","UserId","Status","Subtotal","DiscountTotal","TaxTotal","GrandTotal",
         "CurrencyCode","DeliveryAddressId","PoNumber","RequestedDeliveryDate","ApprovalRequired","Notes","CreatedAt","UpdatedAt","PricingEngineVersion")
       VALUES (gen_random_uuid(),$1,$2,$3,0,$4,0,0,$4,$5,$6,$7,$8,$9,$10,NOW(),NOW(),$11) RETURNING *`,
      [orderNumber, companyId, userId, subtotal, company.CurrencyCode, req.deliveryAddressId ?? null, req.poNumber ?? null,
       req.requestedDeliveryDate ?? null, requiresApproval, req.notes ?? null, company.PricingEngineVersion],
    )
    const order = oRows[0]
    const saved: Row[] = []
    for (const l of lines) {
      const { rows: lr } = await c.query(
        `INSERT INTO "OrderLines" ("Id","OrderId","ArticleId","ArticleCode","ArticleName","VariantId","Quantity","UnitOfMeasure",
           "UnitPrice","OriginalUnitPrice","DiscountPercent","LineTotal","Notes","PriceType")
         VALUES (gen_random_uuid(),$1,$2,$3,$4,$5,$6,$7,$8,$9,0,$10,$11,$12) RETURNING *`,
        [order.Id, l.ArticleId, l.ArticleCode, l.ArticleName, l.VariantId, l.Quantity, l.UnitOfMeasure, l.UnitPrice, l.OriginalUnitPrice, l.LineTotal, l.Notes, l.PriceType],
      )
      saved.push(lr[0])
    }
    return { order, lines: saved }
  })
}

async function transition(orderId: string, extra: { userId?: string; target: Status; set?: string; params?: any[] }) {
  const order = await queryOne(
    `SELECT * FROM "Orders" WHERE "Id" = $1 ${extra.userId ? `AND "UserId" = $2` : ''}`,
    extra.userId ? [orderId, extra.userId] : [orderId],
  )
  if (!order) throw new BadRequest('Order not found')
  const sets = transitionSql(order, extra.target) + (extra.set ? `, ${extra.set}` : '')
  const updated = await queryOne(`UPDATE "Orders" SET ${sets} WHERE "Id" = $1 RETURNING *`, [orderId, ...(extra.params ?? [])])
  const lines = await query(`SELECT * FROM "OrderLines" WHERE "OrderId" = $1`, [orderId])
  return mapOrder(updated!, lines)
}

export const orders = new Hono()
orders.use('*', requireAuth)

orders.onError((err, c) => {
  if (err instanceof BadRequest) return c.json({ message: err.message }, 400)
  throw err
})

const ctx = (c: any) => ({ companyId: getCompanyId(c)!, userId: getUser(c).userId })

orders.post('/', async (c) => {
  const { companyId, userId } = ctx(c)
  const body = (await c.req.json()) as CreateOrderRequest
  const { order, lines } = await createOrder(companyId, userId, body)
  return c.json(mapOrder(order, lines), 201)
})

orders.get('/pending-approval', async (c) => {
  const { companyId } = ctx(c)
  const rows = await query(`SELECT * FROM "Orders" WHERE "CompanyId" = $1 AND "Status" = ${S('PendingApproval')}`, [companyId])
  return c.json(await withLines(rows))
})

orders.get('/', async (c) => {
  const { companyId } = ctx(c)
  const status = c.req.query('status')
  const page = parseInt(c.req.query('page') ?? '1', 10) || 1
  const pageSize = parseInt(c.req.query('pageSize') ?? '20', 10) || 20

  const params: any[] = [companyId]
  let where = `"CompanyId" = $1`
  if (status) {
    const pascal = status.split('_').map((s) => s.charAt(0).toUpperCase() + s.slice(1)).join('')
    const idx = STATUS.findIndex((s) => s.toLowerCase() === pascal.toLowerCase())
    if (idx >= 0) {
      params.push(idx)
      where += ` AND "Status" = $${params.length}`
    }
  }
  const total = (await queryOne(`SELECT COUNT(*)::int AS n FROM "Orders" WHERE ${where}`, params))!.n
  const rows = await query(
    `SELECT * FROM "Orders" WHERE ${where} ORDER BY "CreatedAt" DESC OFFSET ${(page - 1) * pageSize} LIMIT ${pageSize}`,
    params,
  )
  return c.json({ items: await withLines(rows), total, page, pageSize, totalPages: Math.ceil(total / pageSize) })
})

orders.get('/:orderId', async (c) => {
  const { companyId } = ctx(c)
  const order = await queryOne(`SELECT * FROM "Orders" WHERE "Id" = $1 AND "CompanyId" = $2`, [c.req.param('orderId'), companyId])
  if (!order) return c.json({ message: 'Not found' }, 404)
  return c.json((await withLines([order]))[0])
})

orders.post('/:orderId/submit', async (c) => {
  const { userId } = ctx(c)
  const id = c.req.param('orderId')
  const order = await queryOne(`SELECT * FROM "Orders" WHERE "Id" = $1 AND "UserId" = $2`, [id, userId])
  if (!order) throw new BadRequest('Order not found')
  return c.json(await transition(id, { userId, target: order.ApprovalRequired ? 'PendingApproval' : 'Approved' }))
})

orders.post('/:orderId/approve', async (c) => {
  const { userId } = ctx(c)
  return c.json(await transition(c.req.param('orderId'), { target: 'Approved', set: `"ApproverId" = $2`, params: [userId] }))
})

const cancel = async (c: any) => {
  const body = (await c.req.json().catch(() => ({}))) as { reason?: string }
  return c.json(
    await transition(c.req.param('orderId'), {
      target: 'Cancelled',
      set: `"Notes" = COALESCE("Notes",'') || $2`,
      params: [`\nCancelled: ${body.reason ?? ''}`],
    }),
  )
}
orders.post('/:orderId/reject', cancel)
orders.post('/:orderId/cancel', cancel)

orders.post('/:orderId/reorder', async (c) => {
  const { userId } = ctx(c)
  const source = await queryOne(`SELECT * FROM "Orders" WHERE "Id" = $1`, [c.req.param('orderId')])
  if (!source) throw new BadRequest('Source order not found')
  const lines = await query(`SELECT * FROM "OrderLines" WHERE "OrderId" = $1`, [source.Id])
  const created = await createOrder(source.CompanyId, userId, {
    lines: lines.map((l) => ({ articleId: l.ArticleId, variantId: l.VariantId, quantity: l.Quantity, notes: l.Notes })),
    notes: `Reorder of ${source.OrderNumber}`,
  })
  return c.json(mapOrder(created.order, created.lines), 201)
})
