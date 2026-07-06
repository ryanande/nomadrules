// Pricing source of truth for the marketing site, mirrored from
// docs/05-subscription-model.md. Keep aligned with the Stripe products
// (nomrules_basic / _pro / _business) — this is marketing copy, not billing.
// ponytail: annual = monthly * 10 ("2 months free"), derived not hardcoded.

export interface Tier {
  id: string
  name: string
  monthly: number
  tagline: string
  persona: string
  featured?: boolean
  features: string[]
}

export const tiers: Tier[] = [
  {
    id: 'basic',
    name: 'Basic',
    monthly: 9,
    tagline: 'Stay ahead of the essentials.',
    persona: 'Retired RVers & casual full-timers',
    features: [
      '1 domicile state',
      'Insurance + DMV categories',
      'Weekly plain-English digest',
      '90-day law change archive',
      '1 seat',
    ],
  },
  {
    id: 'pro',
    name: 'Pro',
    monthly: 19,
    tagline: 'Never miss a renewal again.',
    persona: 'Active full-timers & nomad entrepreneurs',
    featured: true,
    features: [
      'Up to 3 domicile states',
      'All categories (insurance, tax, DMV, voting)',
      'Weekly digest + urgent alerts',
      'Unlimited law change archive',
      '60/30/7-day renewal alerts',
      '1 seat',
    ],
  },
  {
    id: 'business',
    name: 'Business',
    monthly: 49,
    tagline: 'Built for the people RVers rely on.',
    persona: 'Attorneys, tax preparers, mail forwarders, RV parks',
    features: [
      'Unlimited domicile states',
      'All categories',
      'Everything in Pro',
      'Up to 10 team seats',
      'White-label API access',
      'Priority support',
    ],
  },
]

// 2 months free on annual.
export const annualPrice = (monthly: number) => monthly * 10
