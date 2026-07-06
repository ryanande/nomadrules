import { Link } from 'react-router-dom'
import { useMsal } from '@azure/msal-react'
import { BellRing, ScanSearch, Sparkles, ShieldCheck, MapPin, CalendarClock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { EmailCapture } from '@/components/marketing/EmailCapture'

export function Home() {
  const { instance } = useMsal()
  const signIn = () => instance.loginRedirect()

  return (
    <>
      {/* Hero */}
      <section className="relative overflow-hidden">
        <div className="mx-auto grid w-full max-w-6xl items-center gap-10 px-4 py-16 md:grid-cols-2 md:py-24">
          <div>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-rv-bg-card px-3 py-1 text-xs font-medium text-rv-forest-light">
              <MapPin className="size-3.5" /> Built for full-time RVers & digital nomads
            </span>
            <h1 className="mt-5 font-heading text-4xl font-semibold tracking-tight text-rv-forest md:text-5xl">
              The law changed while you were driving. We'll tell you before it costs you.
            </h1>
            <p className="mt-5 max-w-lg text-lg text-muted-foreground">
              NomadRules watches insurance, DMV, tax, and voting rules in your domicile state and sends
              plain-English alerts timed to your actual renewals — 60, 30, and 7 days out.
            </p>
            <div className="mt-8 max-w-md">
              <EmailCapture source="home-hero" />
            </div>
            <div className="mt-6 flex items-center gap-3">
              <Button variant="ghost" size="lg" onClick={signIn}>
                Sign in
              </Button>
              <Link to="/pricing" className="text-sm font-medium text-rv-rust hover:underline">
                See pricing →
              </Link>
            </div>
          </div>
          <div className="relative">
            <img
              src="/images/hero-van.jpg"
              alt="A camper van driving a desert highway through red-rock country"
              className="aspect-[4/3] w-full rounded-2xl object-cover shadow-xl"
            />
          </div>
        </div>
      </section>

      {/* Value narrative */}
      <section className="border-y border-border/60 bg-rv-bg-card/40">
        <div className="mx-auto w-full max-w-3xl px-4 py-16 text-center">
          <h2 className="font-heading text-2xl font-semibold text-rv-forest md:text-3xl">
            When home is a moving target, the rules keep moving too.
          </h2>
          <p className="mt-5 text-lg text-muted-foreground">
            You picked a domicile state for good reasons. But its legislature doesn't email you when it
            changes minimum coverage, shifts a registration deadline, or tweaks how mail-forwarded residents
            file taxes. Miss one, and a lapse can mean a denied claim or a fine three states away from anyone
            who can help. NomadRules is the quiet co-pilot that reads the fine print so you don't have to.
          </p>
        </div>
      </section>

      {/* How it works */}
      <section className="mx-auto w-full max-w-6xl px-4 py-16 md:py-20">
        <h2 className="text-center font-heading text-2xl font-semibold text-rv-forest md:text-3xl">
          How it works
        </h2>
        <div className="mt-12 grid gap-8 md:grid-cols-3">
          <Step
            icon={<ScanSearch className="size-6" />}
            step="1"
            title="We detect the change"
            body="Our crawlers monitor state and federal sources continuously and flag the moment something relevant to RVers moves."
          />
          <Step
            icon={<Sparkles className="size-6" />}
            step="2"
            title="We translate it"
            body="Each change is summarized into plain English — what changed, who it affects, and whether you need to act — with a quality score behind every summary."
          />
          <Step
            icon={<BellRing className="size-6" />}
            step="3"
            title="We time it to your renewal"
            body="Alerts land 60, 30, and 7 days before your insurance, registration, license, and tax dates — the moments the change actually matters to you."
          />
        </div>
      </section>

      {/* Trust */}
      <section className="border-t border-border/60 bg-rv-forest text-rv-bg">
        <div className="mx-auto grid w-full max-w-6xl gap-10 px-4 py-16 md:grid-cols-2 md:items-center">
          <img
            src="/images/nomads-working.jpg"
            alt="Full-time RVers working remotely together around a table"
            loading="lazy"
            className="aspect-[16/10] w-full rounded-2xl object-cover"
          />
          <div>
            <h2 className="font-heading text-2xl font-semibold md:text-3xl">
              Honest about what we cover
            </h2>
            <ul className="mt-6 space-y-4">
              <TrustItem icon={<ShieldCheck className="size-5" />}>
                <strong>Insurance first, Texas first.</strong> We launched narrow on purpose — deep and
                accurate beats broad and wrong. More categories and states are rolling out.
              </TrustItem>
              <TrustItem icon={<CalendarClock className="size-5" />}>
                <strong>Anchored to your calendar.</strong> No firehose of irrelevant updates — only what
                touches your states and your renewal dates.
              </TrustItem>
              <TrustItem icon={<Sparkles className="size-5" />}>
                <strong>Written for humans.</strong> Every alert is plain English you can act on, not a
                statute you have to decode.
              </TrustItem>
            </ul>
            <p className="mt-6 text-sm text-rv-bg/60">
              Informational only — NomadRules is not legal advice.
            </p>
          </div>
        </div>
      </section>

      {/* Closing CTA */}
      <section className="mx-auto w-full max-w-3xl px-4 py-16 text-center md:py-20">
        <h2 className="font-heading text-2xl font-semibold text-rv-forest md:text-3xl">
          Start with a free state digest.
        </h2>
        <p className="mt-3 text-muted-foreground">
          Drop your email and we'll send the latest changes for your state. Upgrade anytime for
          renewal-timed alerts.
        </p>
        <div className="mx-auto mt-8 max-w-md text-left">
          <EmailCapture source="home-footer" />
        </div>
      </section>
    </>
  )
}

function Step({ icon, step, title, body }: { icon: React.ReactNode; step: string; title: string; body: string }) {
  return (
    <div className="rounded-2xl border border-border/60 bg-background p-6">
      <div className="flex size-11 items-center justify-center rounded-xl bg-rv-rust/10 text-rv-rust">
        {icon}
      </div>
      <div className="mt-4 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Step {step}
      </div>
      <h3 className="mt-1 font-heading text-lg font-semibold text-rv-forest">{title}</h3>
      <p className="mt-2 text-sm text-muted-foreground">{body}</p>
    </div>
  )
}

function TrustItem({ icon, children }: { icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <li className="flex gap-3">
      <span className="mt-0.5 text-rv-rust">{icon}</span>
      <span className="text-rv-bg/85">{children}</span>
    </li>
  )
}
