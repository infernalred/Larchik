import { Operation, OperationModel, Portfolio, PortfolioPerformance, PortfolioSummary, User } from './types';

const API_BASE = (import.meta.env.VITE_API_BASE ?? 'https://localhost:6001').replace(/\/$/, '');

let csrfToken: string | null = null;
let csrfPromise: Promise<string> | null = null;

async function ensureCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken;
  if (!csrfPromise) {
    csrfPromise = fetch(`${API_BASE}/api/account/antiforgery`, {
      method: 'GET',
      credentials: 'include',
    })
      .then(async (res) => {
        if (!res.ok) throw new Error('Не удалось получить CSRF токен');
        const data = await res.json();
        csrfToken = data.token;
        return csrfToken!;
      })
      .finally(() => {
        csrfPromise = null;
      });
  }
  return csrfPromise;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers || undefined);
  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const method = (options.method ?? 'GET').toUpperCase();
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    const token = await ensureCsrfToken();
    headers.set('X-XSRF-TOKEN', token);
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    credentials: 'include',
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

  async me(): Promise<User> {
    return request<User>('/api/account/me', { method: 'GET' });
  },

  async logout(): Promise<void> {
    return request<void>('/api/account/logout', { method: 'POST' });
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return request<void>('/api/account/change-password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
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
