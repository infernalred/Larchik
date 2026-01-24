import { Operation, OperationModel, Portfolio, PortfolioPerformance, PortfolioSummary, User } from './types';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

function getAuthToken() {
  return localStorage.getItem('token');
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(options.headers || {}),
  };

  const token = getAuthToken();
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed: ${res.status}`);
  }

  if (res.status === 204) return {} as T;
  return res.json();
}

export const api = {
  async login(email: string, password: string): Promise<User> {
    return request<User>('/api/account/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
  },

  async listPortfolios(): Promise<Portfolio[]> {
    return request<Portfolio[]>('/api/portfolios');
  },

  async createPortfolio(payload: { name: string; reportingCurrencyId: string; brokerId?: string }) {
    return request<string>('/api/portfolios', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getPortfolioSummary(id: string, method?: string): Promise<PortfolioSummary> {
    const params = method ? `?method=${encodeURIComponent(method)}` : '';
    return request<PortfolioSummary>(`/api/portfolios/${id}/summary${params}`);
  },

  async getPerformance(id: string, method?: string): Promise<PortfolioPerformance[]> {
    const params = new URLSearchParams();
    if (method) params.append('method', method);
    return request<PortfolioPerformance[]>(`/api/portfolios/${id}/performance${params.toString() ? `?${params}` : ''}`);
  },

  async listOperations(portfolioId: string) {
    return request<Operation[]>(`/api/portfolios/${portfolioId}/operations`);
  },

  async createOperation(portfolioId: string, model: OperationModel) {
    return request<string>(`/api/portfolios/${portfolioId}/operations`, {
      method: 'POST',
      body: JSON.stringify(model),
    });
  },

  async updateOperation(portfolioId: string, id: string, model: OperationModel) {
    return request<void>(`/api/portfolios/${portfolioId}/operations/${id}`, {
      method: 'PUT',
      body: JSON.stringify(model),
    });
  },

  async deleteOperation(portfolioId: string, id: string) {
    return request<void>(`/api/portfolios/${portfolioId}/operations/${id}`, {
      method: 'DELETE',
    });
  },
};
