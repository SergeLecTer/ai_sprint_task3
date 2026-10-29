import { mkdir, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { query, queryOne, type Row } from './db.js'
import { transitionSql } from './routes/orders.js'

// Port of OrderTransferService + OrderSerializer + OrderTransferPolicy. Runs from a Vercel cron
// (the .NET original was a 30-second polling BackgroundService, which Vercel Functions cannot host).

const MAX_RETRY_ATTEMPTS = 3
const retryDelayMs = (attempt: number) => 2 ** (attempt - 1) * 5 * 60_000
const f4 = (n: number) => Number(n).toFixed(4)
const csv = (v: string | null) => (v == null ? '' : /[,"\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v)
const STATUS = ['Draft', 'PendingApproval', 'Approved', 'Transferred', 'Acknowledged', 'PartiallyFulfilled', 'Fulfilled', 'Cancelled', 'Returned']

function serialize(format: string, o: Row, lines: Row[]): string {
  switch (format) {
    case 'xml':
      return (
        `<?xml version="1.0" encoding="utf-8"?>\n<Order>\n  <OrderNumber>${o.OrderNumber}</OrderNumber>\n  <CompanyId>${o.CompanyId}</CompanyId>\n` +
        `  <Status>${STATUS[o.Status]}</Status>\n  <CurrencyCode>${o.CurrencyCode}</CurrencyCode>\n  <GrandTotal>${Number(o.GrandTotal).toFixed(2)}</GrandTotal>\n` +
        `  <CreatedAt>${new Date(o.CreatedAt).toISOString()}</CreatedAt>\n  <Lines>\n` +
        lines
          .map(
            (l) =>
              `    <Line>\n      <ArticleCode>${l.ArticleCode}</ArticleCode>\n      <ArticleName>${String(l.ArticleName).replace(/&/g, '&amp;').replace(/</g, '&lt;')}</ArticleName>\n` +
              `      <Quantity>${l.Quantity}</Quantity>\n      <UnitOfMeasure>${l.UnitOfMeasure}</UnitOfMeasure>\n      <UnitPrice>${f4(l.UnitPrice)}</UnitPrice>\n      <LineTotal>${f4(l.LineTotal)}</LineTotal>\n    </Line>\n`,
          )
          .join('') +
        `  </Lines>\n</Order>`
      )
    case 'csv':
      return (
        'OrderNumber,CompanyId,ArticleCode,ArticleName,Quantity,UOM,UnitPrice,LineTotal,CurrencyCode\n' +
        lines
          .map((l) => [csv(o.OrderNumber), o.CompanyId, csv(l.ArticleCode), csv(l.ArticleName), l.Quantity, csv(l.UnitOfMeasure), f4(l.UnitPrice), f4(l.LineTotal), csv(o.CurrencyCode)].join(','))
          .join('\n') +
        '\n'
      )
    case 'edi': {
      const d = new Date(o.CreatedAt).toISOString().slice(0, 10).replace(/-/g, '')
      return (
        `HDR${String(o.OrderNumber).padEnd(20)}${d}${String(o.CurrencyCode).padStart(3)}${Number(o.GrandTotal).toFixed(2).padStart(12)}\n` +
        lines.map((l) => `DET${String(l.ArticleCode).padEnd(30)}${String(l.Quantity).padStart(8)}${String(l.UnitOfMeasure).padEnd(5)}${f4(l.UnitPrice).padStart(12)}${f4(l.LineTotal).padStart(12)}\n`).join('') +
        `TRL${String(lines.length).padStart(5)}\n`
      )
    }
    default:
      return JSON.stringify(
        {
          orderNumber: o.OrderNumber,
          companyId: o.CompanyId,
          status: STATUS[o.Status],
          currencyCode: o.CurrencyCode,
          grandTotal: o.GrandTotal,
          createdAt: o.CreatedAt,
          lines: lines.map((l) => ({ articleCode: l.ArticleCode, articleName: l.ArticleName, quantity: l.Quantity, unitOfMeasure: l.UnitOfMeasure, unitPrice: l.UnitPrice, lineTotal: l.LineTotal })),
        },
        null,
        2,
      )
  }
}

async function transmit(cfg: Row, payload: string, o: Row): Promise<{ ok: boolean; error?: string; response?: string }> {
  if (cfg.SimulatedFailurePercent && cfg.SimulatedFailurePercent > 0 && Math.floor(Math.random() * 100) < cfg.SimulatedFailurePercent) {
    return { ok: false, error: `Simulated failure (configured at ${cfg.SimulatedFailurePercent}%)` }
  }
  if (cfg.SimulatedDelayMs && cfg.SimulatedDelayMs > 0) await new Promise((r) => setTimeout(r, cfg.SimulatedDelayMs))
  const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14)
  switch (cfg.Transport) {
    case 'http':
      return { ok: true, response: JSON.stringify({ status: 'accepted', acknowledgementId: `ACK-${o.OrderNumber}-${Date.now()}` }) }
    case 'sftp':
      return { ok: true, response: `File uploaded: orders/${o.OrderNumber}_${stamp}.${cfg.Format}` }
    case 'fileshare': {
      try {
        const dir = join(tmpdir(), 'b2bshop_transfers')
        await mkdir(dir, { recursive: true })
        const path = join(dir, `order_${o.OrderNumber}_${stamp}.${cfg.Format}`)
        await writeFile(path, payload)
        return { ok: true, response: `Written to ${path}` }
      } catch (e: any) {
        return { ok: false, error: `File write failed: ${e.message}` }
      }
    }
    case 'email':
      return { ok: true, response: `Email stub: would send order ${o.OrderNumber} to configured recipient` }
    default:
      return { ok: false, error: `Unknown transport: ${cfg.Transport}` }
  }
}

async function log(orderId: string, cfgId: string, attempt: number, status: string, error: string | null, response: string | null) {
  await query(
    `INSERT INTO "TransferLogs" ("Id","OrderId","IntegrationConfigId","AttemptNumber","AttemptedAt","Status","ErrorMessage","ResponsePayload")
     VALUES (gen_random_uuid(),$1,$2,$3,NOW(),$4,$5,$6)`,
    [orderId, cfgId, attempt, status, error, response],
  )
}

export async function processPendingTransfers(): Promise<{ found: number; transferred: number }> {
  const pending = await query(`SELECT * FROM "Orders" WHERE "Status" = 2 AND "TransferredAt" IS NULL LIMIT 50`)
  let transferred = 0
  for (const order of pending) {
    const cfg = await queryOne(`SELECT * FROM "IntegrationConfigs" WHERE "CompanyId" = $1 AND "IsActive" = TRUE`, [order.CompanyId])
    if (!cfg) continue

    const previous = (await queryOne(`SELECT COUNT(*)::int n FROM "TransferLogs" WHERE "OrderId" = $1`, [order.Id]))!.n
    const attempt = previous + 1
    if (attempt > MAX_RETRY_ATTEMPTS) {
      await log(order.Id, cfg.Id, attempt, 'dead_letter', 'Exceeded maximum retry attempts', null)
      continue
    }
    if (previous > 0) {
      const last = await queryOne(`SELECT "AttemptedAt" FROM "TransferLogs" WHERE "OrderId" = $1 ORDER BY "AttemptedAt" DESC LIMIT 1`, [order.Id])
      if (last && Date.now() < new Date(last.AttemptedAt).getTime() + retryDelayMs(previous)) continue
    }

    const lines = await query(`SELECT * FROM "OrderLines" WHERE "OrderId" = $1`, [order.Id])
    const payload = serialize(cfg.Format, order, lines)
    const { ok, error, response } = await transmit(cfg, payload, order)
    await log(order.Id, cfg.Id, attempt, ok ? 'success' : 'failed', error ?? null, response ?? null)

    if (ok) {
      await query(`UPDATE "Orders" SET ${transitionSql(order, 'Transferred')} WHERE "Id" = $1`, [order.Id])
      await query(`UPDATE "IntegrationConfigs" SET "LastTransferAt" = NOW(), "LastTransferStatus" = 'success', "LastErrorMessage" = NULL WHERE "Id" = $1`, [cfg.Id])
      transferred++
    } else {
      await query(`UPDATE "IntegrationConfigs" SET "LastTransferStatus" = 'failed', "LastErrorMessage" = $2 WHERE "Id" = $1`, [cfg.Id, error])
    }
  }
  return { found: pending.length, transferred }
}
