import { Routes, Route } from 'react-router-dom'
import { useIsAuthenticated } from '@azure/msal-react'
import { MarketingLayout } from '@/components/marketing/MarketingLayout'
import { Home } from '@/pages/marketing/Home'
import { Product } from '@/pages/marketing/Product'
import { Pricing } from '@/pages/marketing/Pricing'
import { Business } from '@/pages/marketing/Business'
import { Contact } from '@/pages/marketing/Contact'
import { Login } from '@/pages/Login'
import { AppShell } from '@/pages/AppShell'

// Anonymous visitors hitting /app are shown the sign-in card rather than being
// bounced away; authenticated ones get the real app. Public marketing routes
// never touch this guard, so they render for everyone with no redirect.
function AuthGuard() {
  const isAuthenticated = useIsAuthenticated()
  return isAuthenticated ? <AppShell /> : <Login />
}

function App() {
  return (
    <Routes>
      <Route element={<MarketingLayout />}>
        <Route index element={<Home />} />
        <Route path="product" element={<Product />} />
        <Route path="pricing" element={<Pricing />} />
        <Route path="business" element={<Business />} />
        <Route path="contact" element={<Contact />} />
      </Route>
      <Route path="/app/*" element={<AuthGuard />} />
    </Routes>
  )
}

export default App
