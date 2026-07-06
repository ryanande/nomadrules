import { useState } from 'react'
import { NavLink, Outlet, Link } from 'react-router-dom'
import { useMsal } from '@azure/msal-react'
import { Menu, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const navItems = [
  { to: '/product', label: 'Product' },
  { to: '/pricing', label: 'Pricing' },
  { to: '/business', label: 'Business' },
  { to: '/contact', label: 'Contact' },
]

// Shared header + footer for every public marketing page. Sign-in and
// start-trial both route into the existing MSAL flow (no bespoke auth).
export function MarketingLayout() {
  const { instance } = useMsal()
  const [open, setOpen] = useState(false)
  const signIn = () => instance.loginRedirect()

  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-4">
          <Link to="/" className="flex items-center gap-2 font-heading text-lg font-semibold tracking-tight">
            <span className="inline-block size-2.5 rounded-full bg-rv-rust" aria-hidden />
            NomadRules
          </Link>

          <nav className="hidden items-center gap-1 md:flex" aria-label="Primary">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'rounded-lg px-3 py-2 text-sm font-medium transition-colors hover:text-rv-rust',
                    isActive ? 'text-rv-rust' : 'text-muted-foreground',
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="hidden items-center gap-2 md:flex">
            <Button variant="ghost" size="lg" onClick={signIn}>
              Sign in
            </Button>
            <Button size="lg" onClick={signIn}>
              Start free trial
            </Button>
          </div>

          <button
            className="inline-flex items-center justify-center rounded-lg p-2 md:hidden"
            onClick={() => setOpen((v) => !v)}
            aria-label={open ? 'Close menu' : 'Open menu'}
            aria-expanded={open}
          >
            {open ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>

        {open && (
          <div className="border-t border-border/60 md:hidden">
            <nav className="mx-auto flex w-full max-w-6xl flex-col px-4 py-2" aria-label="Mobile">
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  onClick={() => setOpen(false)}
                  className="rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground hover:text-rv-rust"
                >
                  {item.label}
                </NavLink>
              ))}
              <div className="mt-2 flex gap-2 px-3 pb-2">
                <Button variant="outline" size="lg" className="flex-1" onClick={signIn}>
                  Sign in
                </Button>
                <Button size="lg" className="flex-1" onClick={signIn}>
                  Start free trial
                </Button>
              </div>
            </nav>
          </div>
        )}
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="border-t border-border/60 bg-rv-forest text-rv-bg">
        <div className="mx-auto grid w-full max-w-6xl gap-8 px-4 py-12 sm:grid-cols-2 md:grid-cols-4">
          <div className="sm:col-span-2 md:col-span-1">
            <div className="flex items-center gap-2 font-heading text-lg font-semibold">
              <span className="inline-block size-2.5 rounded-full bg-rv-rust" aria-hidden />
              NomadRules
            </div>
            <p className="mt-3 max-w-xs text-sm text-rv-bg/70">
              Legal & regulatory intelligence for full-time RVers — anchored to your renewal calendar.
            </p>
          </div>
          <FooterCol title="Product" links={[['/product', 'How it works'], ['/pricing', 'Pricing'], ['/business', 'For business']]} />
          <FooterCol title="Company" links={[['/contact', 'Contact'], ['/business', 'Partner with us']]} />
          <div>
            <h3 className="text-sm font-semibold">Get started</h3>
            <Button className="mt-3" size="lg" onClick={signIn}>
              Start free trial
            </Button>
          </div>
        </div>
        <div className="border-t border-rv-bg/10">
          <p className="mx-auto w-full max-w-6xl px-4 py-6 text-xs text-rv-bg/60">
            © {new Date().getFullYear()} NomadRules. Informational only — not legal advice.
          </p>
        </div>
      </footer>
    </div>
  )
}

function FooterCol({ title, links }: { title: string; links: [string, string][] }) {
  return (
    <div>
      <h3 className="text-sm font-semibold">{title}</h3>
      <ul className="mt-3 space-y-2">
        {links.map(([to, label]) => (
          <li key={to + label}>
            <Link to={to} className="text-sm text-rv-bg/70 hover:text-rv-bg">
              {label}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
