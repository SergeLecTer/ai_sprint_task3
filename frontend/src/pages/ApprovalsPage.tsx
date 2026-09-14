import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import apiClient from '../services/api'
import { useStore } from '../hooks/useStore'
import { formatPrice, formatDate } from '../utils/format'
import type { Order } from '../types'

export default function ApprovalsPage() {
  const qc = useQueryClient()
  const uiConfig = useStore((s) => s.uiConfig)

  const { data: pendingOrders, isLoading } = useQuery({
    queryKey: ['approvals'],
    queryFn: async () => {
      const { data } = await apiClient.get<Order[]>('/orders/pending-approval')
      return data
    }
  })

  const approveMutation = useMutation({
    mutationFn: async (orderId: string) => {
      await apiClient.post(`/orders/${orderId}/approve`)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['approvals'] })
  })

  const rejectMutation = useMutation({
    mutationFn: async (orderId: string) => {
      await apiClient.post(`/orders/${orderId}/reject`, { reason: 'Rejected by approver' })
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['approvals'] })
  })

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Pending approvals</h1>
      {isLoading ? (
        <div className="py-20 text-center text-gray-400">Loading...</div>
      ) : (
        <div className="space-y-4">
          {pendingOrders?.map((order) => (
            <div key={order.id} className="bg-white rounded-lg shadow-sm p-4 flex items-center gap-4">
              <div className="flex-1">
                <div className="font-medium text-gray-800">{order.orderNumber}</div>
                <div className="text-sm text-gray-500">
                  {formatDate(order.createdAt, uiConfig?.dateFormat)} · {order.lines.length} line{order.lines.length !== 1 ? 's' : ''}
                </div>
              </div>
              <div className="text-right">
                <div className="font-semibold text-gray-900">{formatPrice(order.grandTotal, order.currencyCode)}</div>
              </div>
              <div className="flex gap-2">
                <Link to={`/orders/${order.id}`} className="text-xs text-primary-600 hover:underline px-2">Review</Link>
                <button
                  onClick={() => approveMutation.mutate(order.id)}
                  className="bg-green-600 text-white text-xs px-3 py-1.5 rounded hover:bg-green-700"
                >
                  Approve
                </button>
                <button
                  onClick={() => rejectMutation.mutate(order.id)}
                  className="border border-red-300 text-red-600 text-xs px-3 py-1.5 rounded hover:bg-red-50"
                >
                  Reject
                </button>
              </div>
            </div>
          ))}
          {pendingOrders?.length === 0 && (
            <div className="py-20 text-center text-gray-400">No orders pending approval</div>
          )}
        </div>
      )}
    </div>
  )
}
