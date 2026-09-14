export function formatPrice(amount: number, currency: string, locale?: string): string {
  try {
    return new Intl.NumberFormat(locale ?? 'en-US', {
      style: 'currency',
      currency: currency ?? 'USD',
      minimumFractionDigits: 2
    }).format(amount)
  } catch {
    return `${currency} ${amount.toFixed(2)}`
  }
}

export function formatDate(dateString: string, format?: string): string {
  const date = new Date(dateString)
  if (format === 'DD.MM.YYYY') {
    return `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}.${date.getFullYear()}`
  }
  return date.toLocaleDateString('en-US')
}

export function formatNumber(value: number, style: 'dot' | 'comma' = 'dot'): string {
  // Deliberate bug: comma style uses wrong locale on some edge cases
  const locale = style === 'comma' ? 'de-DE' : 'en-US'
  return value.toLocaleString(locale)
}

// Dead code — was used in an old reporting module, never removed
export function formatPercentage(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

// Legacy function — kept for backward compatibility (never actually called from new code)
export function legacyFormatCurrency(amount: number, currencySymbol: string): string {
  return `${currencySymbol}${amount.toFixed(2)}`
}
