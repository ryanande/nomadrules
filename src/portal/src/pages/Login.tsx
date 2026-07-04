import { useState } from 'react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { api, ApiError } from '@/lib/api'

const US_STATES = ['TX', 'FL', 'SD'] // ponytail: MVP is insurance-only, TX/FL/SD sources — expand when more states onboard

export function Login() {
  return (
    <div className="mx-auto mt-16 w-full max-w-sm">
      <Tabs defaultValue="signin">
        <TabsList className="w-full">
          <TabsTrigger value="signin" className="flex-1">
            Sign in
          </TabsTrigger>
          <TabsTrigger value="register" className="flex-1">
            Register
          </TabsTrigger>
        </TabsList>
        <TabsContent value="signin">
          <SignInForm />
        </TabsContent>
        <TabsContent value="register">
          <RegisterForm />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function SignInForm() {
  const [email, setEmail] = useState('')
  const [status, setStatus] = useState<'idle' | 'sent' | 'error'>('idle')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    try {
      await api.requestMagicLink(email)
      setStatus('sent')
    } catch {
      setStatus('error')
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Get a sign-in link</CardTitle>
      </CardHeader>
      <CardContent>
        {status === 'sent' ? (
          <p className="text-sm text-muted-foreground">
            Check your email for a sign-in link.
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="signin-email">Email</Label>
              <Input
                id="signin-email"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <Button type="submit" className="w-full">
              Send link
            </Button>
            {status === 'error' && (
              <p className="text-sm text-destructive">Something went wrong. Try again.</p>
            )}
          </form>
        )}
      </CardContent>
    </Card>
  )
}

function RegisterForm() {
  const [email, setEmail] = useState('')
  const [state, setState] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api.register({
        email,
        state,
        insuranceRenewalMonth: null,
        registrationRenewalMonth: null,
        licenseRenewalMonth: null,
        taxDueMonth: null,
      })
      setDone(true)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.')
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Create your account</CardTitle>
      </CardHeader>
      <CardContent>
        {done ? (
          <p className="text-sm text-muted-foreground">
            You're registered. Use "Sign in" to get a link and set your renewal dates.
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="register-email">Email</Label>
              <Input
                id="register-email"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="register-state">Domicile state</Label>
              <select
                id="register-state"
                required
                value={state}
                onChange={(e) => setState(e.target.value)}
                className="border-input flex h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs"
              >
                <option value="" disabled>
                  Select a state
                </option>
                {US_STATES.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            </div>
            <Button type="submit" className="w-full">
              Register
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>
        )}
      </CardContent>
    </Card>
  )
}
