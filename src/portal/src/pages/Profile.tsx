import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { api, type RenewalDates, type Subscriber } from '@/lib/api'

// Renewals recur annually, so only month+day matter — the year in the picker is ignored. We show the current
// year purely to give the native date input a valid value.
const REF_YEAR = new Date().getFullYear()

type Field = { monthKey: keyof RenewalDates; dayKey: keyof RenewalDates; label: string }
const RENEWAL_FIELDS: Field[] = [
  { monthKey: 'insuranceRenewalMonth', dayKey: 'insuranceRenewalDay', label: 'Insurance renewal' },
  { monthKey: 'registrationRenewalMonth', dayKey: 'registrationRenewalDay', label: 'Registration renewal' },
  { monthKey: 'licenseRenewalMonth', dayKey: 'licenseRenewalDay', label: 'License renewal' },
  { monthKey: 'taxDueMonth', dayKey: 'taxDueDay', label: 'Tax due' },
]

const pad = (n: number) => String(n).padStart(2, '0')

// month+day -> yyyy-MM-dd for the input (day defaults to the 1st for month-only entries); '' when no month.
function toInputValue(month: number | null, day: number | null): string {
  if (!month) return ''
  return `${REF_YEAR}-${pad(month)}-${pad(day ?? 1)}`
}

// yyyy-MM-dd -> {month, day}; empty -> both null.
function fromInputValue(value: string): { month: number | null; day: number | null } {
  if (!value) return { month: null, day: null }
  const [, m, d] = value.split('-')
  return { month: Number(m), day: Number(d) }
}

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
            Only the month and day matter — the year is ignored.
          </p>
          {RENEWAL_FIELDS.map(({ monthKey, dayKey, label }) => (
            <div key={monthKey} className="space-y-2">
              <Label htmlFor={monthKey}>{label}</Label>
              <input
                id={monthKey}
                type="date"
                value={toInputValue(dates[monthKey], dates[dayKey])}
                onChange={(e) => {
                  const { month, day } = fromInputValue(e.target.value)
                  setDates({ ...dates, [monthKey]: month, [dayKey]: day })
                }}
                className="border-input flex h-9 w-full rounded-md border bg-transparent px-3 py-1 text-sm shadow-xs"
              />
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
