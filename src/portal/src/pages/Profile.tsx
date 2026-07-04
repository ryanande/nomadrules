import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { api, type RenewalMonths, type Subscriber } from '@/lib/api'

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

const RENEWAL_FIELDS: { key: keyof RenewalMonths; label: string }[] = [
  { key: 'insuranceRenewalMonth', label: 'Insurance renewal' },
  { key: 'registrationRenewalMonth', label: 'Registration renewal' },
  { key: 'licenseRenewalMonth', label: 'License renewal' },
  { key: 'taxDueMonth', label: 'Tax due' },
]

export function Profile({ subscriberId }: { subscriberId: string }) {
  const [subscriber, setSubscriber] = useState<Subscriber | null>(null)
  const [months, setMonths] = useState<RenewalMonths | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    api.getProfile(subscriberId).then((sub) => {
      setSubscriber(sub)
      setMonths({
        insuranceRenewalMonth: sub.insuranceRenewalMonth,
        registrationRenewalMonth: sub.registrationRenewalMonth,
        licenseRenewalMonth: sub.licenseRenewalMonth,
        taxDueMonth: sub.taxDueMonth,
      })
    })
  }, [subscriberId])

  if (!subscriber || !months) return <p className="mt-16 text-center text-sm">Loading…</p>

  async function handleSave() {
    if (!months) return
    setSaved(false)
    const updated = await api.updateProfile(subscriberId, months)
    setSubscriber(updated)
    setSaved(true)
  }

  return (
    <div className="mx-auto mt-16 w-full max-w-sm space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{subscriber.email}</CardTitle>
          <p className="text-sm text-muted-foreground">Domicile: {subscriber.state}</p>
        </CardHeader>
        <CardContent className="space-y-4">
          {RENEWAL_FIELDS.map(({ key, label }) => (
            <div key={key} className="space-y-2">
              <Label htmlFor={key}>{label}</Label>
              <select
                id={key}
                value={months[key] ?? ''}
                onChange={(e) =>
                  setMonths({
                    ...months,
                    [key]: e.target.value ? Number(e.target.value) : null,
                  })
                }
                className="border-input flex h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs"
              >
                <option value="">Not set</option>
                {MONTHS.map((m, i) => (
                  <option key={m} value={i + 1}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          ))}
          <Button onClick={handleSave} className="w-full">
            Save
          </Button>
          {saved && <p className="text-sm text-muted-foreground">Saved.</p>}
        </CardContent>
      </Card>
    </div>
  )
}
