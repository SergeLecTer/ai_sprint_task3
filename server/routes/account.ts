import { Hono } from 'hono'
import { camel, camelAll, query, queryOne } from '../db.js'
import { ROLES, getCompanyId, getUser, requireAuth, signToken, verifyPassword } from '../auth.js'

const json = (s: string | null, fallback: any) => {
  try {
    return s ? JSON.parse(s) : fallback
  } catch {
    return fallback
  }
}

// ---------------------------------------------------------------- auth
export const auth = new Hono()

auth.post('/login', async (c) => {
  const { email, password } = (await c.req.json().catch(() => ({}))) as { email?: string; password?: string }
  const user = email ? await queryOne(`SELECT * FROM "Users" WHERE "Email" = $1 AND "IsActive" = TRUE`, [email]) : null
  if (!user || !password || !verifyPassword(password, user.PasswordHash)) {
    return c.json({ message: 'Invalid credentials' }, 401)
  }
  await query(`UPDATE "Users" SET "LastLoginAt" = NOW() WHERE "Id" = $1`, [user.Id])
  return c.json({
    accessToken: await signToken(user as any),
    refreshToken: crypto.randomUUID(), // not stored, as in the original
    expiresIn: 3600,
    user: {
      id: user.Id,
      email: user.Email,
      displayName: user.DisplayName,
      role: ROLES[user.Role].toLowerCase(),
      companyId: user.CompanyId,
    },
  })
})

auth.post('/logout', (c) => c.body(null, 200))

auth.get('/me', requireAuth, async (c) => {
  const user = await queryOne(`SELECT * FROM "Users" WHERE "Id" = $1`, [getUser(c).userId])
  if (!user) return c.json({ message: 'Not found' }, 404)
  return c.json({
    id: user.Id,
    email: user.Email,
    displayName: user.DisplayName,
    role: ROLES[user.Role].toLowerCase(),
    companyId: user.CompanyId,
    isActive: user.IsActive,
  })
})

// ---------------------------------------------------------------- companies
const uiDto = (u: any) => ({
  logoUrl: u.LogoUrl,
  primaryColor: u.PrimaryColor,
  secondaryColor: u.SecondaryColor,
  catalogLayout: u.CatalogLayout,
  showStockLevel: u.ShowStockLevel,
  showProductCode: u.ShowProductCode,
  customLabels: json(u.CustomLabelsJson, {}),
  enabledSections: json(u.EnabledSectionsJson, []),
  dateFormat: u.DateFormat,
  numberFormat: u.NumberFormat,
})

export const companies = new Hono()
companies.use('*', requireAuth)

companies.get('/me', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const co = await queryOne(`SELECT * FROM "Companies" WHERE "Id" = $1 AND "DeactivatedAt" IS NULL`, [companyId])
  if (!co) return c.json({ message: 'Not found' }, 404)
  const ui = await queryOne(`SELECT * FROM "UiConfigurations" WHERE "CompanyId" = $1`, [companyId])
  return c.json({
    id: co.Id,
    code: co.Code,
    name: co.Name,
    currencyCode: co.CurrencyCode,
    languageCode: co.LanguageCode,
    isActive: co.IsActive,
    pricingEngineVersion: co.PricingEngineVersion,
    uiConfig: ui ? uiDto(ui) : null,
  })
})

companies.get('/me/ui-config', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const ui = await queryOne(`SELECT * FROM "UiConfigurations" WHERE "CompanyId" = $1`, [companyId])
  return c.json(ui ? uiDto(ui) : {})
})

companies.get('/me/addresses', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  return c.json(camelAll(await query(`SELECT * FROM "DeliveryAddresses" WHERE "CompanyId" = $1 AND "IsActive" = TRUE`, [companyId])))
})

companies.get('/me/checkout-config', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const cfg = await queryOne(`SELECT * FROM "CheckoutConfigs" WHERE "CompanyId" = $1`, [companyId])
  if (!cfg) {
    return c.json({
      companyId,
      steps: [
        { stepType: 'cart_review', label: 'Review', isRequired: true, sortOrder: 1, config: {} },
        { stepType: 'delivery_address', label: 'Delivery', isRequired: true, sortOrder: 2, config: {} },
        { stepType: 'confirmation', label: 'Confirm', isRequired: true, sortOrder: 3, config: {} },
      ],
      requiresApproval: false,
      approvalSpendThreshold: null,
      paymentTerms: ['Net 30'],
      allowFreeAddressEntry: false,
      requirePoNumber: false,
      confirmationChannel: 'email',
    })
  }
  const steps = await query(`SELECT * FROM "CheckoutSteps" WHERE "CheckoutConfigId" = $1 ORDER BY "SortOrder"`, [cfg.Id])
  return c.json({
    companyId: cfg.CompanyId,
    steps: steps.map((s) => ({ stepType: s.StepType, label: s.Label, isRequired: s.IsRequired, sortOrder: s.SortOrder, config: json(s.ConfigJson, {}) })),
    requiresApproval: cfg.RequiresApproval,
    approvalSpendThreshold: cfg.ApprovalSpendThreshold,
    paymentTerms: json(cfg.PaymentTermsJson, []),
    allowFreeAddressEntry: cfg.AllowFreeAddressEntry,
    requirePoNumber: cfg.RequirePoNumber,
    confirmationChannel: cfg.ConfirmationChannel,
  })
})

// ---------------------------------------------------------------- reports
export const reports = new Hono()
reports.use('*', requireAuth)

const delta = (cur: number, prev: number) => (prev === 0 ? 0 : ((cur - prev) / prev) * 100)

reports.get('/dashboard', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const now = new Date()
  const monthStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
  const lastMonthStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 1, 1))

  const [cur, prev, pending, open, top, breakdown, notifications] = await Promise.all([
    queryOne(`SELECT COUNT(*)::int n, COALESCE(SUM("GrandTotal"),0) s FROM "Orders" WHERE "CompanyId"=$1 AND "CreatedAt" >= $2`, [companyId, monthStart]),
    queryOne(`SELECT COUNT(*)::int n, COALESCE(SUM("GrandTotal"),0) s FROM "Orders" WHERE "CompanyId"=$1 AND "CreatedAt" >= $2 AND "CreatedAt" < $3`, [companyId, lastMonthStart, monthStart]),
    queryOne(`SELECT COUNT(*)::int n FROM "Orders" WHERE "CompanyId"=$1 AND "Status"=1`, [companyId]),
    queryOne(`SELECT COUNT(*)::int n FROM "Orders" WHERE "CompanyId"=$1 AND "Status" NOT IN (6,7,8)`, [companyId]),
    query(
      `SELECT ol."ArticleId" "articleId", ol."ArticleCode" "articleCode", ol."ArticleName" "articleName",
              SUM(ol."Quantity")::int "totalQuantity", SUM(ol."LineTotal") "totalSpend"
       FROM "OrderLines" ol JOIN "Orders" o ON o."Id" = ol."OrderId"
       WHERE o."CompanyId"=$1 AND o."CreatedAt" >= $2
       GROUP BY ol."ArticleId", ol."ArticleCode", ol."ArticleName" ORDER BY "totalSpend" DESC LIMIT 5`,
      [companyId, monthStart],
    ),
    query(`SELECT "Status", COUNT(*)::int n FROM "Orders" WHERE "CompanyId"=$1 AND "CreatedAt" >= $2 GROUP BY "Status"`, [companyId, lastMonthStart]),
    query(`SELECT "Id","Title","Message","Type","CreatedAt" FROM "Notifications" WHERE "UserId"=$1 AND "IsRead" = FALSE ORDER BY "CreatedAt" DESC LIMIT 5`, [getUser(c).userId]),
  ])
  const STATUS = ['Draft', 'PendingApproval', 'Approved', 'Transferred', 'Acknowledged', 'PartiallyFulfilled', 'Fulfilled', 'Cancelled', 'Returned']
  return c.json({
    ordersThisMonth: cur!.n,
    ordersLastMonth: prev!.n,
    ordersMonthDelta: delta(cur!.n, prev!.n),
    totalSpendThisMonth: cur!.s,
    totalSpendLastMonth: prev!.s,
    spendMonthDelta: delta(cur!.s, prev!.s),
    pendingApprovalCount: pending!.n,
    openOrdersCount: open!.n,
    topArticles: top,
    statusBreakdown: breakdown.map((b) => ({ status: STATUS[b.Status], count: b.n })),
    notifications: camelAll(notifications),
  })
})

reports.get('/spend-analysis', async (c) => {
  const companyId = getCompanyId(c)
  if (!companyId) return c.json({ message: 'Unauthorized' }, 401)
  const months = parseInt(c.req.query('months') ?? '12', 10) || 12
  const cutoff = new Date()
  cutoff.setUTCMonth(cutoff.getUTCMonth() - months)
  return c.json(
    await query(
      `SELECT EXTRACT(YEAR FROM "CreatedAt" AT TIME ZONE 'UTC')::int AS year,
              EXTRACT(MONTH FROM "CreatedAt" AT TIME ZONE 'UTC')::int AS month,
              COUNT(*)::int AS "orderCount", SUM("GrandTotal") AS "totalSpend"
       FROM "Orders" WHERE "CompanyId"=$1 AND "CreatedAt" >= $2 GROUP BY 1,2 ORDER BY 1,2`,
      [companyId, cutoff],
    ),
  )
})
