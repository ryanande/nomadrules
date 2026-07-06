## Context

`src/portal/` is a Vite + React 19 + Tailwind v4 + shadcn SPA. `App.tsx` short-circuits to a `Login` component whenever `useIsAuthenticated()` is false — there is no router and no public surface. All the marketing substance (tiers, personas, B2B/API story, funnel) already lives in `docs/05-subscription-model.md`; this change gives it a web home and gates the existing app behind routing. Brand/editorial copy is owned by Jenn (`CLAUDE.md`); this change ships the structure and placeholder-quality copy she can refine.

## Goals / Non-Goals

**Goals:**
- Public, unauthenticated marketing surface: home, product/value detail, pricing, business.
- Preserve the existing authenticated profile/feed experience unchanged, now behind a gated route.
- Email lead capture feeding the acquisition funnel.
- Professional, human, responsive, accessible — reuse the existing Tailwind/shadcn design system.

**Goals (added):**
- Public contact page with a spam-resistant form.
- Professional imagery from free, licensed stock sources.

**Non-Goals:**
- Stripe checkout / real trial provisioning (CTAs route into existing MSAL sign-in only).
- A third-party captcha service on day one (honeypot + rate limit first; captcha is a documented upgrade).
- Blog/CMS, SEO content pipeline, the lead-magnet PDF.
- New billing backend or tier-enforcement logic.

## Decisions

**Routing: add `react-router-dom`.** The app needs multiple public URLs plus a gated section; a router is the native fit and enables deep links / refresh on marketing pages. Alternative (state-based view switching like the current `view` state) was rejected — it can't give shareable URLs or clean gated/public separation. Structure: public routes (`/`, `/product`, `/pricing`, `/business`) under a `MarketingLayout`; the existing app moves under `/app/*` behind an auth guard that invokes MSAL when unauthenticated. `App.tsx`'s current authenticated body becomes the `/app` element; its `if (!isAuthenticated) return <Login/>` logic moves into the guard.

**Content is code, sourced from the doc.** Pricing/persona/B2B copy is hardcoded from `docs/05-subscription-model.md` (single source), not a CMS. A small typed `tiers` array drives the pricing table and the monthly/annual toggle so the two stay in sync. `// ponytail:` note the annual price is derived (monthly × 10) rather than a second hardcoded column, matching the doc's "2 months free."

**Lead capture: one endpoint.** The email CTA POSTs to a lead-capture endpoint via the existing `lib/api.ts` client. If no such endpoint exists on the API, add a minimal unauthenticated `POST /api/leads {email}` that inserts/ignores; keep it dumb (no drip logic here). Reuse the subscriber pre-registration path if one already exists rather than adding a table.

**Marketing chrome as shared layout.** A `MarketingLayout` (header nav + footer + `<Outlet/>`) wraps public pages so nav/footer are defined once. Sign-in CTA calls the same MSAL `loginRedirect` used today. Nav includes a Contact link.

**Contact page + anti-spam: honeypot + rate limit, not a captcha (yet).** The contact form (`/contact`) POSTs to a backend contact endpoint. Spam mitigation is a hidden honeypot field (rejected server-side if filled) plus per-IP rate limiting — zero user friction, no third-party dependency, no privacy/GDPR surface. `// ponytail: honeypot + rate limit; wire Cloudflare Turnstile only if real spam shows up.` A full captcha (Turnstile) was considered but rejected for a pre-launch, low-traffic site: it adds a script dependency and a paid/keyed service for a threat that isn't yet real. The endpoint is structured so a Turnstile token check can be added later without changing the form's data shape. The B2B page's "partner with us" CTA links to this same contact page (topic pre-set to "partnership") rather than shipping a second form.

**Stock photography: free/licensed, self-hosted.** Photographic imagery comes from Unsplash/Pexels (free, commercial-use licenses). Files are downloaded, optimized, and served from `src/portal/public/` (not hotlinked), with source + license recorded in a `CREDITS.md` under the images dir. Below-the-fold images use native `loading="lazy"`. `// ponytail:` no image CDN/pipeline — static optimized assets are enough at launch.

## Risks / Trade-offs

- **Copy quality** → structure ships with honest placeholder copy; Jenn owns final marketing language. Coverage claims stay truthful (insurance-first, TX-first) to avoid overpromising.
- **Adding a router touches `App.tsx`** → risk of regressing the signed-in flow. Mitigation: the authenticated body is moved verbatim under the guarded route; a manual sign-in smoke check is in tasks.
- **Lead endpoint is unauthenticated** → spam/abuse surface. Mitigation: server-side email validation + basic rate limiting; no PII beyond email; `// ponytail:` captcha deferred until abuse is observed.
- **Prices drift from Stripe** → the page is marketing, not the billing source of truth; a note in tasks flags keeping the `tiers` array aligned with `docs/05-subscription-model.md` / Stripe products.

## Migration Plan

Purely additive to the portal; no data migration. Deploy is a normal portal build. Rollback = revert the portal change; the API lead endpoint (if added) is independent and harmless if orphaned.

## Open Questions

- Does the API already expose a lead/pre-registration endpoint to reuse, or is a new `POST /api/leads` needed? (Resolve during task 1.)
- Business-page contact CTA: lightweight form → lead endpoint, or a `mailto:`/scheduling link for v1? Default to the form; downgrade to `mailto:` if backend contact handling is out of scope.
