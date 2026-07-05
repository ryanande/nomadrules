import { useEffect, useState } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { Login } from '@/pages/Login'
import { Profile } from '@/pages/Profile'
import { Feed } from '@/pages/Feed'

type View = 'profile' | 'feed'

function App() {
  const isAuthenticated = useIsAuthenticated()
  const { instance } = useMsal()
  const [subscriberId, setSubscriberId] = useState<string | null>(null)
  const [view, setView] = useState<View>('profile')

  useEffect(() => {
    if (!isAuthenticated) {
      setSubscriberId(null)
      return
    }
    // Entra only proves who signed in; /api/auth/me JIT-resolves (or creates)
    // the matching subscribers row and hands back its internal id.
    api.me().then((sub) => setSubscriberId(sub.id))
  }, [isAuthenticated])

  function handleLogout() {
    instance.logoutRedirect()
  }

  if (!isAuthenticated) return <Login />
  if (!subscriberId) return <p className="mt-16 text-center text-sm">Signing you in…</p>

  return (
    <div className="mx-auto max-w-xl">
      <nav className="mt-6 flex items-center justify-center gap-2">
        <Button variant={view === 'profile' ? 'default' : 'ghost'} onClick={() => setView('profile')}>
          Profile
        </Button>
        <Button variant={view === 'feed' ? 'default' : 'ghost'} onClick={() => setView('feed')}>
          Feed
        </Button>
        <Button variant="ghost" onClick={handleLogout}>
          Sign out
        </Button>
      </nav>
      {view === 'profile' ? (
        <Profile subscriberId={subscriberId} />
      ) : (
        <Feed subscriberId={subscriberId} />
      )}
    </div>
  )
}

export default App
