import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { orderService } from '../services/order.service'
import { useStore } from '../hooks/useStore'
import { formatPrice, formatDate } from '../utils/format'
import type { OrderStatus } from '../types'

const STATE_MACHINE: Record<OrderStatus, OrderStatus[]> = {
  draft: ['pending_approval', 'cancelled'],
  pending_approval: ['approved', 'cancelled'],
  approved: ['transferred', 'cancelled'],
  transferred: ['acknowledged', 'cancelled'],
  acknowledged: ['partially_fulfilled', 'fulfilled'],
  partially_fulfilled: ['fulfilled', 'returned'],
  fulfilled: ['returned'],
  cancelled: [],
  returned: []
}

export default function OrderDetailPage() {
  const { orderId } = useParams<{ orderId: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { uiConfig, user } = useStore()
  const labels = uiConfig?.customLabels ?? {}

  const { data: order, isLoading } = useQuery({
    queryKey: ['order', orderId],
    queryFn: () => orderService.getOrder(orderId!)
  })

  const submitMutation = useMutation({
    mutationFn: () => orderService.submitOrder(orderId!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['order', orderId] })
  })

  const cancelMutation = useMutation({
    mutationFn: () => orderService.cancelOrder(orderId!, 'User cancelled'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['order', orderId] })
  })

  const approveMutation = useMutation({
    mutationFn: () => orderService.approveOrder(orderId!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['order', orderId] })
  })

  const reorderMutation = useMutation({
    mutationFn: () => orderService.reorderFromHistory(orderId!),
    onSuccess: (newOrder) => navigate(`/orders/${newOrder.id}`)
  })

  if (isLoading) return <div className="py-20 text-center text-gray-400">Loading...</div>
  if (!order) return <div className="py-20 text-center text-red-400">Order not found</div>

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <button onClick={() => navigate('/orders')} className="text-sm text-gray-400 hover:text-gray-600 mb-1 block">
            ← Orders
          </button>
          <h1 className="text-2xl font-bold text-gray-900">Order {order.orderNumber}</h1>
          <div className="text-sm text-gray-500">{formatDate(order.createdAt, uiConfig?.dateFormat)}</div>
        </div>
        <div className="flex gap-2">
          {order.status === 'draft' && (
            <button
              onClick={() => submitMutation.mutate()}
              className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700"
            >
              Submit
            </button>
          )}
          {order.status === 'pending_approval' && (user?.role === 'approver' || user?.role === 'company_admin') && (
            <button
              onClick={() => approveMutation.mutate()}
              className="bg-green-600 text-white px-4 py-2 rounded text-sm hover:bg-green-700"
            >
              Approve
            </button>
          )}
          {['draft', 'pending_approval'].includes(order.status) && (
            <button
              onClick={() => cancelMutation.mutate()}
              className="border border-red-300 text-red-600 px-4 py-2 rounded text-sm hover:bg-red-50"
            >
              Cancel
            </button>
          )}
          <button
            onClick={() => reorderMutation.mutate()}
            className="border border-gray-300 text-gray-700 px-4 py-2 rounded text-sm hover:bg-gray-50"
          >
            Reorder
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Order lines */}
        <div className="lg:col-span-2">
          <div className="bg-white rounded-lg shadow-sm overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Product</th>
                  <th className="text-center px-4 py-3 text-xs text-gray-500 font-medium">Qty</th>
                  <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Unit price</th>
                  <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {order.lines.map((line) => (
                  <tr key={line.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-800">{line.articleName}</div>
                      <div className="text-xs text-gray-400 font-mono">{line.articleCode}</div>
                    </td>
                    <td className="px-4 py-3 text-center">{line.quantity} {line.unitOfMeasure}</td>
                    <td className="px-4 py-3 text-right">{formatPrice(line.unitPrice, order.currencyCode)}</td>
                    <td className="px-4 py-3 text-right font-semibold">{formatPrice(line.lineTotal, order.currencyCode)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Summary */}
        <div className="space-y-4">
          <div className="bg-white rounded-lg shadow-sm p-4">
            <h3 className="font-semibold text-gray-800 mb-3">Summary</h3>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between"><span className="text-gray-500">Subtotal</span><span>{formatPrice(order.subtotal, order.currencyCode)}</span></div>
              {order.discountTotal > 0 && (
                <div className="flex justify-between text-green-600"><span>Discount</span><span>-{formatPrice(order.discountTotal, order.currencyCode)}</span></div>
              )}
              <div className="flex justify-between"><span className="text-gray-500">Tax</span><span>{formatPrice(order.taxTotal, order.currencyCode)}</span></div>
              <div className="flex justify-between font-bold border-t border-gray-200 pt-2 mt-2">
                <span>Total</span><span>{formatPrice(order.grandTotal, order.currencyCode)}</span>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-lg shadow-sm p-4">
            <h3 className="font-semibold text-gray-800 mb-3">Details</h3>
            <div className="space-y-2 text-sm">
              <div><span className="text-gray-500">Status: </span><span className="font-medium">{order.status.replace('_', ' ')}</span></div>
              {order.poNumber && <div><span className="text-gray-500">PO: </span><span className="font-mono">{order.poNumber}</span></div>}
              {order.transferredAt && <div><span className="text-gray-500">Transferred: </span><span>{formatDate(order.transferredAt)}</span></div>}
              {order.acknowledgedAt && <div><span className="text-gray-500">Acknowledged: </span><span>{formatDate(order.acknowledgedAt)}</span></div>}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
