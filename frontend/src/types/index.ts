// Deliberately inconsistent naming across modules — as if built by different teams over years

// --- Tenant / Company / Account (naming inconsistency intentional) ---
export interface Company {
  id: string
  code: string
  name: string
  taxId?: string
  countryCode: string
  currencyCode: string
  languageCode: string
  isActive: boolean
  uiConfig?: CompanyUiConfig
  pricingEngineVersion: 'legacy' | 'v2'
}

export interface CompanyUiConfig {
  logoUrl?: string
  primaryColor: string
  secondaryColor: string
  catalogLayout: 'grid' | 'list' | 'table'
  showStockLevel: boolean
  showProductCode: boolean
  customLabels: Record<string, string>
  enabledSections: string[]
  dateFormat: string
  numberFormat: 'dot' | 'comma'
}

// --- User / Account roles ---
export type UserRole = 'platform_admin' | 'company_admin' | 'buyer' | 'approver' | 'sales_rep' | 'auditor'

export interface User {
  id: string
  email: string
  displayName: string
  role: UserRole
  companyId?: string
  isActive: boolean
}

// --- Article / Product / Item (naming inconsistency intentional) ---
export interface Article {
  id: string
  code: string
  name: string
  description?: string
  categoryId: string
  basePrice: number
  currencyCode: string
  unitOfMeasure: string
  packSize: number
  weight?: number
  isActive: boolean
  imageUrl?: string
  attributes: Record<string, string>
  stockLevel?: number
  variants?: ArticleVariant[]
}

export interface ArticleVariant {
  id: string
  articleId: string
  variantCode: string
  attributes: Record<string, string>
  extraPrice: number
  stock: number
}

export interface ProductCategory {
  id: string
  code: string
  name: string
  parentId?: string
  sortOrder: number
  isActive: boolean
}

// Catalog item as seen by a specific company — resolved after assortment + pricing
export interface CatalogItem {
  articleId: string
  code: string
  name: string
  description?: string
  categoryId: string
  categoryName: string
  unitOfMeasure: string
  packSize: number
  imageUrl?: string
  attributes: Record<string, string>
  resolvedPrice: PriceResult
  isOrderable: boolean
  stockLevel?: number
  variants?: ArticleVariant[]
}

export interface PriceResult {
  unitPrice: number
  displayPrice: number
  originalPrice: number
  currencyCode: string
  taxRate: number
  taxAmount: number
  totalWithTax: number
  appliedTiers?: PriceTier[]
  appliedPromotions?: string[]
  priceType: 'list' | 'company' | 'contract' | 'volume' | 'promotional'
  validUntil?: string
}

export interface PriceTier {
  minQuantity: number
  maxQuantity?: number
  unitPrice: number
  label?: string
}

// --- Order / Cart ---
export type OrderStatus =
  | 'draft'
  | 'pending_approval'
  | 'approved'
  | 'transferred'
  | 'acknowledged'
  | 'partially_fulfilled'
  | 'fulfilled'
  | 'cancelled'
  | 'returned'

export interface OrderLine {
  id: string
  articleId: string
  articleCode: string
  articleName: string
  variantId?: string
  quantity: number
  unitOfMeasure: string
  unitPrice: number
  lineTotal: number
  discountPercent: number
  notes?: string
}

export interface Order {
  id: string
  orderNumber: string
  companyId: string
  userId: string
  status: OrderStatus
  lines: OrderLine[]
  subtotal: number
  discountTotal: number
  taxTotal: number
  grandTotal: number
  currencyCode: string
  deliveryAddressId?: string
  poNumber?: string
  requestedDeliveryDate?: string
  approvalRequired: boolean
  approverId?: string
  notes?: string
  createdAt: string
  updatedAt: string
  transferredAt?: string
  acknowledgedAt?: string
}

// --- Checkout ---
export type CheckoutStepType =
  | 'cart_review'
  | 'delivery_address'
  | 'payment_terms'
  | 'po_number'
  | 'notes'
  | 'split_delivery'
  | 'approval'
  | 'confirmation'

export interface CheckoutStep {
  stepType: CheckoutStepType
  label: string
  isRequired: boolean
  sortOrder: number
  config: Record<string, unknown>
}

export interface CheckoutConfig {
  companyId: string
  steps: CheckoutStep[]
  requiresApproval: boolean
  approvalSpendThreshold?: number
  paymentTerms: string[]
  allowFreeAddressEntry: boolean
  requirePoNumber: boolean
  confirmationChannel: 'email' | 'webhook' | 'none'
}

// --- Shopping List ---
export interface ShoppingList {
  id: string
  name: string
  companyId: string
  userId: string
  isShared: boolean
  lines: ShoppingListLine[]
  createdAt: string
}

export interface ShoppingListLine {
  id: string
  articleId: string
  articleCode: string
  articleName: string
  quantity: number
  notes?: string
}

// --- Quotes / RFQ ---
export type QuoteStatus = 'draft' | 'submitted' | 'reviewing' | 'accepted' | 'rejected' | 'expired'

export interface Quote {
  id: string
  quoteNumber: string
  companyId: string
  userId: string
  status: QuoteStatus
  lines: QuoteLine[]
  validUntil?: string
  notes?: string
  createdAt: string
}

export interface QuoteLine {
  id: string
  articleId: string
  articleCode: string
  articleName: string
  requestedQuantity: number
  requestedPrice?: number
  offeredPrice?: number
  notes?: string
}

// --- Notifications ---
export interface Notification {
  id: string
  userId: string
  type: string
  title: string
  message: string
  isRead: boolean
  relatedEntityId?: string
  relatedEntityType?: string
  createdAt: string
}

// --- Pagination ---
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// --- Auth ---
export interface LoginRequest {
  email: string
  password: string
}

export interface AuthToken {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: User
}

// --- Address ---
export interface DeliveryAddress {
  id: string
  companyId: string
  label: string
  street: string
  city: string
  postalCode: string
  countryCode: string
  isDefault: boolean
  contactName?: string
  contactPhone?: string
}

// --- Catalog filters ---
export interface CatalogFilter {
  categoryId?: string
  search?: string
  minPrice?: number
  maxPrice?: number
  attributes?: Record<string, string[]>
  inStockOnly?: boolean
  page?: number
  pageSize?: number
  sortBy?: 'name' | 'price_asc' | 'price_desc' | 'code'
}
