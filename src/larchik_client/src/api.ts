import {
  Broker,
  ClearPortfolioDataResult,
  ImportResult,
  InstrumentLookup,
  Operation,
  OperationModel,
  PagedResult,
  Portfolio,
  PortfolioPerformance,
  PortfoliosSummary,
  PortfolioSummary,
  User,
} from './types';

const API_BASE = (import.meta.env.VITE_API_BASE ?? 'https://localhost:6001').replace(/\/$/, '');

let csrfToken: string | null = null;
let csrfPromise: Promise<string> | null = null;
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

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

function resetCsrfToken(): void {
  csrfToken = null;
  csrfPromise = null;
}

function isClaimsMismatchError(body: string): boolean {
  return body.includes('different claims-based user');
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase();

  const send = async (): Promise<Response> => {
    const headers = new Headers(options.headers || undefined);
    const hasBody = options.body != null;
    const isFormData = typeof FormData !== 'undefined' && options.body instanceof FormData;
    if (hasBody && !isFormData && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    if (!SAFE_METHODS.has(method)) {
      const token = await ensureCsrfToken();
      headers.set('X-XSRF-TOKEN', token);
    }

    return fetch(`${API_BASE}${path}`, {
      ...options,
      credentials: 'include',
      headers,
    });
  };

  let res = await send();

  if (!res.ok && !SAFE_METHODS.has(method)) {
    const body = await res.text();
    if (isClaimsMismatchError(body)) {
      resetCsrfToken();
      res = await send();
    } else {
      throw new Error(body || `Request failed: ${res.status}`);
    }
  }

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed: ${res.status}`);
  }

  if (res.status === 204) return {} as T;
  return res.json();
}

export const api = {
  async login(email: string, password: string): Promise<User> {
    const user = await request<User>('/api/account/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    resetCsrfToken();
    await ensureCsrfToken();
    return user;
  },

  async me(): Promise<User> {
    return request<User>('/api/account/me', { method: 'GET' });
  },

  async logout(): Promise<void> {
    await request<void>('/api/account/logout', { method: 'POST' });
    resetCsrfToken();
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

  async listBrokers(): Promise<Broker[]> {
    return request<Broker[]>('/api/brokers');
  },

  async searchInstruments(query: string, limit = 20): Promise<InstrumentLookup[]> {
    const params = new URLSearchParams();
    if (query.trim().length > 0) params.set('query', query.trim());
    params.set('limit', String(limit));
    return request<InstrumentLookup[]>(`/api/instruments?${params.toString()}`);
  },

  async createPortfolio(payload: { name: string; reportingCurrencyId: string; brokerId: string }) {
    return request<string>('/api/portfolios', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async clearPortfolioData(id: string): Promise<ClearPortfolioDataResult> {
    return request<ClearPortfolioDataResult>(`/api/portfolios/${id}/data`, {
      method: 'DELETE',
    });
  },

  async getPortfolioSummary(id: string, method?: string): Promise<PortfolioSummary> {
    const params = method ? `?method=${encodeURIComponent(method)}` : '';
    return request<PortfolioSummary>(`/api/portfolios/${id}/summary${params}`);
  },

  async getPortfoliosSummary(method?: string, currency?: string): Promise<PortfoliosSummary> {
    const params = new URLSearchParams();
    if (method) params.append('method', method);
    if (currency) params.append('currency', currency);
    return request<PortfoliosSummary>(`/api/portfolios/summary${params.toString() ? `?${params}` : ''}`);
  },

  async getPerformance(id: string, method?: string): Promise<PortfolioPerformance[]> {
    const params = new URLSearchParams();
    if (method) params.append('method', method);
    return request<PortfolioPerformance[]>(`/api/portfolios/${id}/performance${params.toString() ? `?${params}` : ''}`);
  },

  async listOperations(
    portfolioId: string,
    options: { page?: number; pageSize?: number } = {},
  ): Promise<PagedResult<Operation>> {
    const params = new URLSearchParams();
    if (options.page != null) params.set('page', String(options.page));
    if (options.pageSize != null) params.set('pageSize', String(options.pageSize));

    const suffix = params.toString() ? `?${params.toString()}` : '';
    return request<PagedResult<Operation>>(`/api/portfolios/${portfolioId}/operations${suffix}`);
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

  async importOperations(portfolioId: string, brokerCode: string, file: File): Promise<ImportResult> {
    const payload = new FormData();
    payload.set('file', file, file.name);

    return request<ImportResult>(`/api/portfolios/${portfolioId}/imports/${encodeURIComponent(brokerCode)}`, {
      method: 'POST',
      body: payload,
    });
  },
};
