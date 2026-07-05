import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { api, type RenewalDates, type Subscriber } from '@/lib/api'

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

// Our data is month + OPTIONAL day, not a full date — so we use a month select plus an optional day number
// rather than <input type="date">. That keeps "month only" a real, editable state (a native date picker
// can't represent a partial date, and would force a day the moment the field is touched).
type Field = { monthKey: keyof RenewalDates; dayKey: keyof RenewalDates; label: string }
const RENEWAL_FIELDS: Field[] = [
  { monthKey: 'insuranceRenewalMonth', dayKey: 'insuranceRenewalDay', label: 'Insurance renewal' },
  { monthKey: 'registrationRenewalMonth', dayKey: 'registrationRenewalDay', label: 'Registration renewal' },
  { monthKey: 'licenseRenewalMonth', dayKey: 'licenseRenewalDay', label: 'License renewal' },
  { monthKey: 'taxDueMonth', dayKey: 'taxDueDay', label: 'Tax due' },
]

const inputClass =
  'border-input flex h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs disabled:opacity-50'

export function Profile({ subscriberId }: { subscriberId: string }) {
  const [subscriber, setSubscriber] = useState<Subscriber | null>(null)
  const [dates, setDates] = useState<RenewalDates | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    api.getProfile(subscriberId).then((sub) => {
      setSubscriber(sub)
      setDates({
        insuranceRenewalMonth: sub.insuranceRenewalMonth,
        insuranceRenewalDay: sub.insuranceRenewalDay,
        registrationRenewalMonth: sub.registrationRenewalMonth,
        registrationRenewalDay: sub.registrationRenewalDay,
        licenseRenewalMonth: sub.licenseRenewalMonth,
        licenseRenewalDay: sub.licenseRenewalDay,
        taxDueMonth: sub.taxDueMonth,
        taxDueDay: sub.taxDueDay,
      })
    })
  }, [subscriberId])

  if (!subscriber || !dates) return <p className="mt-16 text-center text-sm">Loading…</p>

  async function handleSave() {
    if (!dates) return
    setSaved(false)
    const updated = await api.updateProfile(subscriberId, dates)
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
          <p className="text-xs text-muted-foreground">
            Pick the renewal month. Add the day if you know it — you'll get alerts closer to the exact date.
          </p>
          {RENEWAL_FIELDS.map(({ monthKey, dayKey, label }) => {
            const month = dates[monthKey]
            return (
              <div key={monthKey} className="space-y-2">
                <Label htmlFor={monthKey}>{label}</Label>
                <div className="flex gap-2">
                  <select
                    id={monthKey}
                    value={month ?? ''}
                    onChange={(e) => {
                      const m = e.target.value ? Number(e.target.value) : null
                      // Clearing the month also clears the day — a day without a month is meaningless.
                      setDates({ ...dates, [monthKey]: m, [dayKey]: m ? dates[dayKey] : null })
                    }}
                    className={inputClass}
                  >
                    <option value="">Not set</option>
                    {MONTHS.map((m, i) => (
                      <option key={m} value={i + 1}>{m}</option>
                    ))}
                  </select>
                  <input
                    type="number"
                    aria-label={`${label} day`}
                    min={1}
                    max={31}
                    placeholder="Day"
                    disabled={!month}
                    value={dates[dayKey] ?? ''}
                    onChange={(e) =>
                      setDates({ ...dates, [dayKey]: e.target.value ? Number(e.target.value) : null })
                    }
                    className={`${inputClass} w-24`}
                  />
                </div>
              </div>
            )
          })}
          <Button onClick={handleSave} className="w-full">
            Save
          </Button>
          {saved && <p className="text-sm text-muted-foreground">Saved.</p>}
        </CardContent>
      </Card>
    </div>
  )
}
