import { useEffect, useState } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { Button } from '@/components/ui/button'
import { api, ApiError } from '@/lib/api'
import { Login } from '@/pages/Login'
import { Profile } from '@/pages/Profile'
import { Feed } from '@/pages/Feed'

type View = 'profile' | 'feed'

function App() {
  const isAuthenticated = useIsAuthenticated()
  const { instance } = useMsal()
  const [subscriberId, setSubscriberId] = useState<string | null>(null)
  const [meError, setMeError] = useState<string | null>(null)
  const [view, setView] = useState<View>('profile')

  function loadMe() {
    setMeError(null)
    // Entra only proves who signed in; /api/auth/me JIT-resolves (or creates)
    // the matching subscribers row and hands back its internal id.
    api
      .me()
      .then((sub) => setSubscriberId(sub.id))
      .catch((err) =>
        setMeError(err instanceof ApiError ? err.message : 'Something went wrong signing you in.'),
      )
  }

  useEffect(() => {
    if (!isAuthenticated) {
      setSubscriberId(null)
      setMeError(null)
      return
    }
    loadMe()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated])

  function handleLogout() {
    instance.logoutRedirect()
  }

  if (!isAuthenticated) return <Login />

  if (meError) {
    return (
      <div className="mx-auto mt-16 w-full max-w-sm space-y-4 text-center">
        <p className="text-sm text-destructive">{meError}</p>
        <div className="flex justify-center gap-2">
          <Button onClick={loadMe}>Try again</Button>
          <Button variant="ghost" onClick={handleLogout}>
            Sign out
          </Button>
        </div>
      </div>
    )
  }

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
