import { Routes, Route, Link, useLocation } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import apiClient from '../services/api'
import type { Company, CompanyUiConfig } from '../types'

export default function AdminPage() {
  const location = useLocation()
  const sections = [
    { path: '/admin', label: 'Companies', exact: true },
    { path: '/admin/pricing', label: 'Price lists' },
    { path: '/admin/assortments', label: 'Assortments' },
    { path: '/admin/checkout', label: 'Checkout config' },
    { path: '/admin/integrations', label: 'Integrations' },
    { path: '/admin/users', label: 'Users' },
  ]

  return (
    <div className="flex gap-6">
      <aside className="w-48 shrink-0">
        <div className="bg-white rounded-lg shadow-sm p-3">
          <div className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2 px-2">Admin</div>
          {sections.map((s) => (
            <Link
              key={s.path}
              to={s.path}
              className={`block px-3 py-2 rounded text-sm ${location.pathname === s.path ? 'bg-primary-50 text-primary-700 font-medium' : 'text-gray-600 hover:bg-gray-50'}`}
            >
              {s.label}
            </Link>
          ))}
        </div>
      </aside>
      <div className="flex-1">
        <Routes>
          <Route index element={<AdminCompanies />} />
          <Route path="pricing" element={<AdminPricing />} />
          <Route path="assortments" element={<AdminAssortments />} />
          <Route path="checkout" element={<AdminCheckout />} />
          <Route path="integrations" element={<AdminIntegrations />} />
          <Route path="users" element={<AdminUsers />} />
        </Routes>
      </div>
    </div>
  )
}

function AdminCompanies() {
  const qc = useQueryClient()
  const { data: companies, isLoading } = useQuery({
    queryKey: ['admin-companies'],
    queryFn: async () => {
      const { data } = await apiClient.get<Company[]>('/admin/companies')
      return data
    }
  })

  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Companies</h2>
      {isLoading ? <div className="text-gray-400">Loading...</div> : (
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Code</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Name</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Currency</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Pricing engine</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {companies?.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-xs text-gray-500">{c.code}</td>
                  <td className="px-4 py-3 font-medium text-gray-800">{c.name}</td>
                  <td className="px-4 py-3 text-gray-600">{c.currencyCode}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-xs font-medium ${c.pricingEngineVersion === 'legacy' ? 'bg-orange-100 text-orange-700' : 'bg-green-100 text-green-700'}`}>
                      {c.pricingEngineVersion === 'legacy' ? 'Legacy' : 'v2'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`w-2 h-2 rounded-full inline-block mr-1.5 ${c.isActive ? 'bg-green-500' : 'bg-red-400'}`} />
                    {c.isActive ? 'Active' : 'Inactive'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AdminPricing() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-price-lists'],
    queryFn: async () => {
      const { data } = await apiClient.get('/admin/price-lists')
      return data as { id: string; name: string; companyCode: string; validFrom: string; validTo?: string; lineCount: number }[]
    }
  })

  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Price lists</h2>
      {isLoading ? <div className="text-gray-400">Loading...</div> : (
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Name</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Company</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Valid from</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Valid to</th>
                <th className="text-right px-4 py-3 text-xs text-gray-500 font-medium">Lines</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data?.map((pl) => (
                <tr key={pl.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{pl.name}</td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-500">{pl.companyCode}</td>
                  <td className="px-4 py-3 text-gray-600">{new Date(pl.validFrom).toLocaleDateString()}</td>
                  <td className="px-4 py-3 text-gray-600">{pl.validTo ? new Date(pl.validTo).toLocaleDateString() : '–'}</td>
                  <td className="px-4 py-3 text-right text-gray-600">{pl.lineCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AdminAssortments() {
  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Assortments</h2>
      <div className="bg-white rounded-lg shadow-sm p-6 text-gray-400 text-sm">
        Manage company-to-product and company-to-category assignments here.
      </div>
    </div>
  )
}

function AdminCheckout() {
  const { data: companies } = useQuery({
    queryKey: ['admin-companies'],
    queryFn: async () => {
      const { data } = await apiClient.get<Company[]>('/admin/companies')
      return data
    }
  })

  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Checkout configuration</h2>
      <div className="space-y-3">
        {companies?.map((c) => (
          <div key={c.id} className="bg-white rounded-lg shadow-sm p-4 flex items-center justify-between">
            <div>
              <div className="font-medium text-gray-800">{c.name}</div>
              <div className="text-xs text-gray-400 font-mono">{c.code}</div>
            </div>
            <button className="text-xs text-primary-600 hover:underline border border-primary-200 px-3 py-1 rounded">
              Edit config
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}

function AdminIntegrations() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-integrations'],
    queryFn: async () => {
      const { data } = await apiClient.get('/admin/integrations')
      return data as { id: string; companyCode: string; transport: string; format: string; isActive: boolean; lastTransferAt?: string }[]
    }
  })

  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Integration configurations</h2>
      {isLoading ? <div className="text-gray-400">Loading...</div> : (
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Company</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Transport</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Format</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Last transfer</th>
                <th className="text-left px-4 py-3 text-xs text-gray-500 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data?.map((cfg) => (
                <tr key={cfg.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono text-xs text-gray-500">{cfg.companyCode}</td>
                  <td className="px-4 py-3 text-gray-700">{cfg.transport}</td>
                  <td className="px-4 py-3 text-gray-700">{cfg.format}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">
                    {cfg.lastTransferAt ? new Date(cfg.lastTransferAt).toLocaleString() : 'Never'}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`w-2 h-2 rounded-full inline-block mr-1.5 ${cfg.isActive ? 'bg-green-500' : 'bg-gray-400'}`} />
                    {cfg.isActive ? 'Active' : 'Disabled'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AdminUsers() {
  return (
    <div>
      <h2 className="text-xl font-bold text-gray-900 mb-4">Users</h2>
      <div className="bg-white rounded-lg shadow-sm p-6 text-gray-400 text-sm">
        User management interface.
      </div>
    </div>
  )
}
