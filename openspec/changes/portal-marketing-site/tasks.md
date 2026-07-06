## 1. Routing & scaffolding

- [x] 1.1 Add `react-router-dom` to `src/portal/package.json`; wrap the app in a `BrowserRouter`
- [x] 1.2 Move the current authenticated body of `App.tsx` into an `AppShell` mounted at `/app/*`; extract the `if (!isAuthenticated)` logic into an `AuthGuard` that invokes MSAL sign-in for anonymous users on gated routes
- [x] 1.3 Add public routes `/`, `/product`, `/pricing`, `/business` under a `MarketingLayout`; verify deep-linking/refresh on each renders without a sign-in redirect
- [ ] 1.4 Smoke-check the existing signed-in flow (login → profile → feed → sign out) still works after the refactor

## 2. Marketing chrome

- [x] 2.1 Build `MarketingLayout` with a responsive header (nav: Home, Product, Pricing, Business + Sign in CTA) and a footer, using existing shadcn/Tailwind components
- [x] 2.2 Wire the header Sign in / Start free trial CTAs to the existing MSAL `loginRedirect`

## 3. Home / landing page

- [x] 3.1 Hero: core promise + primary CTA above the fold
- [x] 3.2 Story-driven value narrative section (targeted at full-time RVers, human/plain-language copy)
- [x] 3.3 "How it works" section (detection → plain-English summary → renewal-anchored 60/30/7 alert)
- [x] 3.4 Trust/confidence section (honest coverage statement: insurance-first, TX-first)
- [x] 3.5 Email-capture CTA component with client-side email validation and confirmation state

## 4. Product / value detail page

- [x] 4.1 Deeper "what you receive" content: sample digest and sample renewal alert with the 60/30/7 timeline
- [x] 4.2 Category/state coverage detail, stated plainly

## 5. Pricing page

- [x] 5.1 Define a typed `tiers` array (Basic/Pro/Business) sourced from `docs/05-subscription-model.md`; annual price derived (monthly × 10)
- [x] 5.2 Render tier cards + feature comparison + target persona per tier
- [x] 5.3 Monthly/annual toggle updating displayed prices
- [x] 5.4 14-day free-trial (no card) messaging and a per-tier CTA routing into the sign-in flow

## 6. Business / B2B page

- [x] 6.1 Partner-focused hero + persona sections (mail forwarders, attorneys, tax preparers, RV parks)
- [x] 6.2 White-label API value proposition + example use cases
- [x] 6.3 "Partner with us" CTA links to the contact page with topic pre-set to "partnership"

## 7. Contact page

- [x] 7.1 Build the `/contact` page and form (name, email, topic, message) with client-side validation and a confirmation state
- [x] 7.2 Add a hidden honeypot field to the form; add Contact to the header/footer nav
- [x] 7.3 Add a `contact(payload)` method to `src/portal/src/lib/api.ts` wiring the form (and the B2B partnership CTA) to the backend contact endpoint

## 8. Lead capture & contact backend

- [x] 8.1 Confirm whether the API already exposes a lead/pre-registration endpoint; reuse it if so
- [x] 8.2 If none exists, add a minimal unauthenticated `POST /api/leads {email}` with server-side email validation and basic rate limiting
- [x] 8.3 Add `POST /api/contact` handling the inquiry: reject when the honeypot is filled, per-IP rate limit, validate email/message; structure it so a captcha-token check can be added later without changing the request shape
- [x] 8.4 Wire the portal `leads`/`contact` API methods to these endpoints

## 9. Imagery

- [x] 9.1 Source RV-lifestyle/open-road/workspace imagery from free licensed stock (Unsplash/Pexels); download and optimize into `src/portal/public/`
- [x] 9.2 Record source + license per image in a `CREDITS.md` alongside the images; apply `loading="lazy"` to below-the-fold images and alt text throughout

## 10. Verification

- [x] 10.1 Verify anonymous visitor can reach all public routes (incl. `/contact`) with no sign-in redirect; authenticated user still reaches `/app`
- [x] 10.2 Verify responsive layout (mobile + desktop) and accessibility basics (landmarks, keyboard nav, contrast, alt text)
- [ ] 10.3 Verify email capture and contact form post and confirm; invalid input rejected client-side; honeypot-filled and rate-exceeded submissions rejected server-side
- [x] 10.4 Run `openspec validate portal-marketing-site --strict`
