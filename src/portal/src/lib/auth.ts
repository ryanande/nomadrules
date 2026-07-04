const TOKEN_KEY = 'nr_token'
// .NET writes ClaimTypes.NameIdentifier claims using the long XML-Soap URI, not "sub".
const NAME_ID_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'

export function setToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
}

export function getSubscriberId(): string | null {
  const token = localStorage.getItem(TOKEN_KEY)
  if (!token) return null
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return payload[NAME_ID_CLAIM] ?? payload.sub ?? null
  } catch {
    return null
  }
}
