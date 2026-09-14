import { Outlet, Link, useNavigate } from 'react-router-dom'
import { useStore } from '../hooks/useStore'
import { authService } from '../services/auth.service'
import { ShoppingCartIcon, BellIcon, UserCircleIcon } from './icons'

export default function Layout() {
  const { user, uiConfig, company, unreadCount, logout, totalItems } = useStore()
  const navigate = useNavigate()

  // DB-driven: which sections are visible for this tenant
  const enabledSections = uiConfig?.enabledSections ?? [
    'catalog', 'orders', 'approvals', 'lists', 'quotes', 'dashboard'
  ]
  const labels = uiConfig?.customLabels ?? {}

  const navItems = [
    { key: 'catalog', path: '/catalog', label: labels['nav.catalog'] ?? 'Catalog' },
    { key: 'orders', path: '/orders', label: labels['nav.orders'] ?? 'Orders' },
    { key: 'approvals', path: '/approvals', label: labels['nav.approvals'] ?? 'Approvals' },
    { key: 'lists', path: '/lists', label: labels['nav.lists'] ?? 'Lists' },
    { key: 'quotes', path: '/quotes', label: labels['nav.quotes'] ?? 'Quotes' },
    { key: 'dashboard', path: '/dashboard', label: labels['nav.dashboard'] ?? 'Dashboard' },
  ].filter((item) => enabledSections.includes(item.key))

  const showAdmin = user?.role === 'platform_admin' || user?.role === 'company_admin'

  async function handleLogout() {
    await authService.logout()
    logout()
    navigate('/login')
  }

  return (
    <div className="min-h-screen flex flex-col">
      {/* Header */}
      <header className="bg-primary-600 text-white shadow-md">
        <div className="max-w-7xl mx-auto px-4 flex items-center justify-between h-16">
          <div className="flex items-center gap-4">
            {uiConfig?.logoUrl ? (
              <img src={uiConfig.logoUrl} alt={company?.name} className="h-8 w-auto" />
            ) : (
              <span className="font-bold text-lg">{company?.name ?? 'B2B Shop'}</span>
            )}
          </div>
          <nav className="hidden md:flex items-center gap-6">
            {navItems.map((item) => (
              <Link
                key={item.key}
                to={item.path}
                className="text-sm font-medium text-white/80 hover:text-white transition-colors"
              >
                {item.label}
              </Link>
            ))}
            {showAdmin && (
              <Link to="/admin" className="text-sm font-medium text-yellow-300 hover:text-yellow-100">
                Admin
              </Link>
            )}
          </nav>
          <div className="flex items-center gap-3">
            <Link to="/cart" className="relative p-2 hover:bg-primary-700 rounded">
              <ShoppingCartIcon className="w-5 h-5" />
              {totalItems() > 0 && (
                <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-4 h-4 flex items-center justify-center">
                  {totalItems()}
                </span>
              )}
            </Link>
            <button className="relative p-2 hover:bg-primary-700 rounded">
              <BellIcon className="w-5 h-5" />
              {unreadCount > 0 && (
                <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-4 h-4 flex items-center justify-center">
                  {unreadCount}
                </span>
              )}
            </button>
            <div className="relative group">
              <button className="flex items-center gap-2 p-2 hover:bg-primary-700 rounded">
                <UserCircleIcon className="w-5 h-5" />
                <span className="text-sm hidden lg:block">{user?.displayName}</span>
              </button>
              <div className="absolute right-0 top-full mt-1 bg-white text-gray-800 rounded shadow-lg opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all min-w-40 z-50">
                <div className="px-4 py-2 text-xs text-gray-500 border-b">{user?.role?.replace('_', ' ')}</div>
                <button
                  onClick={handleLogout}
                  className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50"
                >
                  Sign out
                </button>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Main content */}
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 py-6">
        <Outlet />
      </main>

      <footer className="border-t border-gray-200 py-4 text-center text-xs text-gray-400">
        &copy; {new Date().getFullYear()} {company?.name}
      </footer>
    </div>
  )
}
