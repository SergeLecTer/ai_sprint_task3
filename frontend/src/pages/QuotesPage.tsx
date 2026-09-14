import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import apiClient from '../services/api'
import { useStore } from '../hooks/useStore'
import { formatDate } from '../utils/format'
import type { Quote, QuoteStatus } from '../types'

const STATUS_COLORS: Record<QuoteStatus, string> = {
  draft: 'bg-gray-100 text-gray-600',
  submitted: 'bg-blue-100 text-blue-700',
  reviewing: 'bg-yellow-100 text-yellow-700',
  accepted: 'bg-green-100 text-green-700',
  rejected: 'bg-red-100 text-red-700',
  expired: 'bg-gray-100 text-gray-500'
}

export default function QuotesPage() {
  const uiConfig = useStore((s) => s.uiConfig)

  const { data: quotes, isLoading } = useQuery({
    queryKey: ['quotes'],
    queryFn: async () => {
      const { data } = await apiClient.get<Quote[]>('/quotes')
      return data
    }
  })

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Quotes / RFQ</h1>
        <button className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700">
          Request quote
        </button>
      </div>

      {isLoading ? (
        <div className="py-20 text-center text-gray-400">Loading...</div>
      ) : (
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Quote #</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Date</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Status</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Valid until</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {quotes?.map((quote) => (
                <tr key={quote.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-xs font-medium text-gray-800">{quote.quoteNumber}</td>
                  <td className="px-4 py-3 text-gray-600">{formatDate(quote.createdAt, uiConfig?.dateFormat)}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${STATUS_COLORS[quote.status]}`}>
                      {quote.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-600">
                    {quote.validUntil ? formatDate(quote.validUntil, uiConfig?.dateFormat) : '-'}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button className="text-xs text-primary-600 hover:underline">View</button>
                  </td>
                </tr>
              ))}
              {quotes?.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-gray-400">No quotes found</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
