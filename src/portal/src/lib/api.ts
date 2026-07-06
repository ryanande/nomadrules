import { getAccessToken } from '@/lib/msal'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5017'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(
  path: string,
  init?: RequestInit & { anonymous?: boolean },
): Promise<T> {
  const { anonymous, ...requestInit } = init ?? {}
  const headers: HeadersInit = { 'Content-Type': 'application/json', ...requestInit.headers }

  // Anonymous endpoints (e.g. /api/subscribers register) must never carry a
  // token, even if a stale/previous account is cached on a shared device —
  // callers opt in explicitly rather than this being inferred from cache state.
  if (!anonymous) {
    const token = await getAccessToken()
    ;(headers as Record<string, string>)['Authorization'] = `Bearer ${token}`
  }

  const res = await fetch(`${API_URL}${path}`, { ...requestInit, headers })

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
  insuranceRenewalDay: number | null
  registrationRenewalDay: number | null
  licenseRenewalDay: number | null
  taxDueDay: number | null
  tier: string
}

// Renewals recur annually — we store month+day, the year is ignored. Day is null for month-only entries.
export interface RenewalDates {
  insuranceRenewalMonth: number | null
  insuranceRenewalDay: number | null
  registrationRenewalMonth: number | null
  registrationRenewalDay: number | null
  licenseRenewalMonth: number | null
  licenseRenewalDay: number | null
  taxDueMonth: number | null
  taxDueDay: number | null
}

export interface LawChangeFeedItem {
  id: string
  headline: string
  summary: string
  severity: string
  detectedAt: string
}

export const api = {
  register: (body: { email: string; state: string } & RenewalDates) =>
    request<Subscriber>('/api/subscribers', {
      method: 'POST',
      body: JSON.stringify(body),
      anonymous: true,
    }),

  me: () => request<Subscriber>('/api/auth/me'),

  getProfile: (id: string) => request<Subscriber>(`/api/subscribers/${id}/profile`),

  updateProfile: (id: string, body: RenewalDates) =>
    request<Subscriber>(`/api/subscribers/${id}/profile`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),

  getFeed: (id: string, limit = 20, offset = 0) =>
    request<{ items: LawChangeFeedItem[]; total_count: number }>(
      `/api/subscribers/${id}/feed?limit=${limit}&offset=${offset}`,
    ),

  // Public marketing funnel — anonymous, no token attached.
  captureLead: (email: string, source?: string) =>
    request<void>('/api/leads', {
      method: 'POST',
      body: JSON.stringify({ email, source }),
      anonymous: true,
    }),

  // `website` is the honeypot: left empty by the form, must stay empty for humans.
  contact: (body: {
    name: string
    email: string
    topic?: string
    message: string
    website?: string
  }) =>
    request<void>('/api/contact', {
      method: 'POST',
      body: JSON.stringify(body),
      anonymous: true,
    }),
}
