import type { Context, MiddlewareHandler } from 'hono'
import { SignJWT, jwtVerify } from 'jose'

export const ROLES = ['PlatformAdmin', 'CompanyAdmin', 'Buyer', 'Approver', 'SalesRep', 'Auditor'] as const
export type RoleName = (typeof ROLES)[number]

export interface AuthUser {
  userId: string
  email: string
  role: RoleName
  companyId: string | null
  actingCompanyId: string | null
}

function secret(): Uint8Array {
  const s = process.env.JWT_SECRET
  if (!s) throw new Error('JWT_SECRET is not set')
  return new TextEncoder().encode(s)
}

const issuer = () => process.env.JWT_ISSUER ?? 'b2bshop'
const audience = () => process.env.JWT_AUDIENCE ?? 'b2bshop-client'

export async function signToken(u: {
  Id: string
  Email: string
  Role: number
  CompanyId: string | null
  ActingOnBehalfOfCompanyId: string | null
}): Promise<string> {
  return new SignJWT({
    email: u.Email,
    role: ROLES[u.Role],
    companyId: u.CompanyId ?? '',
    actingCompanyId: u.ActingOnBehalfOfCompanyId ?? '',
  })
    .setProtectedHeader({ alg: 'HS256', typ: 'JWT' })
    .setSubject(u.Id)
    .setIssuer(issuer())
    .setAudience(audience())
    .setExpirationTime('1h')
    .sign(secret())
}

/** Same stub scheme as the original AuthController (and the seeder): "$2a$10$" + base64(password). */
export const hashPassword = (pw: string) => '$2a$10$' + Buffer.from(pw, 'utf8').toString('base64')
export const verifyPassword = (pw: string, hash: string) => hash === hashPassword(pw)

export const requireAuth: MiddlewareHandler = async (c, next) => {
  const header = c.req.header('authorization') ?? ''
  const token = header.startsWith('Bearer ') ? header.slice(7) : null
  if (!token) return c.json({ message: 'Unauthorized' }, 401)
  try {
    const { payload } = await jwtVerify(token, secret(), { issuer: issuer(), audience: audience() })
    const user: AuthUser = {
      userId: String(payload.sub),
      email: String(payload.email ?? ''),
      role: payload.role as RoleName,
      companyId: (payload.companyId as string) || null,
      actingCompanyId: (payload.actingCompanyId as string) || null,
    }
    c.set('user', user)
  } catch {
    return c.json({ message: 'Unauthorized' }, 401)
  }
  await next()
}

export const requireRoles =
  (...roles: RoleName[]): MiddlewareHandler =>
  async (c, next) => {
    const u = c.get('user') as AuthUser
    if (!roles.includes(u.role)) return c.json({ message: 'Forbidden' }, 403)
    await next()
  }

export const getUser = (c: Context) => c.get('user') as AuthUser

/** Acting-on-behalf company (SalesRep) takes precedence over the user's own company. */
export const getCompanyId = (c: Context): string | null => {
  const u = getUser(c)
  return u.actingCompanyId || u.companyId || null
}
