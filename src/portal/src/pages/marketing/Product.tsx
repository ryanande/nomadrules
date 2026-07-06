import { Link } from 'react-router-dom'
import { Mail, AlertTriangle, Check, Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'

const categories = [
  { name: 'Insurance', status: 'Live', note: 'Minimum coverage, filing rules, carrier requirements.' },
  { name: 'DMV / Registration', status: 'Rolling out', note: 'Registration deadlines, inspection & title rules.' },
  { name: 'Tax', status: 'Rolling out', note: 'Residency-based filing changes for mail-forwarded domiciles.' },
  { name: 'Voting', status: 'Planned', note: 'Registration & absentee rules for out-of-state residents.' },
]

export function Product() {
  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-16 md:py-20">
      <header className="max-w-2xl">
        <h1 className="font-heading text-3xl font-semibold tracking-tight text-rv-forest md:text-4xl">
          What actually lands in your inbox
        </h1>
        <p className="mt-4 text-lg text-muted-foreground">
          Two things, and only when they matter: a weekly plain-English digest of changes in your states,
          and urgent alerts timed to your renewals. Here's what each looks like.
        </p>
      </header>

      {/* Sample digest */}
      <section className="mt-12 grid gap-8 md:grid-cols-2">
        <article className="rounded-2xl border border-border/60 bg-background p-6 shadow-sm">
          <div className="flex items-center gap-2 text-sm font-medium text-rv-blue">
            <Mail className="size-4" /> Weekly digest — sample
          </div>
          <h2 className="mt-3 font-heading text-lg font-semibold text-rv-forest">
            2 changes for Texas this week
          </h2>
          <ul className="mt-4 space-y-4 text-sm">
            <li className="border-l-2 border-rv-rust pl-3">
              <div className="font-medium text-rv-forest">Minimum liability coverage unchanged — clarification issued</div>
              <p className="mt-1 text-muted-foreground">
                TDI clarified that non-resident RV policies still satisfy the 30/60/25 minimum. No action needed.
              </p>
            </li>
            <li className="border-l-2 border-border pl-3">
              <div className="font-medium text-rv-forest">County inspection grace period extended</div>
              <p className="mt-1 text-muted-foreground">
                Registrants out of state on their due date get a 30-day inspection grace window. Relevant if you're traveling.
              </p>
            </li>
          </ul>
        </article>

        {/* Sample urgent alert */}
        <article className="rounded-2xl border border-rv-rust/40 bg-rv-rust/5 p-6 shadow-sm">
          <div className="flex items-center gap-2 text-sm font-medium text-rv-rust">
            <AlertTriangle className="size-4" /> Urgent alert — sample
          </div>
          <h2 className="mt-3 font-heading text-lg font-semibold text-rv-forest">
            Your insurance renews in 30 days — and the rules changed
          </h2>
          <p className="mt-3 text-sm text-muted-foreground">
            Texas now requires proof of continuous coverage at renewal for mail-forwarded residents. Here's the
            one-line version and exactly what to have ready before June 14.
          </p>
          <Button className="mt-4" size="lg" asChild>
            <Link to="/pricing">See what to do →</Link>
          </Button>
        </article>
      </section>

      {/* 60/30/7 timeline */}
      <section className="mt-16">
        <h2 className="font-heading text-2xl font-semibold text-rv-forest">Timed to your renewal, not a calendar</h2>
        <p className="mt-2 max-w-2xl text-muted-foreground">
          You tell us your renewal dates once. We work backward from them so alerts arrive when you can still act.
        </p>
        <div className="mt-8 grid gap-4 sm:grid-cols-3">
          {[
            { d: '60 days', t: 'Heads up', b: 'Early notice of any change affecting this renewal, with time to shop or plan.' },
            { d: '30 days', t: 'Get ready', b: 'What you need to gather or do, spelled out step by step.' },
            { d: '7 days', t: 'Last call', b: 'Final reminder so nothing lapses while you\'re off-grid.' },
          ].map((s) => (
            <div key={s.d} className="rounded-2xl border border-border/60 bg-background p-5">
              <div className="flex items-center gap-2 text-rv-rust">
                <Clock className="size-4" />
                <span className="font-heading text-lg font-semibold">{s.d}</span>
              </div>
              <div className="mt-2 text-sm font-medium text-rv-forest">{s.t}</div>
              <p className="mt-1 text-sm text-muted-foreground">{s.b}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Coverage */}
      <section className="mt-16">
        <h2 className="font-heading text-2xl font-semibold text-rv-forest">What we cover today</h2>
        <p className="mt-2 max-w-2xl text-muted-foreground">
          We'd rather tell you the truth than oversell. Insurance is live and deep; the rest is rolling out.
        </p>
        <div className="mt-6 overflow-hidden rounded-2xl border border-border/60">
          <table className="w-full text-left text-sm">
            <thead className="bg-rv-bg-card/50 text-rv-forest">
              <tr>
                <th className="px-4 py-3 font-semibold">Category</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="hidden px-4 py-3 font-semibold sm:table-cell">What's included</th>
              </tr>
            </thead>
            <tbody>
              {categories.map((c) => (
                <tr key={c.name} className="border-t border-border/60">
                  <td className="px-4 py-3 font-medium text-rv-forest">{c.name}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex items-center gap-1 text-muted-foreground">
                      {c.status === 'Live' && <Check className="size-4 text-rv-forest-light" />}
                      {c.status}
                    </span>
                  </td>
                  <td className="hidden px-4 py-3 text-muted-foreground sm:table-cell">{c.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="mt-4 text-sm text-muted-foreground">
          Coverage currently centers on Texas domicile, with South Dakota and Florida next.
        </p>
      </section>

      <div className="mt-14 flex flex-wrap items-center gap-3">
        <Button size="lg" asChild>
          <Link to="/pricing">See pricing</Link>
        </Button>
        <Button variant="outline" size="lg" asChild>
          <Link to="/contact">Ask us a question</Link>
        </Button>
      </div>
    </div>
  )
}
