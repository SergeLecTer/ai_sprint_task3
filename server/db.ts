import pg from 'pg'

// numeric -> number, int8 -> number (EF/.NET serialised decimals and counts as JSON numbers)
pg.types.setTypeParser(1700, (v) => parseFloat(v))
pg.types.setTypeParser(20, (v) => parseInt(v, 10))

let pool: pg.Pool | undefined

export function getPool(): pg.Pool {
  if (!pool) {
    const connectionString = process.env.POSTGRES_URL
    if (!connectionString) throw new Error('POSTGRES_URL is not set')
    pool = new pg.Pool({ connectionString, max: 3, idleTimeoutMillis: 10_000 })
  }
  return pool
}

export type Row = Record<string, any>

export async function query<T extends Row = Row>(text: string, params: any[] = []): Promise<T[]> {
  const res = await getPool().query(text, params)
  return res.rows as T[]
}

export async function queryOne<T extends Row = Row>(text: string, params: any[] = []): Promise<T | null> {
  const rows = await query<T>(text, params)
  return rows[0] ?? null
}

export async function withTransaction<T>(fn: (c: pg.PoolClient) => Promise<T>): Promise<T> {
  const client = await getPool().connect()
  try {
    await client.query('BEGIN')
    const out = await fn(client)
    await client.query('COMMIT')
    return out
  } catch (e) {
    await client.query('ROLLBACK')
    throw e
  } finally {
    client.release()
  }
}

/** DB columns are PascalCase (EF Core); the API contract is camelCase (System.Text.Json). */
export function camel<T = any>(row: Row | null): T {
  if (row === null) return null as T
  const out: Row = {}
  for (const [k, v] of Object.entries(row)) out[k.charAt(0).toLowerCase() + k.slice(1)] = v
  return out as T
}

export const camelAll = <T = any>(rows: Row[]): T[] => rows.map((r) => camel<T>(r))
