import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { api, ApiError } from '@/lib/api'

// Simple RFC-ish check — the server validates too; this just catches typos
// client-side so we don't fire an obviously-bad request.
const isValidEmail = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)

export function EmailCapture({ source, className }: { source: string; className?: string }) {
  const [email, setEmail] = useState('')
  const [status, setStatus] = useState<'idle' | 'sending' | 'done' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!isValidEmail(email)) {
      setError('Please enter a valid email address.')
      return
    }
    setError(null)
    setStatus('sending')
    try {
      await api.captureLead(email, source)
      setStatus('done')
    } catch (err) {
      setStatus('error')
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  if (status === 'done') {
    return (
      <p className={className}>
        <span className="font-medium text-rv-forest">You're on the list.</span>{' '}
        We'll send your first state digest shortly.
      </p>
    )
  }

  return (
    <form onSubmit={handleSubmit} className={className} noValidate>
      <div className="flex flex-col gap-2 sm:flex-row">
        <Input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@example.com"
          aria-label="Email address"
          aria-invalid={!!error}
          className="h-11 flex-1"
        />
        <Button type="submit" size="lg" className="h-11" disabled={status === 'sending'}>
          {status === 'sending' ? 'Signing up…' : 'Get free updates'}
        </Button>
      </div>
      {error && <p className="mt-2 text-sm text-destructive">{error}</p>}
      <p className="mt-2 text-xs text-muted-foreground">
        Free state law digest. No spam, unsubscribe anytime.
      </p>
    </form>
  )
}
