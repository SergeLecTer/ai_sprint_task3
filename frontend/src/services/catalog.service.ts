import apiClient from './api'
import type { CatalogFilter, CatalogItem, PagedResult, Article, ProductCategory } from '../types'

export const catalogService = {
  async getItems(filter: CatalogFilter = {}): Promise<PagedResult<CatalogItem>> {
    const { data } = await apiClient.get<PagedResult<CatalogItem>>('/catalog', { params: filter })
    return data
  },

  async getItem(articleId: string): Promise<CatalogItem> {
    const { data } = await apiClient.get<CatalogItem>(`/catalog/${articleId}`)
    return data
  },

  async getCategories(): Promise<ProductCategory[]> {
    const { data } = await apiClient.get<ProductCategory[]>('/catalog/categories')
    return data
  },

  async searchArticles(query: string): Promise<Article[]> {
    const { data } = await apiClient.get<Article[]>('/catalog/search', { params: { q: query } })
    return data
  },

  async getRelatedItems(articleId: string): Promise<CatalogItem[]> {
    const { data } = await apiClient.get<CatalogItem[]>(`/catalog/${articleId}/related`)
    return data
  }
}
