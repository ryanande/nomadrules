## Why

The portal today is a single auth-gated SPA: an unauthenticated visitor sees only a bare `Login` screen (`src/portal/src/App.tsx`). There is no public front door — nothing that explains what NomadRules is, sells the value proposition, shows pricing, or gives a B2B/partner audience a reason to reach out. The subscription model, tiers, personas, and B2B story already exist in `docs/05-subscription-model.md`; they just have no home on the web. Acquisition (the Week-1 "renewal calendar public and collecting emails, 50+" success criterion in `CLAUDE.md`) is blocked without a marketing surface.

## What Changes

- Add a **public marketing site** to the portal, served on unauthenticated routes, that a visitor lands on before any sign-in.
- Introduce client-side routing (public marketing routes + the existing authenticated app), so `/`, `/pricing`, `/business`, etc. render marketing content while `/app/*` stays gated behind MSAL.
- **Home / landing page**: hero with the core promise (renewal-anchored legal & regulatory alerts for full-time RVers), story-driven value narrative, "how it works," trust/confidence signals, and an email-capture CTA feeding the acquisition funnel.
- **Pricing page**: the three tiers (Basic / Pro / Business) from `docs/05-subscription-model.md` with monthly/annual toggle, feature comparison, trial messaging, and per-tier CTA.
- **Business / B2B page**: dedicated partner story (mail forwarders, RV-friendly attorneys, tax preparers, RV parks), white-label API pitch, and a "contact sales / partner with us" CTA.
- **Detailed value / product page**: deeper explanation of what a subscriber actually receives (digest sample, alert timeline 60/30/7, category coverage), reinforcing confidence with concrete artifacts.
- **Contact page**: a public contact form for general/press/support inquiries, protected against spam (honeypot + server-side rate limiting; a real captcha as a documented upgrade path if abuse appears).
- Use free, properly-licensed stock photography (e.g. Unsplash/Pexels) where imagery strengthens the story (RV lifestyle, open road, workspace-on-the-road), with attribution/licensing tracked; optimized and lazy-loaded.
- Shared marketing chrome: header nav, footer, consistent brand styling (existing Tailwind v4 + shadcn stack), responsive + accessible.
- Email-capture CTA posts to a lead-capture endpoint (funnel entry); no new backend billing work — "Start free trial" / "Sign in" route into the existing MSAL flow.

## Capabilities

### New Capabilities
- `marketing-site`: Public, unauthenticated marketing surface — landing/home, product/value detail, and shared marketing chrome (header, footer, responsive layout) with acquisition CTAs.
- `pricing-page`: Public pricing presentation of the Basic/Pro/Business tiers with monthly/annual toggle, feature comparison, trial messaging, and per-tier call-to-action.
- `b2b-partner-page`: Dedicated B2B/partner surface covering white-label API, partner personas, and a contact/partner CTA.
- `marketing-routing`: Client-side routing that separates public marketing routes from the MSAL-gated authenticated app and preserves the existing signed-in experience.
- `contact-page`: Public contact page with a spam-resistant contact form (honeypot + rate limiting, upgradeable to a real captcha) that submits an inquiry to the backend.

### Modified Capabilities
<!-- No existing openspec/specs/ capabilities change requirement-level behavior; the authenticated app is relocated behind routing but its behavior is unchanged. -->

## Impact

- **Code**: `src/portal/` — new public pages/components, routing layer, marketing chrome; `App.tsx` refactored so the current authenticated experience mounts under a gated route.
- **Dependencies**: adds a router (`react-router-dom`) to the portal.
- **Backend/API**: one lead-capture endpoint for the email CTA (or reuse an existing subscriber pre-registration path if present); no billing/Stripe changes in this change.
- **Content**: pricing/persona/B2B copy sourced from `docs/05-subscription-model.md`; editorial/brand copy owned by Jenn (marketing) per `CLAUDE.md`.
- **Non-goals**: Stripe checkout wiring, blog/CMS, SEO content pipeline, and the lead-magnet PDF — deferred.
