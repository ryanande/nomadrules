import { useEffect, useState } from 'react'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { Button } from '@/components/ui/button'
import { api, ApiError, type Subscriber } from '@/lib/api'
import { Login } from '@/pages/Login'
import { Profile } from '@/pages/Profile'
import { Feed } from '@/pages/Feed'
import { SelectState } from '@/pages/SelectState'

type View = 'profile' | 'feed'

function App() {
  const isAuthenticated = useIsAuthenticated()
  const { instance } = useMsal()
  const [subscriber, setSubscriber] = useState<Subscriber | null>(null)
  const [meError, setMeError] = useState<string | null>(null)
  const [view, setView] = useState<View>('profile')

  function loadMe() {
    setMeError(null)
    // Entra only proves who signed in; /api/auth/me JIT-resolves (or creates)
    // the matching subscribers row and hands back its internal id.
    api
      .me()
      .then(setSubscriber)
      .catch((err) =>
        setMeError(err instanceof ApiError ? err.message : 'Something went wrong signing you in.'),
      )
  }

  useEffect(() => {
    if (!isAuthenticated) {
      setSubscriber(null)
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

  if (!subscriber) return <p className="mt-16 text-center text-sm">Signing you in…</p>

  // A freshly provisioned subscriber has no domicile yet; ask once before showing Profile/Feed.
  if (subscriber.state === null) {
    return (
      <>
        <SelectState subscriber={subscriber} onSaved={setSubscriber} />
        <div className="mt-4 text-center">
          <Button variant="ghost" onClick={handleLogout}>
            Sign out
          </Button>
        </div>
      </>
    )
  }

  const subscriberId = subscriber.id

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
