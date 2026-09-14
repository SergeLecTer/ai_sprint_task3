import apiClient from './api'
import type { Company, CompanyUiConfig, DeliveryAddress, CheckoutConfig } from '../types'

export const companyService = {
  async getMyCompany(): Promise<Company> {
    const { data } = await apiClient.get<Company>('/companies/me')
    return data
  },

  async getUiConfig(): Promise<CompanyUiConfig> {
    const { data } = await apiClient.get<CompanyUiConfig>('/companies/me/ui-config')
    return data
  },

  async getDeliveryAddresses(): Promise<DeliveryAddress[]> {
    const { data } = await apiClient.get<DeliveryAddress[]>('/companies/me/addresses')
    return data
  },

  async getCheckoutConfig(): Promise<CheckoutConfig> {
    const { data } = await apiClient.get<CheckoutConfig>('/companies/me/checkout-config')
    return data
  },

  // Admin only
  async getAllCompanies(): Promise<Company[]> {
    const { data } = await apiClient.get<Company[]>('/admin/companies')
    return data
  },

  async updateUiConfig(companyId: string, config: Partial<CompanyUiConfig>): Promise<CompanyUiConfig> {
    const { data } = await apiClient.put<CompanyUiConfig>(`/admin/companies/${companyId}/ui-config`, config)
    return data
  },

  async updateCheckoutConfig(companyId: string, config: Partial<CheckoutConfig>): Promise<CheckoutConfig> {
    const { data } = await apiClient.put<CheckoutConfig>(`/admin/companies/${companyId}/checkout-config`, config)
    return data
  }
}
