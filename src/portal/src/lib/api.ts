import { getAccessToken, msalInstance } from '@/lib/msal'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5017'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: HeadersInit = { 'Content-Type': 'application/json', ...init?.headers }

  // Anonymous endpoints (e.g. /api/subscribers register) are called before any
  // sign-in exists — only attach a token when there's a signed-in account.
  const hasAccount = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0]
  if (hasAccount) {
    const token = await getAccessToken()
    ;(headers as Record<string, string>)['Authorization'] = `Bearer ${token}`
  }

  const res = await fetch(`${API_URL}${path}`, { ...init, headers })

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(res.status, body.message ?? res.statusText)
  }
  if (res.status === 204) return undefined as T
  return res.json()
}

export interface Subscriber {
  id: string
  email: string
  state: string
  insuranceRenewalMonth: number | null
  registrationRenewalMonth: number | null
  licenseRenewalMonth: number | null
  taxDueMonth: number | null
  tier: string
}

export interface RenewalMonths {
  insuranceRenewalMonth: number | null
  registrationRenewalMonth: number | null
  licenseRenewalMonth: number | null
  taxDueMonth: number | null
}

export interface LawChangeFeedItem {
  id: string
  headline: string
  summary: string
  severity: string
  detectedAt: string
}

export const api = {
  register: (body: { email: string; state: string } & RenewalMonths) =>
    request<Subscriber>('/api/subscribers', { method: 'POST', body: JSON.stringify(body) }),

  me: () => request<Subscriber>('/api/auth/me'),

  getProfile: (id: string) => request<Subscriber>(`/api/subscribers/${id}/profile`),

  updateProfile: (id: string, body: RenewalMonths) =>
    request<Subscriber>(`/api/subscribers/${id}/profile`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  getFeed: (id: string, limit = 20, offset = 0) =>
    request<{ items: LawChangeFeedItem[]; total_count: number }>(
      `/api/subscribers/${id}/feed?limit=${limit}&offset=${offset}`,
    ),
}
