import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import { clearToken, getSubscriberId, setToken } from '@/lib/auth'
import { Login } from '@/pages/Login'
import { Profile } from '@/pages/Profile'
import { Feed } from '@/pages/Feed'

type View = 'profile' | 'feed'

function App() {
  const [subscriberId, setSubscriberId] = useState<string | null>(getSubscriberId())
  const [verifying, setVerifying] = useState(false)
  const [view, setView] = useState<View>('profile')

  useEffect(() => {
    const magicToken = new URLSearchParams(window.location.search).get('token')
    if (!magicToken) return

    setVerifying(true)
    api
      .verifyMagicLink(magicToken)
      .then(({ token }) => {
        setToken(token)
        setSubscriberId(getSubscriberId())
        window.history.replaceState({}, '', window.location.pathname)
      })
      .finally(() => setVerifying(false))
  }, [])

  function handleLogout() {
    clearToken()
    setSubscriberId(null)
  }

  if (verifying) return <p className="mt-16 text-center text-sm">Signing you in…</p>

  if (!subscriberId) return <Login />

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
