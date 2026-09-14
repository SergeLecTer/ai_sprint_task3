import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { User, Company, CompanyUiConfig, Order, OrderLine } from '../types'

interface CartState {
  lines: CartLine[]
  addLine: (line: CartLine) => void
  updateLine: (articleId: string, quantity: number) => void
  removeLine: (articleId: string) => void
  clearCart: () => void
  totalItems: () => number
}

export interface CartLine {
  articleId: string
  articleCode: string
  articleName: string
  variantId?: string
  quantity: number
  unitPrice: number
  lineTotal: number
  unitOfMeasure: string
  notes?: string
}

interface AuthState {
  user: User | null
  isAuthenticated: boolean
  setUser: (user: User | null) => void
  logout: () => void
}

interface TenantState {
  company: Company | null
  uiConfig: CompanyUiConfig | null
  setCompany: (company: Company) => void
  setUiConfig: (config: CompanyUiConfig) => void
}

interface NotificationState {
  unreadCount: number
  setUnreadCount: (count: number) => void
}

type StoreState = CartState & AuthState & TenantState & NotificationState

export const useStore = create<StoreState>()(
  persist(
    (set, get) => ({
      // Cart
      lines: [],
      addLine: (line) => {
        const existing = get().lines.find(
          (l) => l.articleId === line.articleId && l.variantId === line.variantId
        )
        if (existing) {
          set((s) => ({
            lines: s.lines.map((l) =>
              l.articleId === line.articleId && l.variantId === line.variantId
                ? { ...l, quantity: l.quantity + line.quantity, lineTotal: (l.quantity + line.quantity) * l.unitPrice }
                : l
            )
          }))
        } else {
          set((s) => ({ lines: [...s.lines, line] }))
        }
      },
      updateLine: (articleId, quantity) => {
        if (quantity <= 0) {
          get().removeLine(articleId)
          return
        }
        set((s) => ({
          lines: s.lines.map((l) =>
            l.articleId === articleId ? { ...l, quantity, lineTotal: quantity * l.unitPrice } : l
          )
        }))
      },
      removeLine: (articleId) => {
        set((s) => ({ lines: s.lines.filter((l) => l.articleId !== articleId) }))
      },
      clearCart: () => set({ lines: [] }),
      totalItems: () => get().lines.reduce((sum, l) => sum + l.quantity, 0),

      // Auth
      user: null,
      isAuthenticated: false,
      setUser: (user) => set({ user, isAuthenticated: !!user }),
      logout: () => {
        localStorage.removeItem('accessToken')
        localStorage.removeItem('refreshToken')
        set({ user: null, isAuthenticated: false, lines: [] })
      },

      // Tenant
      company: null,
      uiConfig: null,
      setCompany: (company) => set({ company }),
      setUiConfig: (uiConfig) => set({ uiConfig }),

      // Notifications
      unreadCount: 0,
      setUnreadCount: (unreadCount) => set({ unreadCount })
    }),
    {
      name: 'b2bshop-store',
      partialize: (state) => ({
        lines: state.lines,
        user: state.user,
        isAuthenticated: state.isAuthenticated
      })
    }
  )
)
