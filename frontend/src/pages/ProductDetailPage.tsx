import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { catalogService } from '../services/catalog.service'
import { useStore } from '../hooks/useStore'
import { formatPrice } from '../utils/format'
import type { ArticleVariant } from '../types'

export default function ProductDetailPage() {
  const { articleId } = useParams<{ articleId: string }>()
  const navigate = useNavigate()
  const { uiConfig, addLine } = useStore()
  const [quantity, setQuantity] = useState(1)
  const [selectedVariant, setSelectedVariant] = useState<ArticleVariant | null>(null)
  const [addedToCart, setAddedToCart] = useState(false)

  const { data: item, isLoading, error } = useQuery({
    queryKey: ['catalog-item', articleId],
    queryFn: () => catalogService.getItem(articleId!),
    enabled: !!articleId
  })

  const { data: related } = useQuery({
    queryKey: ['related', articleId],
    queryFn: () => catalogService.getRelatedItems(articleId!),
    enabled: !!articleId
  })

  if (isLoading) return <div className="py-20 text-center text-gray-400">Loading...</div>
  if (error || !item) return <div className="py-20 text-center text-red-400">Product not found</div>

  const labels = uiConfig?.customLabels ?? {}
  const showCode = uiConfig?.showProductCode ?? true
  const price = item.resolvedPrice
  const effectivePrice = (selectedVariant?.extraPrice ?? 0) + price.unitPrice

  function handleAddToCart() {
    addLine({
      articleId: item!.articleId,
      articleCode: item!.code,
      articleName: item!.name,
      variantId: selectedVariant?.id,
      quantity,
      unitPrice: effectivePrice,
      lineTotal: effectivePrice * quantity,
      unitOfMeasure: item!.unitOfMeasure
    })
    setAddedToCart(true)
    setTimeout(() => setAddedToCart(false), 2000)
  }

  return (
    <div>
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-400 mb-4">
        <button onClick={() => navigate('/catalog')} className="hover:text-primary-600">Catalog</button>
        <span className="mx-2">/</span>
        <span>{item.categoryName}</span>
        <span className="mx-2">/</span>
        <span className="text-gray-700">{item.name}</span>
      </nav>

      <div className="bg-white rounded-lg shadow-sm p-6 grid grid-cols-1 md:grid-cols-2 gap-8">
        {/* Image */}
        <div>
          {item.imageUrl ? (
            <img src={item.imageUrl} alt={item.name} className="w-full rounded-lg" />
          ) : (
            <div className="w-full aspect-square bg-gray-100 rounded-lg flex items-center justify-center text-gray-300">
              No image available
            </div>
          )}
        </div>

        {/* Details */}
        <div>
          {showCode && <div className="text-sm font-mono text-gray-400 mb-1">{item.code}</div>}
          <h1 className="text-2xl font-bold text-gray-900 mb-2">{item.name}</h1>
          <div className="text-sm text-gray-500 mb-4">{item.categoryName}</div>

          {item.description && (
            <p className="text-sm text-gray-600 mb-4 leading-relaxed">{item.description}</p>
          )}

          {/* Attributes — shown per DB config per company */}
          {Object.keys(item.attributes).length > 0 && (
            <div className="mb-4 grid grid-cols-2 gap-2">
              {Object.entries(item.attributes).map(([key, value]) => (
                <div key={key} className="text-sm">
                  <span className="text-gray-500">{key}: </span>
                  <span className="text-gray-800">{value}</span>
                </div>
              ))}
            </div>
          )}

          {/* Variants */}
          {item.variants && item.variants.length > 0 && (
            <div className="mb-4">
              <div className="text-sm font-medium text-gray-700 mb-2">Variants</div>
              <div className="flex flex-wrap gap-2">
                {item.variants.map((v) => (
                  <button
                    key={v.id}
                    onClick={() => setSelectedVariant(v)}
                    className={`border rounded px-3 py-1 text-sm ${selectedVariant?.id === v.id ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-300 text-gray-600 hover:border-gray-400'}`}
                  >
                    {v.variantCode}
                    {v.extraPrice > 0 && <span className="text-xs ml-1 text-gray-400">+{formatPrice(v.extraPrice, price.currencyCode)}</span>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Price block */}
          <div className="mb-4">
            <div className="text-3xl font-bold text-primary-700">
              {formatPrice(effectivePrice, price.currencyCode)}
              <span className="text-sm font-normal text-gray-500 ml-2">/ {item.unitOfMeasure}</span>
            </div>
            {price.originalPrice > price.unitPrice && (
              <div className="text-sm text-gray-400 line-through">
                {formatPrice(price.originalPrice, price.currencyCode)}
              </div>
            )}
            {/* Price tiers — shown when available */}
            {price.appliedTiers && price.appliedTiers.length > 0 && (
              <div className="mt-2 bg-blue-50 rounded p-3">
                <div className="text-xs font-medium text-blue-700 mb-1">{labels['label.volumePricing'] ?? 'Volume pricing'}</div>
                <table className="text-xs w-full">
                  <tbody>
                    {price.appliedTiers.map((tier, i) => (
                      <tr key={i}>
                        <td className="text-gray-600">{tier.minQuantity}{tier.maxQuantity ? `–${tier.maxQuantity}` : '+'} units</td>
                        <td className="text-right font-medium text-blue-700">{formatPrice(tier.unitPrice, price.currencyCode)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Pack size note */}
          {item.packSize > 1 && (
            <div className="text-xs text-amber-600 mb-3">
              Sold in packs of {item.packSize}
            </div>
          )}

          {/* Quantity + Add to cart */}
          {item.isOrderable ? (
            <div className="flex items-center gap-3">
              <div className="flex items-center border border-gray-300 rounded">
                <button
                  onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                  className="px-3 py-2 text-gray-500 hover:bg-gray-50"
                >
                  -
                </button>
                <input
                  type="number"
                  value={quantity}
                  min={1}
                  onChange={(e) => setQuantity(Math.max(1, parseInt(e.target.value) || 1))}
                  className="w-14 text-center py-2 text-sm border-x border-gray-300 focus:outline-none"
                />
                <button
                  onClick={() => setQuantity((q) => q + 1)}
                  className="px-3 py-2 text-gray-500 hover:bg-gray-50"
                >
                  +
                </button>
              </div>
              <button
                onClick={handleAddToCart}
                className={`flex-1 py-2 rounded font-medium text-sm transition-colors ${addedToCart ? 'bg-green-600 text-white' : 'bg-primary-600 hover:bg-primary-700 text-white'}`}
              >
                {addedToCart ? 'Added!' : (labels['btn.addToCart'] ?? 'Add to cart')}
              </button>
            </div>
          ) : (
            <div className="bg-gray-100 rounded p-3 text-sm text-gray-500 text-center">
              {labels['label.notOrderable'] ?? 'Not available for ordering'}
            </div>
          )}
        </div>
      </div>

      {/* Related products */}
      {related && related.length > 0 && (
        <div className="mt-8">
          <h2 className="text-lg font-semibold text-gray-800 mb-4">Related products</h2>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {related.slice(0, 4).map((rel) => (
              <div key={rel.articleId} className="bg-white rounded shadow-sm p-3">
                <div className="text-sm font-medium text-gray-800 mb-1 line-clamp-2">{rel.name}</div>
                <div className="text-primary-700 font-semibold">
                  {formatPrice(rel.resolvedPrice.displayPrice, rel.resolvedPrice.currencyCode)}
                </div>
                <button
                  onClick={() => navigate(`/catalog/${rel.articleId}`)}
                  className="mt-2 text-xs text-primary-600 hover:underline"
                >
                  View
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
