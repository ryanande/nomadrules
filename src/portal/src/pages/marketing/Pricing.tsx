import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMsal } from '@azure/msal-react'
import { Check } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { tiers, annualPrice } from '@/lib/tiers'

export function Pricing() {
  const { instance } = useMsal()
  const [annual, setAnnual] = useState(false)
  const startTrial = () => instance.loginRedirect()

  return (
    <div className="mx-auto w-full max-w-6xl px-4 py-16 md:py-20">
      <header className="mx-auto max-w-2xl text-center">
        <h1 className="font-heading text-3xl font-semibold tracking-tight text-rv-forest md:text-4xl">
          Simple pricing. 14-day free trial, no card required.
        </h1>
        <p className="mt-4 text-lg text-muted-foreground">
          Start free, keep it if it saves you a single missed renewal. Cancel anytime.
        </p>

        {/* Billing toggle */}
        <div className="mt-8 inline-flex items-center gap-3 rounded-full border border-border/60 bg-background p-1">
          <button
            onClick={() => setAnnual(false)}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              !annual ? 'bg-rv-rust text-white' : 'text-muted-foreground',
            )}
            aria-pressed={!annual}
          >
            Monthly
          </button>
          <button
            onClick={() => setAnnual(true)}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              annual ? 'bg-rv-rust text-white' : 'text-muted-foreground',
            )}
            aria-pressed={annual}
          >
            Annual <span className="text-xs opacity-80">· 2 months free</span>
          </button>
        </div>
      </header>

      <div className="mt-12 grid gap-6 md:grid-cols-3">
        {tiers.map((tier) => {
          const price = annual ? annualPrice(tier.monthly) : tier.monthly
          return (
            <div
              key={tier.id}
              className={cn(
                'flex flex-col rounded-2xl border bg-background p-6',
                tier.featured ? 'border-rv-rust shadow-lg ring-1 ring-rv-rust/20' : 'border-border/60',
              )}
            >
              {tier.featured && (
                <span className="mb-3 inline-flex w-fit rounded-full bg-rv-rust px-3 py-1 text-xs font-semibold text-white">
                  Most popular
                </span>
              )}
              <h2 className="font-heading text-xl font-semibold text-rv-forest">{tier.name}</h2>
              <p className="mt-1 text-sm text-muted-foreground">{tier.tagline}</p>

              <div className="mt-5 flex items-end gap-1">
                <span className="font-heading text-4xl font-semibold text-rv-forest">${price}</span>
                <span className="pb-1 text-sm text-muted-foreground">/{annual ? 'yr' : 'mo'}</span>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                For {tier.persona.toLowerCase()}
              </p>

              <Button
                className="mt-6"
                size="lg"
                variant={tier.featured ? 'default' : 'outline'}
                onClick={startTrial}
              >
                Start free trial
              </Button>

              <ul className="mt-6 space-y-3 text-sm">
                {tier.features.map((f) => (
                  <li key={f} className="flex gap-2">
                    <Check className="mt-0.5 size-4 shrink-0 text-rv-forest-light" />
                    <span className="text-muted-foreground">{f}</span>
                  </li>
                ))}
              </ul>
            </div>
          )
        })}
      </div>

      <p className="mt-10 text-center text-sm text-muted-foreground">
        Need the API or team seats?{' '}
        <Link to="/business" className="font-medium text-rv-rust hover:underline">
          See the Business tier
        </Link>
        . Prices in USD.
      </p>
    </div>
  )
}
