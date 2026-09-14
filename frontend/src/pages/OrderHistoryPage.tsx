import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { orderService } from '../services/order.service'
import { useStore } from '../hooks/useStore'
import { formatPrice, formatDate } from '../utils/format'
import type { OrderStatus } from '../types'

const STATUS_COLORS: Record<OrderStatus, string> = {
  draft: 'bg-gray-100 text-gray-600',
  pending_approval: 'bg-yellow-100 text-yellow-700',
  approved: 'bg-blue-100 text-blue-700',
  transferred: 'bg-indigo-100 text-indigo-700',
  acknowledged: 'bg-purple-100 text-purple-700',
  partially_fulfilled: 'bg-orange-100 text-orange-700',
  fulfilled: 'bg-green-100 text-green-700',
  cancelled: 'bg-red-100 text-red-700',
  returned: 'bg-pink-100 text-pink-700'
}

export default function OrderHistoryPage() {
  const currency = useStore((s) => s.company?.currencyCode ?? 'USD')
  const uiConfig = useStore((s) => s.uiConfig)
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['orders', statusFilter, page],
    queryFn: () => orderService.getOrders({ status: statusFilter || undefined, page, pageSize: 20 })
  })

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Orders</h1>
        <Link
          to="/checkout"
          className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700"
        >
          New order
        </Link>
      </div>

      {/* Filters */}
      <div className="flex gap-2 mb-4">
        {['', 'draft', 'pending_approval', 'approved', 'transferred', 'fulfilled', 'cancelled'].map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1) }}
            className={`px-3 py-1 rounded-full text-xs font-medium border ${statusFilter === s ? 'bg-primary-600 text-white border-primary-600' : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}
          >
            {s || 'All'}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="py-20 text-center text-gray-400">Loading...</div>
      ) : (
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Order #</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Date</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Status</th>
                <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Total</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data?.items.map((order) => (
                <tr key={order.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-xs font-medium text-gray-800">{order.orderNumber}</td>
                  <td className="px-4 py-3 text-gray-600">
                    {formatDate(order.createdAt, uiConfig?.dateFormat)}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${STATUS_COLORS[order.status]}`}>
                      {order.status.replace('_', ' ')}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right font-semibold">
                    {formatPrice(order.grandTotal, order.currencyCode)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Link to={`/orders/${order.id}`} className="text-xs text-primary-600 hover:underline">
                      View
                    </Link>
                  </td>
                </tr>
              ))}
              {data?.items.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-gray-400">No orders found</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
