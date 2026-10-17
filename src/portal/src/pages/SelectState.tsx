import { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { api, ApiError, type Subscriber } from '@/lib/api'

// Keep in sync with the API's SupportedStates allow-list.
const STATES = [
  { code: 'TX', name: 'Texas' },
  { code: 'SD', name: 'South Dakota' },
]

const inputClass =
  'border-input flex h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs disabled:opacity-50'

export function SelectState({
  subscriber,
  onSaved,
}: {
  subscriber: Subscriber
  onSaved: (updated: Subscriber) => void
}) {
  const [state, setState] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSave() {
    setSaving(true)
    setError(null)
    try {
      onSaved(await api.setState(subscriber.id, state))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not save your state. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Card className="mx-auto mt-16 w-full max-w-sm">
      <CardHeader>
        <CardTitle>Where are you domiciled?</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="domicile-state">Domicile state</Label>
          <select
            id="domicile-state"
            className={inputClass}
            value={state}
            onChange={(e) => setState(e.target.value)}
            disabled={saving}
          >
            <option value="">Select a state…</option>
            {STATES.map((s) => (
              <option key={s.code} value={s.code}>
                {s.name}
              </option>
            ))}
          </select>
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button className="w-full" onClick={handleSave} disabled={!state || saving}>
          {saving ? 'Saving…' : 'Save'}
        </Button>
      </CardContent>
    </Card>
  )
}
