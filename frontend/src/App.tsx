import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useEffect } from 'react'
import { useStore } from './hooks/useStore'
import { companyService } from './services/company.service'
import { authService } from './services/auth.service'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import CatalogPage from './pages/CatalogPage'
import ProductDetailPage from './pages/ProductDetailPage'
import CartPage from './pages/CartPage'
import CheckoutPage from './pages/CheckoutPage'
import OrderHistoryPage from './pages/OrderHistoryPage'
import OrderDetailPage from './pages/OrderDetailPage'
import ApprovalsPage from './pages/ApprovalsPage'
import ShoppingListsPage from './pages/ShoppingListsPage'
import QuotesPage from './pages/QuotesPage'
import AdminPage from './pages/AdminPage'
import DashboardPage from './pages/DashboardPage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useStore((s) => s.isAuthenticated)
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return <>{children}</>
}

export default function App() {
  const { isAuthenticated, setUser, setCompany, setUiConfig } = useStore()

  useEffect(() => {
    if (!isAuthenticated) return
    authService.getCurrentUser()
      .then(setUser)
      .catch(() => {})
    companyService.getMyCompany()
      .then((company) => {
        setCompany(company)
        // Apply tenant theming from DB-driven config
        if (company.uiConfig) {
          document.documentElement.style.setProperty('--color-primary-500', company.uiConfig.primaryColor)
          document.documentElement.style.setProperty('--color-primary-600', company.uiConfig.primaryColor)
        }
      })
      .catch(() => {})
    companyService.getUiConfig()
      .then(setUiConfig)
      .catch(() => {})
  }, [isAuthenticated])

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="/catalog" replace />} />
          <Route path="catalog" element={<CatalogPage />} />
          <Route path="catalog/:articleId" element={<ProductDetailPage />} />
          <Route path="cart" element={<CartPage />} />
          <Route path="checkout" element={<CheckoutPage />} />
          <Route path="orders" element={<OrderHistoryPage />} />
          <Route path="orders/:orderId" element={<OrderDetailPage />} />
          <Route path="approvals" element={<ApprovalsPage />} />
          <Route path="lists" element={<ShoppingListsPage />} />
          <Route path="quotes" element={<QuotesPage />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="admin/*" element={<AdminPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
