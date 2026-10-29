import { Hono } from 'hono'
import { auth, companies, reports } from './routes/account.js'
import { admin, quotes, shoppingLists } from './routes/records.js'
import { catalog } from './routes/catalog.js'
import { orders } from './routes/orders.js'
import { processPendingTransfers } from './transfer.js'

export const app = new Hono().basePath('/api')

app.onError((err, c) => {
  console.error(err)
  return c.json({ message: err instanceof Error ? err.message : 'Internal Server Error' }, 500)
})

app.route('/auth', auth)
app.route('/companies', companies)
app.route('/reports', reports)
app.route('/catalog', catalog)
app.route('/orders', orders)
app.route('/quotes', quotes)
app.route('/shopping-lists', shoppingLists)
app.route('/admin', admin)

// Vercel Cron (replaces the Worker BackgroundService). Vercel sends `Authorization: Bearer $CRON_SECRET`.
app.get('/cron/transfer', async (c) => {
  const secret = process.env.CRON_SECRET
  if (!secret || c.req.header('authorization') !== `Bearer ${secret}`) return c.json({ message: 'Unauthorized' }, 401)
  return c.json(await processPendingTransfers())
})
