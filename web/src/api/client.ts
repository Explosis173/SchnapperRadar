import type { FeedPage, Interest, Listing, PlatformSet, SearchRequest, SearchResponse, SystemStatus } from './types'

const CSRF_HEADER = 'X-Requested-With'
const CSRF_VALUE = 'SchnapperRadar'

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
  }
}

type UnauthorizedHandler = () => void
let onUnauthorized: UnauthorizedHandler = () => {}
export const setUnauthorizedHandler = (handler: UnauthorizedHandler): void => {
  onUnauthorized = handler
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { [CSRF_HEADER]: CSRF_VALUE, Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  const res = await fetch(`/api${path}`, {
    method,
    headers,
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (res.status === 401 && !path.startsWith('/auth/')) onUnauthorized()
  if (!res.ok) {
    let message = `Fehler ${res.status}`
    if (res.status === 429) message = 'Zu viele Anfragen – kurz warten.'
    try {
      const data: unknown = await res.json()
      if (data && typeof data === 'object' && 'error' in data && typeof data.error === 'string') message = data.error
    } catch {
      // Body war kein JSON – Standardmeldung reicht.
    }
    throw new ApiError(res.status, message)
  }
  if (res.status === 204 || res.status === 202) return undefined as T
  return (await res.json()) as T
}

export const api = {
  login: (password: string) => request<void>('POST', '/auth/login', { password }),
  logout: () => request<void>('POST', '/auth/logout'),
  me: () => request<{ authenticated: boolean }>('GET', '/auth/me'),

  feed: (offset: number, limit = 20) => request<FeedPage>('GET', `/feed?offset=${offset}&limit=${limit}`),
  refreshFeed: () => request<void>('POST', '/feed/refresh'),
  saved: () => request<Listing[]>('GET', '/saved'),
  hide: (id: string) => request<void>('POST', `/listings/${encodeURIComponent(id)}/hide`),
  toggleSave: (id: string) => request<{ saved: boolean }>('POST', `/listings/${encodeURIComponent(id)}/save`),

  search: (req: SearchRequest) => request<SearchResponse>('POST', '/search', req),

  interests: () => request<Interest[]>('GET', '/interests'),
  addInterest: (query: string, minPrice: number | null, maxPrice: number | null, platforms: PlatformSet) =>
    request<Interest>('POST', '/interests', { query, minPrice, maxPrice, platforms }),
  toggleInterest: (id: string) => request<Interest>('POST', `/interests/${encodeURIComponent(id)}/toggle`),
  deleteInterest: (id: string) => request<void>('DELETE', `/interests/${encodeURIComponent(id)}`),

  status: () => request<SystemStatus>('GET', '/system/status'),
}
