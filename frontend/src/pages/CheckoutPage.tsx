import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { companyService } from '../services/company.service'
import { orderService } from '../services/order.service'
import { useStore } from '../hooks/useStore'
import { formatPrice } from '../utils/format'
import type { CheckoutStep, DeliveryAddress, CheckoutStepType } from '../types'
import { CheckIcon } from '../components/icons'

interface CheckoutState {
  deliveryAddressId?: string
  poNumber?: string
  paymentTerms?: string
  notes?: string
  requestedDeliveryDate?: string
  splitDelivery?: boolean
}

export default function CheckoutPage() {
  const navigate = useNavigate()
  const { lines, clearCart, uiConfig, company } = useStore()
  const currency = company?.currencyCode ?? 'USD'
  const labels = uiConfig?.customLabels ?? {}

  const [currentStepIndex, setCurrentStepIndex] = useState(0)
  const [state, setState] = useState<CheckoutState>({})
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // DB-driven checkout steps per company
  const { data: checkoutConfig, isLoading } = useQuery({
    queryKey: ['checkout-config'],
    queryFn: () => companyService.getCheckoutConfig()
  })

  const { data: addresses } = useQuery({
    queryKey: ['addresses'],
    queryFn: () => companyService.getDeliveryAddresses()
  })

  if (lines.length === 0) {
    navigate('/cart')
    return null
  }

  const steps = checkoutConfig?.steps ?? []
  const currentStep = steps[currentStepIndex]
  const subtotal = lines.reduce((sum, l) => sum + l.lineTotal, 0)

  async function handleSubmit() {
    setSubmitting(true)
    setError(null)
    try {
      const order = await orderService.createOrder({
        lines: lines.map((l) => ({
          articleId: l.articleId,
          variantId: l.variantId,
          quantity: l.quantity,
          notes: l.notes
        })),
        deliveryAddressId: state.deliveryAddressId,
        poNumber: state.poNumber,
        requestedDeliveryDate: state.requestedDeliveryDate,
        notes: state.notes
      })
      clearCart()
      navigate(`/orders/${order.id}`)
    } catch (e) {
      setError('Failed to place order. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  function canProceed(): boolean {
    if (!currentStep) return false
    if (currentStep.stepType === 'delivery_address' && currentStep.isRequired) {
      if (!checkoutConfig?.allowFreeAddressEntry && !state.deliveryAddressId) return false
    }
    if (currentStep.stepType === 'po_number' && currentStep.isRequired) {
      if (!state.poNumber) return false
    }
    return true
  }

  if (isLoading) return <div className="py-20 text-center text-gray-400">Loading checkout...</div>

  return (
    <div className="max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">{labels['checkout.title'] ?? 'Checkout'}</h1>

      <div className="flex gap-8">
        {/* Steps column */}
        <div className="flex-1">
          {/* Step progress indicator */}
          <div className="flex items-center mb-6 gap-2">
            {steps.map((step, i) => (
              <div key={step.stepType} className="flex items-center gap-2">
                <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold ${
                  i < currentStepIndex ? 'bg-green-500 text-white' :
                  i === currentStepIndex ? 'bg-primary-600 text-white' :
                  'bg-gray-200 text-gray-500'
                }`}>
                  {i < currentStepIndex ? <CheckIcon className="w-3.5 h-3.5" /> : i + 1}
                </div>
                <span className={`text-xs hidden sm:block ${i === currentStepIndex ? 'font-medium text-gray-800' : 'text-gray-400'}`}>
                  {step.label}
                </span>
                {i < steps.length - 1 && <div className="w-6 h-px bg-gray-300 mx-1" />}
              </div>
            ))}
          </div>

          {/* Step content — rendered from DB-driven step config */}
          {currentStep && (
            <div className="bg-white rounded-lg shadow-sm p-6">
              <h2 className="text-lg font-semibold text-gray-800 mb-4">{currentStep.label}</h2>
              <StepContent
                step={currentStep}
                state={state}
                setState={setState}
                addresses={addresses ?? []}
                checkoutConfig={checkoutConfig}
              />
            </div>
          )}

          {error && <div className="mt-3 text-red-500 text-sm">{error}</div>}

          <div className="flex justify-between mt-4">
            <button
              onClick={() => currentStepIndex > 0 ? setCurrentStepIndex((i) => i - 1) : navigate('/cart')}
              className="border border-gray-300 text-gray-700 px-4 py-2 rounded hover:bg-gray-50 text-sm"
            >
              Back
            </button>
            {currentStepIndex < steps.length - 1 ? (
              <button
                onClick={() => setCurrentStepIndex((i) => i + 1)}
                disabled={!canProceed()}
                className="bg-primary-600 text-white px-6 py-2 rounded hover:bg-primary-700 text-sm font-medium disabled:opacity-40"
              >
                Continue
              </button>
            ) : (
              <button
                onClick={handleSubmit}
                disabled={submitting || !canProceed()}
                className="bg-green-600 text-white px-6 py-2 rounded hover:bg-green-700 text-sm font-medium disabled:opacity-40"
              >
                {submitting ? 'Placing order...' : (labels['btn.placeOrder'] ?? 'Place order')}
              </button>
            )}
          </div>
        </div>

        {/* Order summary sidebar */}
        <div className="w-72 shrink-0">
          <div className="bg-white rounded-lg shadow-sm p-4">
            <h3 className="font-semibold text-gray-800 mb-3">Order summary</h3>
            <div className="space-y-2 mb-3">
              {lines.map((line) => (
                <div key={line.articleId} className="flex justify-between text-sm">
                  <span className="text-gray-600 truncate mr-2">{line.articleName} x{line.quantity}</span>
                  <span className="shrink-0">{formatPrice(line.lineTotal, currency)}</span>
                </div>
              ))}
            </div>
            <div className="border-t border-gray-200 pt-3">
              <div className="flex justify-between font-bold">
                <span>Total</span>
                <span>{formatPrice(subtotal, currency)}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

function StepContent({ step, state, setState, addresses, checkoutConfig }: {
  step: CheckoutStep
  state: CheckoutState
  setState: React.Dispatch<React.SetStateAction<CheckoutState>>
  addresses: DeliveryAddress[]
  checkoutConfig: import('../types').CheckoutConfig | undefined
}) {
  switch (step.stepType as CheckoutStepType) {
    case 'cart_review':
      return <div className="text-sm text-gray-600">Please review your cart before proceeding.</div>

    case 'delivery_address':
      return (
        <div className="space-y-3">
          {addresses.length > 0 && (
            <div className="space-y-2">
              {addresses.map((addr) => (
                <label key={addr.id} className="flex items-start gap-3 cursor-pointer p-3 border rounded hover:bg-gray-50">
                  <input
                    type="radio"
                    name="address"
                    value={addr.id}
                    checked={state.deliveryAddressId === addr.id}
                    onChange={() => setState((s) => ({ ...s, deliveryAddressId: addr.id }))}
                    className="mt-0.5"
                  />
                  <div>
                    <div className="font-medium text-sm">{addr.label}</div>
                    <div className="text-xs text-gray-500">{addr.street}, {addr.city} {addr.postalCode}</div>
                  </div>
                </label>
              ))}
            </div>
          )}
          {/* Free address entry — only if DB config allows it */}
          {checkoutConfig?.allowFreeAddressEntry && (
            <details>
              <summary className="text-sm text-primary-600 cursor-pointer hover:underline">Enter a different address</summary>
              <div className="mt-3 grid grid-cols-2 gap-3">
                <input placeholder="Street" className="col-span-2 border rounded px-3 py-2 text-sm" />
                <input placeholder="City" className="border rounded px-3 py-2 text-sm" />
                <input placeholder="Postal code" className="border rounded px-3 py-2 text-sm" />
              </div>
            </details>
          )}
        </div>
      )

    case 'po_number':
      return (
        <div>
          <label className="block text-sm text-gray-600 mb-1">
            PO Number {step.isRequired && <span className="text-red-500">*</span>}
          </label>
          <input
            value={state.poNumber ?? ''}
            onChange={(e) => setState((s) => ({ ...s, poNumber: e.target.value }))}
            placeholder="Enter your purchase order number"
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>
      )

    case 'payment_terms':
      return (
        <div>
          <label className="block text-sm text-gray-600 mb-2">Payment terms</label>
          <div className="space-y-2">
            {(checkoutConfig?.paymentTerms ?? ['Net 30']).map((term) => (
              <label key={term} className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  name="paymentTerms"
                  value={term}
                  checked={state.paymentTerms === term}
                  onChange={() => setState((s) => ({ ...s, paymentTerms: term }))}
                />
                <span className="text-sm">{term}</span>
              </label>
            ))}
          </div>
        </div>
      )

    case 'notes':
      return (
        <div>
          <label className="block text-sm text-gray-600 mb-1">Order notes</label>
          <textarea
            value={state.notes ?? ''}
            onChange={(e) => setState((s) => ({ ...s, notes: e.target.value }))}
            rows={4}
            placeholder="Any special instructions..."
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>
      )

    case 'approval':
      return (
        <div className="bg-amber-50 border border-amber-200 rounded p-4">
          <div className="text-sm font-medium text-amber-800 mb-1">Approval required</div>
          <div className="text-sm text-amber-700">
            This order will be sent for approval before processing.
            {checkoutConfig?.approvalSpendThreshold && (
              <span> Orders over {checkoutConfig.approvalSpendThreshold} require manager approval.</span>
            )}
          </div>
        </div>
      )

    case 'confirmation':
      return (
        <div className="text-sm text-gray-600">
          <p>Please review your order details above before confirming.</p>
        </div>
      )

    default:
      return <div className="text-sm text-gray-400">Step: {step.stepType}</div>
  }
}
