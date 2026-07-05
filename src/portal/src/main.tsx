import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { MsalProvider } from '@azure/msal-react'
import { msalInstance } from '@/lib/msal'
import './index.css'
import App from './App.tsx'

const root = createRoot(document.getElementById('root')!)

try {
  await msalInstance.initialize()
  await msalInstance.handleRedirectPromise()

  root.render(
    <StrictMode>
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    </StrictMode>,
  )
} catch (err) {
  console.error('Failed to initialize sign-in', err)
  root.render(
    <StrictMode>
      <p style={{ margin: '4rem auto', textAlign: 'center', fontSize: '0.875rem' }}>
        Something went wrong loading the sign-in page. Please refresh and try again.
      </p>
    </StrictMode>,
  )
}
