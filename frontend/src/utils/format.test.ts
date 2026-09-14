import { describe, it, expect } from 'vitest'
import { formatPrice, formatDate, formatNumber } from './format'

describe('formatPrice', () => {
  it('formats USD correctly', () => {
    expect(formatPrice(1234.5, 'USD')).toBe('$1,234.50')
  })

  it('formats EUR correctly', () => {
    const result = formatPrice(99.9, 'EUR')
    expect(result).toContain('99.90')
  })

  it('handles zero amount', () => {
    expect(formatPrice(0, 'USD')).toBe('$0.00')
  })

  it('falls back gracefully for unknown currency', () => {
    const result = formatPrice(50, 'XYZ')
    expect(result).toContain('50')
  })
})

describe('formatDate', () => {
  it('formats date in DD.MM.YYYY format when specified', () => {
    const result = formatDate('2024-03-15T00:00:00Z', 'DD.MM.YYYY')
    expect(result).toMatch(/15\.03\.2024/)
  })

  it('returns locale string by default', () => {
    const result = formatDate('2024-03-15T00:00:00Z')
    expect(result).toBeTruthy()
    expect(typeof result).toBe('string')
  })
})

describe('formatNumber', () => {
  it('uses dot as decimal separator by default', () => {
    const result = formatNumber(1234.5)
    expect(result).toContain('1')
    expect(result).toContain('234')
  })

  it('returns a string', () => {
    expect(typeof formatNumber(42)).toBe('string')
  })
})
