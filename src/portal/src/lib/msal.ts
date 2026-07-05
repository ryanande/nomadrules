import { PublicClientApplication, InteractionRequiredAuthError } from '@azure/msal-browser'

// Public SPA client — authorization code + PKCE, no client secret (see design.md).
export const msalInstance = new PublicClientApplication({
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID,
    authority: import.meta.env.VITE_ENTRA_AUTHORITY,
    redirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
})

const loginRequest = { scopes: [import.meta.env.VITE_ENTRA_API_SCOPE] }

export async function getAccessToken(): Promise<string> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0]
  if (!account) throw new Error('No signed-in account')

  try {
    const result = await msalInstance.acquireTokenSilent({ ...loginRequest, account })
    return result.accessToken
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      const result = await msalInstance.acquireTokenRedirect(loginRequest)
      // acquireTokenRedirect navigates away; this line is unreachable but keeps types happy.
      return result as unknown as string
    }
    throw err
  }
}
