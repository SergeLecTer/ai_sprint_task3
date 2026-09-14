import apiClient from './api'
import type { Order, OrderLine, PagedResult } from '../types'

export interface CreateOrderRequest {
  lines: { articleId: string; variantId?: string; quantity: number; notes?: string }[]
  deliveryAddressId?: string
  poNumber?: string
  requestedDeliveryDate?: string
  notes?: string
}

export interface UpdateOrderRequest {
  lines?: Partial<OrderLine>[]
  deliveryAddressId?: string
  poNumber?: string
  requestedDeliveryDate?: string
  notes?: string
}

export const orderService = {
  async createOrder(req: CreateOrderRequest): Promise<Order> {
    const { data } = await apiClient.post<Order>('/orders', req)
    return data
  },

  async getOrder(orderId: string): Promise<Order> {
    const { data } = await apiClient.get<Order>(`/orders/${orderId}`)
    return data
  },

  async getOrders(params?: { status?: string; page?: number; pageSize?: number }): Promise<PagedResult<Order>> {
    const { data } = await apiClient.get<PagedResult<Order>>('/orders', { params })
    return data
  },

  async updateOrder(orderId: string, req: UpdateOrderRequest): Promise<Order> {
    const { data } = await apiClient.put<Order>(`/orders/${orderId}`, req)
    return data
  },

  async submitOrder(orderId: string): Promise<Order> {
    const { data } = await apiClient.post<Order>(`/orders/${orderId}/submit`)
    return data
  },

  async approveOrder(orderId: string, comment?: string): Promise<Order> {
    const { data } = await apiClient.post<Order>(`/orders/${orderId}/approve`, { comment })
    return data
  },

  async rejectOrder(orderId: string, reason: string): Promise<Order> {
    const { data } = await apiClient.post<Order>(`/orders/${orderId}/reject`, { reason })
    return data
  },

  async cancelOrder(orderId: string, reason: string): Promise<Order> {
    const { data } = await apiClient.post<Order>(`/orders/${orderId}/cancel`, { reason })
    return data
  },

  async reorderFromHistory(orderId: string): Promise<Order> {
    const { data } = await apiClient.post<Order>(`/orders/${orderId}/reorder`)
    return data
  },

  async bulkUpload(file: File): Promise<{ created: Order; errors: string[] }> {
    const formData = new FormData()
    formData.append('file', file)
    const { data } = await apiClient.post('/orders/bulk-upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    return data
  }
}
