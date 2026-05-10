# Subscription Model

## Pricing tiers

| | Basic | Pro | Business |
|--|-------|-----|----------|
| **Price** | $9/mo · $90/yr | $19/mo · $190/yr | $49/mo · $490/yr |
| **Domicile states** | 1 | Up to 3 | Unlimited |
| **Categories** | Insurance + DMV | All categories | All categories |
| **Weekly digest** | ✓ | ✓ | ✓ |
| **Urgent alerts** | — | ✓ | ✓ |
| **Law change archive** | 90 days | Unlimited | Unlimited |
| **Team seats** | 1 | 1 | Up to 10 |
| **White-label API** | — | — | ✓ |
| **Priority support** | — | — | ✓ |
| **Target persona** | Retired RVers, casual users | Active full-timers, nomad entrepreneurs | Attorneys, tax preparers, mail forwarding services, RV parks |

## Stripe configuration

- **Products:** `nomrules_basic`, `nomrules_pro`, `nomrules_business`
- **Prices:** Monthly and annual variants per product (6 prices total)
- **Annual discount:** ~17% (2 months free)
- **Trial:** 14-day free trial on all tiers — no credit card required at signup
- **Billing portal:** Stripe Customer Portal embedded in subscriber portal for self-serve upgrades, downgrades, and cancellations

**Webhook events to handle:**
```
customer.subscription.created
customer.subscription.updated
customer.subscription.deleted
invoice.payment_failed
invoice.payment_succeeded
customer.subscription.trial_will_end
```

## Freemium / acquisition funnel

**Free tier (unregistered):** Public-facing law change feed — last 3 items per state, no alert preferences, no digest. Conversion CTA on every item: "Get this delivered to your inbox."

**Lead magnet:** "The Full-Timer's Domicile Checklist" — a free downloadable PDF covering SD/TX/FL setup steps. Captures email → enters drip sequence → soft pitch for Basic tier.

**Community distribution:** Post in Escapees forums, iRV2, Facebook groups (Full Time RVers, RV Entrepreneurs, etc.) with free state-specific weekly summaries. Link to sign up for personalized delivery.

## Revenue projections (conservative)

| Month | Subscribers | MRR | Notes |
|-------|------------|-----|-------|
| 1 | 50 | $600 | Friends, community launch |
| 3 | 200 | $2,400 | Organic community growth |
| 6 | 500 | $6,500 | SEO traction, partnerships |
| 12 | 1,500 | $20,000 | Affiliate + B2B tier kicking in |

These are conservative. The RV community is tight-knit and word-of-mouth driven. One post in a major Facebook group (Full Time RVers, 200k members) can drive hundreds of signups.

## Affiliate & partnership revenue

This is pure margin — no additional operational cost.

| Partner type | Commission model | Estimated partners | Monthly potential |
|-------------|------------------|--------------------|-------------------|
| Mail forwarding services (Escapees, Traveling Mailbox, America's Mailbox) | $20–$50 per signup | 3–5 | $500–$2,000 |
| RV-friendly attorneys (SD, TX, FL domicile specialists) | $50–$150 per lead | 5–10 | $500–$3,000 |
| RV insurance brokers (Progressive, National General, RVerInsurance.com) | 5–10% of first premium | 3–5 | $1,000–$5,000 |
| Tax preparers (Tax Queen, etc.) | $30–$75 per referral | 3–5 | $300–$1,500 |

**Implementation:** Contextual affiliate links within law change summaries. When a South Dakota insurance minimum changes, the summary links to an SD-specialized insurance broker. Natural, high-relevance placement.

## B2B / API tier (Business)

White-label API allows partners to embed NomadRules data in their own products:

- Mail forwarding services can send clients automated law change alerts under their own brand
- RV park operators offering domicile assistance can include a "law updates" section in their client portal
- Attorneys can subscribe to raw change feeds for their practice area

**API endpoints exposed to Business tier:**
```
GET /api/v1/changes?state=SD&category=insurance&since=2025-01-01
GET /api/v1/changes/{id}
POST /api/v1/webhooks          # register a webhook URL for push delivery
GET /api/v1/sources            # list monitored sources
```

**Rate limits:** 1,000 requests/day per Business subscriber. Higher limits available on custom enterprise pricing.

## Cost structure

At scale, the system is extremely low-cost to operate:

| Cost item | Estimated monthly (at 1,000 subscribers) |
|-----------|------------------------------------------|
| Azure (Functions, Container Apps, Cosmos, Service Bus) | ~$80–$150 |
| Claude API (summarization) | ~$20–$50 (est. 500 changes/mo at ~$0.10/change) |
| Email delivery (Resend) | ~$20 |
| Stripe fees | 2.9% + $0.30 per transaction (~$400 at $14k MRR) |
| Domain, misc | ~$20 |
| **Total** | **~$550–$640/mo** |

At $14,000 MRR, that's >95% gross margin on infrastructure. The business scales to meaningful revenue with near-zero marginal cost per subscriber.
