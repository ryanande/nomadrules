# Assumptions and Risks — NomadRules v0.1

**Date:** May 2026  
**Version:** Texas Renewal Radar (multi-category, calendar-personalized)  
**Audience:** Founders + team, reference during build

---

## Core assumptions

| # | Assumption | Risk if wrong | Validation signal |
|---|-----------|---------------|-------------------|
| 1 | Users will fill in renewal dates accurately and keep them updated | Calendar becomes stale → personalization breaks → emails sent to wrong timing | User comes back after 30 days; calendar dates match reality |
| 2 | Claude can summarize tax/DMV/voting as well as insurance | Bad summaries on complex topics → customer loses trust in entire product | Internal read on first 5 Claude outputs; customer feedback |
| 3 | Texas is a large enough TAM to get meaningful signal | 50 free users, 2 paying = not enough data to iterate | Reach 100+ free users; observe at least 5% conversion to paid |
| 4 | Jenn's voice/distribution differentiates vs. Escapees/FMCA | Free alternatives are trusted + free; why pay? | Measure engagement + conversion; A/B test "Jenn's voice" copy vs. generic |
| 5 | TX government sites are stable enough to scrape | One scraper breaks → user pays but gets no content | Daily monitoring of scraper health; customer refund policy ready |
| 6 | Generic "please pay" CTA after 30 free days converts | User gets value, but $5/mo is not urgent; conversion flatlines | Test event-triggered CTAs ("renewal in 45 days") vs. time-triggered |
| 7 | B2C is the right wedge for v0.1 | Might be outcompeted on consumer side; B2B is actually lower friction | Test B2B outreach in parallel (attorneys, mail forwarders) |

---

## Load-bearing risks

### Technical

**Claude hallucination on complex topics (tax, voting)**
- **Mitigation:** Start with insurance only (weekly changes, easy to verify). Add tax/voting once insurance is proven.
- **Owner:** Ryan (code review of summaries)
- **Decision point:** After first 10 summaries, internal vote: "Is quality good enough?" If no, pivot to insurance-only.

**Scraper fragility (government sites change structure)**
- **Mitigation:** Monitor scraper health daily. If TX DMV goes down, alert user, don't send false "no changes" email.
- **Owner:** Ryan (monitoring + alerts)
- **Decision point:** Month 1 — if 2+ sources break, revisit scraping vs. API-first strategy.

**Calendar data staleness**
- **Mitigation:** Refresh reminder email every 30 days. "Update your dates if anything changed."
- **Owner:** Ryan (email template) + Jenn (voice)
- **Decision point:** Week 4 — if >20% of users have outdated dates, pivot to auto-population (integration with registration records, etc.).

### Product

**Willingness-to-pay at $5/mo**
- **Mitigation:** Don't rely on generic time-triggered CTAs. Test event-triggered: "Your renewal in 45 days — unlock full tracking for $5."
- **Owner:** Jenn (copy testing)
- **Decision point:** After 30 free users, run two CTA variants. Pick winner or kill time-triggered model.

**Free tier cannibalizes paid**
- **Mitigation:** Free tier shows only latest 3 items per category, no history search, no personalization. Paid unlocks full archive + notifications.
- **Owner:** Jenn (positioning) + Ryan (UI gates)
- **Decision point:** Week 2 — lock down what free vs. paid actually means. Be ruthless about gates.

**Churn after free trial ends**
- **Mitigation:** Don't assume monthly churn is "normal." Track cohort retention by acquisition channel. If people acquired via Facebook churn faster, that's a signal.
- **Owner:** Jenn (community channels) + Ryan (analytics)
- **Decision point:** Month 2 — if churn >50% for any cohort, pivot messaging or channel.

### Business

**B2C alone may not be viable; B2B is the real revenue**
- **Mitigation:** Parallel test B2B in month 1. Reach out to 5 TX RV attorneys + 3 mail forwarders. Offer: "Weekly TX summary, you rebrand + send to clients."
- **Owner:** Jenn (outreach)
- **Decision point:** Month 1 — if 2+ B2B leads are warm, shift focus. B2B might be the wedge, B2C the upside.

**Competitive commoditization (Escapees/FMCA already do this)**
- **Mitigation:** Differentiation is Jenn's voice + personalization (calendar) + depth (you pick categories). Lead with "written for your renewal" not "generic law updates."
- **Owner:** Jenn (voice + positioning)
- **Decision point:** Month 1 — monitor Escapees/FMCA + NomadRules positioning. If they add personalization, pivot faster to B2B.

---

## Sequencing decisions

### Decision 1: Insurance-first vs. multi-category day 1

**Chosen:** Insurance first. Add tax/DMV/voting after insurance quality is proven.

**Rationale:**
- Insurance changes weekly → feedback in 1 week, not 1 month
- Easiest to summarize → lowest Claude hallucination risk
- Clearest ROI message → "your October renewal, here's what changed"
- Lower initial risk, expand once proven

**Tradeoff:** Narrower appeal initially. Could miss tax-motivated personas (nomad entrepreneurs).

**Pivot trigger:** If insurance adoption is strong but user request volume for "tax" is high, accelerate tax addition.

### Decision 2: B2C lead gen via calendar vs. B2B direct sales

**Chosen:** B2C as primary, B2B as parallel test (month 1).

**Rationale:**
- B2C is bigger TAM, higher ceiling
- Calendar tool is an easy lead magnet (free)
- B2B validates product-market fit before scaling consumer

**Tradeoff:** B2B might close faster and be less competitive. If B2B signals are strong, consider flipping priority.

**Pivot trigger:** If B2B close rate is >50% and B2C is <2%, rebalance.

### Decision 3: Time-triggered vs. event-triggered pricing

**Chosen:** Event-triggered (renewal-based CTAs). Time-triggered as fallback.

**Rationale:**
- Clear ROI at moment of need
- Higher willingness-to-pay
- Better messaging ("Your renewal in 45 days...")

**Tradeoff:** More complex to implement (need to track user's renewal dates). Higher initial engineering lift.

**Pivot trigger:** If event-triggered CTAs underperform time-triggered, revert.

---

## Validation plan (in priority order)

| Phase | Experiment | Owner | Timeline | Success criteria |
|-------|-----------|-------|----------|------------------|
| 0 | Audit: what do Escapees/FMCA/NAIC actually cover? | Jenn | Week 1 | Identify 3 specific gaps we own |
| 1 | Ship renewal calendar + insurance crawler (TX only) | Ryan | Weeks 1-2 | Scraper runs daily, 0 errors |
| 1 | Jenn posts in 3 RV Facebook groups (free preview) | Jenn | Week 2 | 50+ signups to calendar |
| 2 | Collect first 10 Claude summaries; internal quality review | Ryan + Jenn | Week 3 | Decision: "Keep multi-category" or "Insurance-only" |
| 2 | B2B outreach: contact 5 TX RV attorneys | Jenn | Week 3 | 2+ warm conversations |
| 3 | A/B test CTAs: event-triggered vs. time-triggered | Ryan + Jenn | Week 4 | Learn which converts higher |
| 3 | Track free → paid conversion rate | Ryan | Week 4+ | Baseline for future iterations |
| 4 | Month 2 cohort analysis: retention by channel | Ryan | Month 2 | Identify strongest acquisition channel |

---

## Open questions (to resolve before proposal)

1. **Should we auto-populate renewal dates from public data (TX vehicle registration lookup) or keep them manual?**
   - Manual: faster to ship, user responsibility
   - Auto: better UX, but requires data integration work

2. **If B2B signals are strong, do we pivot away from B2C or run parallel?**
   - Parallel: slower, more complex, but captures both
   - Pivot: faster, more focused, but risks B2C upside

3. **How many categories should we actually launch with?**
   - Insurance-only: lower risk, slower to full value
   - Insurance + Tax: middle ground
   - All four: full vision, higher risk

4. **Do we need Stripe + portal from day 1, or is Stripe payment link + email sufficient?**
   - Portal: more professional, but 2-3 weeks of build
   - Payment link: ship in days, can upgrade later
