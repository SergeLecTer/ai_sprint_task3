import { useQuery } from '@tanstack/react-query'
import apiClient from '../services/api'
import { useStore } from '../hooks/useStore'
import { formatPrice } from '../utils/format'

interface DashboardStats {
  totalOrders: number
  totalSpend: number
  pendingApprovals: number
  openQuotes: number
  topCategories: { name: string; spend: number }[]
  recentActivity: { type: string; description: string; at: string }[]
  spendByMonth: { month: string; amount: number }[]
}

export default function DashboardPage() {
  const company = useStore((s) => s.company)
  const currency = company?.currencyCode ?? 'USD'

  const { data: stats, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => {
      const { data } = await apiClient.get<DashboardStats>('/reports/dashboard')
      return data
    }
  })

  if (isLoading) return <div className="py-20 text-center text-gray-400">Loading...</div>

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Dashboard</h1>

      {/* KPI tiles */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <KpiTile label="Total orders" value={String(stats?.totalOrders ?? 0)} />
        <KpiTile label="Total spend (YTD)" value={formatPrice(stats?.totalSpend ?? 0, currency)} highlight />
        <KpiTile label="Pending approvals" value={String(stats?.pendingApprovals ?? 0)} warning={(stats?.pendingApprovals ?? 0) > 0} />
        <KpiTile label="Open quotes" value={String(stats?.openQuotes ?? 0)} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Spend by category */}
        <div className="bg-white rounded-lg shadow-sm p-4">
          <h3 className="font-semibold text-gray-800 mb-4">Spend by category</h3>
          <div className="space-y-3">
            {stats?.topCategories.map((cat) => {
              const pct = stats.totalSpend > 0 ? (cat.spend / stats.totalSpend) * 100 : 0
              return (
                <div key={cat.name}>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-700">{cat.name}</span>
                    <span className="font-medium">{formatPrice(cat.spend, currency)}</span>
                  </div>
                  <div className="h-1.5 bg-gray-100 rounded-full">
                    <div
                      className="h-full bg-primary-500 rounded-full"
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </div>
              )
            })}
          </div>
        </div>

        {/* Recent activity */}
        <div className="bg-white rounded-lg shadow-sm p-4">
          <h3 className="font-semibold text-gray-800 mb-4">Recent activity</h3>
          <div className="space-y-3">
            {stats?.recentActivity.map((activity, i) => (
              <div key={i} className="flex gap-3 text-sm">
                <div className="w-2 h-2 rounded-full bg-primary-400 mt-1.5 shrink-0" />
                <div>
                  <div className="text-gray-800">{activity.description}</div>
                  <div className="text-xs text-gray-400">{new Date(activity.at).toLocaleString()}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

function KpiTile({ label, value, highlight, warning }: {
  label: string
  value: string
  highlight?: boolean
  warning?: boolean
}) {
  return (
    <div className={`bg-white rounded-lg shadow-sm p-4 ${warning ? 'border-l-4 border-amber-400' : ''}`}>
      <div className="text-xs text-gray-500 mb-1">{label}</div>
      <div className={`text-2xl font-bold ${highlight ? 'text-primary-700' : warning ? 'text-amber-600' : 'text-gray-900'}`}>
        {value}
      </div>
    </div>
  )
}
