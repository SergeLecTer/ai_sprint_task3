import apiClient from './api'
import type { AuthToken, LoginRequest, User } from '../types'

export const authService = {
  async login(credentials: LoginRequest): Promise<AuthToken> {
    const { data } = await apiClient.post<AuthToken>('/auth/login', credentials)
    localStorage.setItem('accessToken', data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    return data
  },

  async logout(): Promise<void> {
    try {
      await apiClient.post('/auth/logout')
    } finally {
      localStorage.removeItem('accessToken')
      localStorage.removeItem('refreshToken')
    }
  },

  async getCurrentUser(): Promise<User> {
    const { data } = await apiClient.get<User>('/auth/me')
    return data
  },

  isAuthenticated(): boolean {
    return !!localStorage.getItem('accessToken')
  }
}
