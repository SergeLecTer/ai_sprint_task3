import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { catalogService } from '../services/catalog.service'
import { useStore } from '../hooks/useStore'
import type { CatalogFilter, CatalogItem } from '../types'
import { SearchIcon, FilterIcon, GridIcon, ListIcon } from '../components/icons'
import { formatPrice } from '../utils/format'

export default function CatalogPage() {
  const uiConfig = useStore((s) => s.uiConfig)
  const addLine = useStore((s) => s.addLine)

  // Layout driven by DB config — grid, list, or table
  const defaultLayout = uiConfig?.catalogLayout ?? 'grid'
  const [layout, setLayout] = useState<'grid' | 'list' | 'table'>(defaultLayout)
  const [filter, setFilter] = useState<CatalogFilter>({ page: 1, pageSize: 24 })
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['catalog', filter],
    queryFn: () => catalogService.getItems(filter)
  })

  const { data: categories } = useQuery({
    queryKey: ['categories'],
    queryFn: () => catalogService.getCategories()
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setFilter((f) => ({ ...f, search, page: 1 }))
  }

  function handleAddToCart(item: CatalogItem) {
    addLine({
      articleId: item.articleId,
      articleCode: item.code,
      articleName: item.name,
      quantity: 1,
      unitPrice: item.resolvedPrice.unitPrice,
      lineTotal: item.resolvedPrice.unitPrice,
      unitOfMeasure: item.unitOfMeasure
    })
  }

  const showProductCode = uiConfig?.showProductCode ?? true
  const showStock = uiConfig?.showStockLevel ?? false
  const labels = uiConfig?.customLabels ?? {}

  return (
    <div className="flex gap-6">
      {/* Sidebar filters */}
      <aside className="w-56 shrink-0">
        <div className="bg-white rounded-lg shadow-sm p-4">
          <h3 className="font-semibold text-gray-700 mb-3 flex items-center gap-2">
            <FilterIcon className="w-4 h-4" />
            {labels['filter.title'] ?? 'Filters'}
          </h3>
          <div className="space-y-2">
            <button
              onClick={() => setFilter((f) => ({ ...f, categoryId: undefined, page: 1 }))}
              className={`w-full text-left text-sm px-2 py-1 rounded ${!filter.categoryId ? 'bg-primary-50 text-primary-700 font-medium' : 'text-gray-600 hover:bg-gray-50'}`}
            >
              All categories
            </button>
            {categories?.map((cat) => (
              <button
                key={cat.id}
                onClick={() => setFilter((f) => ({ ...f, categoryId: cat.id, page: 1 }))}
                className={`w-full text-left text-sm px-2 py-1 rounded ${filter.categoryId === cat.id ? 'bg-primary-50 text-primary-700 font-medium' : 'text-gray-600 hover:bg-gray-50'}`}
              >
                {cat.name}
              </button>
            ))}
          </div>
          {/* Stock filter — only shown if DB config enables it */}
          {showStock && (
            <div className="mt-4 pt-3 border-t border-gray-100">
              <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
                <input
                  type="checkbox"
                  checked={filter.inStockOnly ?? false}
                  onChange={(e) => setFilter((f) => ({ ...f, inStockOnly: e.target.checked, page: 1 }))}
                  className="rounded"
                />
                In stock only
              </label>
            </div>
          )}
        </div>
      </aside>

      {/* Main catalog area */}
      <div className="flex-1">
        {/* Search + controls */}
        <div className="flex items-center gap-3 mb-4">
          <form onSubmit={handleSearch} className="flex-1 flex gap-2">
            <div className="relative flex-1">
              <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={labels['search.placeholder'] ?? 'Search products...'}
                className="w-full pl-9 pr-3 py-2 border border-gray-300 rounded text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>
            <button type="submit" className="bg-primary-600 text-white px-4 py-2 rounded text-sm hover:bg-primary-700">
              Search
            </button>
          </form>
          <select
            value={filter.sortBy ?? 'name'}
            onChange={(e) => setFilter((f) => ({ ...f, sortBy: e.target.value as CatalogFilter['sortBy'] }))}
            className="border border-gray-300 rounded px-2 py-2 text-sm"
          >
            <option value="name">Name</option>
            <option value="price_asc">Price: low to high</option>
            <option value="price_desc">Price: high to low</option>
            <option value="code">Code</option>
          </select>
          {/* Layout toggle — grid/list/table (if catalog doesn't lock it) */}
          <div className="flex gap-1">
            <button
              onClick={() => setLayout('grid')}
              className={`p-2 rounded ${layout === 'grid' ? 'bg-primary-100 text-primary-700' : 'text-gray-400 hover:text-gray-600'}`}
            >
              <GridIcon className="w-4 h-4" />
            </button>
            <button
              onClick={() => setLayout('list')}
              className={`p-2 rounded ${layout === 'list' ? 'bg-primary-100 text-primary-700' : 'text-gray-400 hover:text-gray-600'}`}
            >
              <ListIcon className="w-4 h-4" />
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="text-center text-gray-400 py-20">Loading...</div>
        ) : layout === 'grid' ? (
          <CatalogGrid items={data?.items ?? []} onAdd={handleAddToCart} showCode={showProductCode} showStock={showStock} labels={labels} />
        ) : layout === 'table' ? (
          <CatalogTable items={data?.items ?? []} onAdd={handleAddToCart} showCode={showProductCode} labels={labels} />
        ) : (
          <CatalogList items={data?.items ?? []} onAdd={handleAddToCart} showCode={showProductCode} showStock={showStock} labels={labels} />
        )}

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex justify-center gap-2 mt-6">
            {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((page) => (
              <button
                key={page}
                onClick={() => setFilter((f) => ({ ...f, page }))}
                className={`w-8 h-8 rounded text-sm ${filter.page === page ? 'bg-primary-600 text-white' : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'}`}
              >
                {page}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function CatalogGrid({ items, onAdd, showCode, showStock, labels }: {
  items: CatalogItem[]
  onAdd: (item: CatalogItem) => void
  showCode: boolean
  showStock: boolean
  labels: Record<string, string>
}) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
      {items.map((item) => (
        <div key={item.articleId} className="bg-white rounded-lg shadow-sm overflow-hidden flex flex-col">
          <Link to={`/catalog/${item.articleId}`}>
            {item.imageUrl ? (
              <img src={item.imageUrl} alt={item.name} className="w-full h-36 object-cover" />
            ) : (
              <div className="w-full h-36 bg-gray-100 flex items-center justify-center text-gray-300 text-sm">No image</div>
            )}
          </Link>
          <div className="p-3 flex flex-col flex-1">
            {showCode && <span className="text-xs text-gray-400 font-mono">{item.code}</span>}
            <Link to={`/catalog/${item.articleId}`} className="text-sm font-medium text-gray-800 hover:text-primary-600 mt-1 line-clamp-2">
              {item.name}
            </Link>
            <div className="mt-auto pt-2">
              <div className="font-semibold text-primary-700">
                {formatPrice(item.resolvedPrice.displayPrice, item.resolvedPrice.currencyCode)}
              </div>
              {item.resolvedPrice.originalPrice > item.resolvedPrice.displayPrice && (
                <div className="text-xs text-gray-400 line-through">
                  {formatPrice(item.resolvedPrice.originalPrice, item.resolvedPrice.currencyCode)}
                </div>
              )}
              {showStock && item.stockLevel !== undefined && (
                <div className={`text-xs mt-1 ${item.stockLevel > 0 ? 'text-green-600' : 'text-red-500'}`}>
                  {item.stockLevel > 0 ? `${item.stockLevel} in stock` : 'Out of stock'}
                </div>
              )}
              <button
                onClick={() => onAdd(item)}
                disabled={!item.isOrderable}
                className="mt-2 w-full bg-primary-600 text-white text-xs py-1.5 rounded hover:bg-primary-700 disabled:opacity-40"
              >
                {labels['btn.addToCart'] ?? 'Add to cart'}
              </button>
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

function CatalogList({ items, onAdd, showCode, showStock, labels }: {
  items: CatalogItem[]
  onAdd: (item: CatalogItem) => void
  showCode: boolean
  showStock: boolean
  labels: Record<string, string>
}) {
  return (
    <div className="space-y-2">
      {items.map((item) => (
        <div key={item.articleId} className="bg-white rounded-lg shadow-sm p-4 flex items-center gap-4">
          {item.imageUrl ? (
            <img src={item.imageUrl} alt={item.name} className="w-16 h-16 object-cover rounded" />
          ) : (
            <div className="w-16 h-16 bg-gray-100 rounded flex items-center justify-center text-gray-300 text-xs shrink-0">No img</div>
          )}
          <div className="flex-1 min-w-0">
            {showCode && <div className="text-xs text-gray-400 font-mono">{item.code}</div>}
            <Link to={`/catalog/${item.articleId}`} className="text-sm font-medium text-gray-800 hover:text-primary-600">
              {item.name}
            </Link>
            <div className="text-xs text-gray-500 mt-1">{item.categoryName} · {item.unitOfMeasure}</div>
          </div>
          <div className="text-right shrink-0">
            <div className="font-semibold text-primary-700">
              {formatPrice(item.resolvedPrice.displayPrice, item.resolvedPrice.currencyCode)}
            </div>
            {showStock && item.stockLevel !== undefined && (
              <div className={`text-xs ${item.stockLevel > 0 ? 'text-green-600' : 'text-red-500'}`}>
                {item.stockLevel > 0 ? `${item.stockLevel}` : 'OOS'}
              </div>
            )}
            <button
              onClick={() => onAdd(item)}
              disabled={!item.isOrderable}
              className="mt-1 bg-primary-600 text-white text-xs px-3 py-1 rounded hover:bg-primary-700 disabled:opacity-40"
            >
              {labels['btn.addToCart'] ?? 'Add'}
            </button>
          </div>
        </div>
      ))}
    </div>
  )
}

function CatalogTable({ items, onAdd, showCode, labels }: {
  items: CatalogItem[]
  onAdd: (item: CatalogItem) => void
  showCode: boolean
  labels: Record<string, string>
}) {
  return (
    <div className="bg-white rounded-lg shadow-sm overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="bg-gray-50 border-b border-gray-200">
          <tr>
            {showCode && <th className="text-left px-4 py-2 text-xs text-gray-500 font-medium">Code</th>}
            <th className="text-left px-4 py-2 text-xs text-gray-500 font-medium">Product</th>
            <th className="text-left px-4 py-2 text-xs text-gray-500 font-medium">Category</th>
            <th className="text-left px-4 py-2 text-xs text-gray-500 font-medium">UoM</th>
            <th className="text-right px-4 py-2 text-xs text-gray-500 font-medium">Price</th>
            <th className="px-4 py-2"></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {items.map((item) => (
            <tr key={item.articleId} className="hover:bg-gray-50">
              {showCode && <td className="px-4 py-2 font-mono text-xs text-gray-400">{item.code}</td>}
              <td className="px-4 py-2">
                <Link to={`/catalog/${item.articleId}`} className="font-medium text-gray-800 hover:text-primary-600">
                  {item.name}
                </Link>
              </td>
              <td className="px-4 py-2 text-gray-500">{item.categoryName}</td>
              <td className="px-4 py-2 text-gray-500">{item.unitOfMeasure}</td>
              <td className="px-4 py-2 text-right font-semibold text-primary-700">
                {formatPrice(item.resolvedPrice.displayPrice, item.resolvedPrice.currencyCode)}
              </td>
              <td className="px-4 py-2 text-right">
                <button
                  onClick={() => onAdd(item)}
                  disabled={!item.isOrderable}
                  className="bg-primary-600 text-white text-xs px-3 py-1 rounded hover:bg-primary-700 disabled:opacity-40"
                >
                  {labels['btn.addToCart'] ?? 'Add'}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
