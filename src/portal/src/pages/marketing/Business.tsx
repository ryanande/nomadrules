import { Link } from 'react-router-dom'
import { Mailbox, Scale, Calculator, Caravan, Code2, Webhook } from 'lucide-react'
import { Button } from '@/components/ui/button'

const personas = [
  {
    icon: <Mailbox className="size-6" />,
    title: 'Mail forwarding services',
    body: 'Send your clients automated, state-specific law alerts under your own brand — a retention feature that costs you nothing to run.',
  },
  {
    icon: <Scale className="size-6" />,
    title: 'RV-friendly attorneys',
    body: 'Subscribe to raw change feeds for your practice area and domicile states so you catch what affects your clients first.',
  },
  {
    icon: <Calculator className="size-6" />,
    title: 'Tax preparers',
    body: 'Track residency and filing-rule changes for mail-forwarded clients across SD, TX, and FL without manual monitoring.',
  },
  {
    icon: <Caravan className="size-6" />,
    title: 'RV parks & resorts',
    body: 'Add a "law updates" section to your resident or member portal that keeps itself current automatically.',
  },
]

export function Business() {
  return (
    <>
      <section className="border-b border-border/60 bg-rv-forest text-rv-bg">
        <div className="mx-auto grid w-full max-w-6xl items-center gap-10 px-4 py-16 md:grid-cols-2 md:py-24">
          <div>
            <span className="inline-flex rounded-full bg-rv-bg/10 px-3 py-1 text-xs font-medium">
              For business & partners
            </span>
            <h1 className="mt-5 font-heading text-3xl font-semibold tracking-tight md:text-4xl">
              Put NomadRules intelligence inside your own product
            </h1>
            <p className="mt-4 max-w-lg text-lg text-rv-bg/80">
              The professionals RVers rely on shouldn't have to babysit fifty legislatures. The Business tier
              gives you team seats, priority support, and a white-label API to embed our law-change data under
              your brand.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Button size="lg" asChild>
                <Link to="/contact?topic=partnership">Partner with us</Link>
              </Button>
              <Button size="lg" variant="outline" className="border-rv-bg/30 bg-transparent text-rv-bg hover:bg-rv-bg/10" asChild>
                <Link to="/pricing">See pricing</Link>
              </Button>
            </div>
          </div>
          <img
            src="/images/business-partner.jpg"
            alt="Professionals collaborating over a laptop"
            className="aspect-[4/3] w-full rounded-2xl object-cover shadow-xl"
          />
        </div>
      </section>

      {/* Personas */}
      <section className="mx-auto w-full max-w-6xl px-4 py-16 md:py-20">
        <h2 className="font-heading text-2xl font-semibold text-rv-forest md:text-3xl">Who it's for</h2>
        <div className="mt-10 grid gap-6 sm:grid-cols-2">
          {personas.map((p) => (
            <div key={p.title} className="rounded-2xl border border-border/60 bg-background p-6">
              <div className="flex size-11 items-center justify-center rounded-xl bg-rv-blue/10 text-rv-blue">
                {p.icon}
              </div>
              <h3 className="mt-4 font-heading text-lg font-semibold text-rv-forest">{p.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">{p.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* API */}
      <section className="border-t border-border/60 bg-rv-bg-card/40">
        <div className="mx-auto grid w-full max-w-6xl gap-10 px-4 py-16 md:grid-cols-2 md:items-center md:py-20">
          <div>
            <div className="flex items-center gap-2 text-rv-rust">
              <Code2 className="size-5" />
              <span className="text-sm font-semibold uppercase tracking-wide">White-label API</span>
            </div>
            <h2 className="mt-3 font-heading text-2xl font-semibold text-rv-forest md:text-3xl">
              Your brand, our data pipeline
            </h2>
            <p className="mt-4 text-muted-foreground">
              Pull classified, summarized law changes by state and category, or register a webhook and let us
              push them to you the moment they're detected. No scrapers to maintain, no legislatures to read.
            </p>
            <ul className="mt-6 space-y-3 text-sm text-muted-foreground">
              <li className="flex gap-2"><Webhook className="mt-0.5 size-4 text-rv-rust" /> Push delivery via registered webhooks</li>
              <li className="flex gap-2"><Code2 className="mt-0.5 size-4 text-rv-rust" /> Query by state, category, and date</li>
            </ul>
          </div>
          <pre className="overflow-x-auto rounded-2xl bg-rv-forest p-5 text-xs text-rv-bg/90 shadow-sm">
{`GET /api/v1/changes?state=SD&category=insurance
Authorization: Bearer <partner-key>

{
  "items": [{
    "id": "chg_2f9a…",
    "headline": "SD min. coverage clarified",
    "summary": "Non-resident RV policies still…",
    "severity": "info",
    "detectedAt": "2026-06-01T14:22:00Z"
  }]
}`}
          </pre>
        </div>
      </section>

      {/* CTA */}
      <section className="mx-auto w-full max-w-3xl px-4 py-16 text-center md:py-20">
        <h2 className="font-heading text-2xl font-semibold text-rv-forest md:text-3xl">
          Let's talk about your use case
        </h2>
        <p className="mt-3 text-muted-foreground">
          Tell us what you'd embed and where. We'll help you scope it.
        </p>
        <Button className="mt-8" size="lg" asChild>
          <Link to="/contact?topic=partnership">Partner with us</Link>
        </Button>
      </section>
    </>
  )
}
