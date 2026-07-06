import { useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Mail } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, ApiError } from '@/lib/api'

const topics = ['general', 'support', 'press', 'partnership']
const isValidEmail = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)

export function Contact() {
  const [params] = useSearchParams()
  // B2B "Partner with us" links here with ?topic=partnership pre-set.
  const initialTopic = topics.includes(params.get('topic') ?? '') ? params.get('topic')! : 'general'

  const [form, setForm] = useState({ name: '', email: '', topic: initialTopic, message: '', website: '' })
  const [status, setStatus] = useState<'idle' | 'sending' | 'done' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  const set = (k: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!form.name.trim()) return setError('Please enter your name.')
    if (!isValidEmail(form.email)) return setError('Please enter a valid email address.')
    if (!form.message.trim()) return setError('Please enter a message.')
    setError(null)
    setStatus('sending')
    try {
      await api.contact(form)
      setStatus('done')
    } catch (err) {
      setStatus('error')
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  return (
    <div className="mx-auto w-full max-w-xl px-4 py-16 md:py-20">
      <div className="flex items-center gap-2 text-rv-rust">
        <Mail className="size-5" />
        <span className="text-sm font-semibold uppercase tracking-wide">Contact</span>
      </div>
      <h1 className="mt-3 font-heading text-3xl font-semibold tracking-tight text-rv-forest md:text-4xl">
        Get in touch
      </h1>
      <p className="mt-3 text-muted-foreground">
        Questions, press, support, or a partnership idea — real humans read every message.
      </p>

      {status === 'done' ? (
        <div className="mt-8 rounded-2xl border border-rv-forest-light/30 bg-rv-forest/5 p-6">
          <p className="font-medium text-rv-forest">Thanks — we got your message.</p>
          <p className="mt-1 text-sm text-muted-foreground">We'll reply to {form.email} soon.</p>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="mt-8 space-y-4" noValidate>
          {/* Honeypot: hidden from humans; bots fill it and get silently dropped. */}
          <div aria-hidden className="absolute left-[-9999px] h-0 w-0 overflow-hidden">
            <label>
              Website
              <input
                tabIndex={-1}
                autoComplete="off"
                value={form.website}
                onChange={set('website')}
              />
            </label>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" value={form.name} onChange={set('name')} className="h-11" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" value={form.email} onChange={set('email')} className="h-11" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="topic">Topic</Label>
            <select
              id="topic"
              value={form.topic}
              onChange={set('topic')}
              className="h-11 w-full rounded-lg border border-input bg-background px-3 text-sm capitalize outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            >
              {topics.map((t) => (
                <option key={t} value={t} className="capitalize">{t}</option>
              ))}
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="message">Message</Label>
            <textarea
              id="message"
              value={form.message}
              onChange={set('message')}
              rows={5}
              className="w-full rounded-lg border border-input bg-background p-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            />
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}

          <Button type="submit" size="lg" className="h-11 w-full" disabled={status === 'sending'}>
            {status === 'sending' ? 'Sending…' : 'Send message'}
          </Button>
        </form>
      )}
    </div>
  )
}
