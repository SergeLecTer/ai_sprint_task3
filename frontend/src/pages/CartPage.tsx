import { useNavigate } from 'react-router-dom'
import { useStore } from '../hooks/useStore'
import { formatPrice } from '../utils/format'
import { XIcon } from '../components/icons'

export default function CartPage() {
  const navigate = useNavigate()
  const { lines, updateLine, removeLine, clearCart, uiConfig } = useStore()
  const labels = uiConfig?.customLabels ?? {}
  const currency = useStore((s) => s.company?.currencyCode ?? 'USD')

  const subtotal = lines.reduce((sum, l) => sum + l.lineTotal, 0)

  if (lines.length === 0) {
    return (
      <div className="py-20 text-center">
        <div className="text-gray-400 text-lg mb-4">{labels['cart.empty'] ?? 'Your cart is empty'}</div>
        <button
          onClick={() => navigate('/catalog')}
          className="bg-primary-600 text-white px-6 py-2 rounded hover:bg-primary-700"
        >
          {labels['btn.continueShopping'] ?? 'Continue shopping'}
        </button>
      </div>
    )
  }

  return (
    <div className="max-w-3xl mx-auto">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">{labels['cart.title'] ?? 'Cart'}</h1>
      <div className="bg-white rounded-lg shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Product</th>
              <th className="text-center px-4 py-3 text-xs text-gray-500 font-medium">Qty</th>
              <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Unit price</th>
              <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Total</th>
              <th className="px-4 py-3"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {lines.map((line) => (
              <tr key={line.articleId} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="font-medium text-gray-800">{line.articleName}</div>
                  <div className="text-xs text-gray-400 font-mono">{line.articleCode}</div>
                </td>
                <td className="px-4 py-3 text-center">
                  <div className="flex items-center justify-center gap-1">
                    <button
                      onClick={() => updateLine(line.articleId, line.quantity - 1)}
                      className="w-6 h-6 rounded border border-gray-300 hover:bg-gray-100 text-gray-600 flex items-center justify-center"
                    >
                      -
                    </button>
                    <span className="w-8 text-center">{line.quantity}</span>
                    <button
                      onClick={() => updateLine(line.articleId, line.quantity + 1)}
                      className="w-6 h-6 rounded border border-gray-300 hover:bg-gray-100 text-gray-600 flex items-center justify-center"
                    >
                      +
                    </button>
                  </div>
                </td>
                <td className="px-4 py-3 text-right">{formatPrice(line.unitPrice, currency)}</td>
                <td className="px-4 py-3 text-right font-semibold">{formatPrice(line.lineTotal, currency)}</td>
                <td className="px-4 py-3 text-right">
                  <button
                    onClick={() => removeLine(line.articleId)}
                    className="text-gray-400 hover:text-red-500 p-1"
                  >
                    <XIcon className="w-4 h-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-4 flex justify-between items-center">
        <button
          onClick={clearCart}
          className="text-sm text-red-500 hover:underline"
        >
          Clear cart
        </button>
        <div className="text-right">
          <div className="text-sm text-gray-500">Subtotal</div>
          <div className="text-2xl font-bold text-gray-900">{formatPrice(subtotal, currency)}</div>
          <div className="text-xs text-gray-400 mt-0.5">Taxes and shipping calculated at checkout</div>
        </div>
      </div>

      <div className="mt-6 flex justify-end gap-3">
        <button
          onClick={() => navigate('/catalog')}
          className="border border-gray-300 text-gray-700 px-5 py-2 rounded hover:bg-gray-50 text-sm"
        >
          Continue shopping
        </button>
        <button
          onClick={() => navigate('/checkout')}
          className="bg-primary-600 text-white px-6 py-2 rounded hover:bg-primary-700 text-sm font-medium"
        >
          {labels['btn.proceedToCheckout'] ?? 'Proceed to checkout'}
        </button>
      </div>
    </div>
  )
}
