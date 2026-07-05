# Proposal: Texas Renewal Radar MVP

> **SUPERSEDED (2026-07-04).** This umbrella epic has been delivered piecemeal by focused per-capability
> changes, which are now the unit of work. Archived to keep the active backlog honest.
>
> | Capability | Delivered by |
> |---|---|
> | subscriber-api | subscriber API + DbUp migration runner (#1) |
> | law-change-summarizer | summarizer service |
> | digest-delivery + renewal-radar | email-delivery-service (#3), archived |
> | subscriber-portal + renewal-calendar-tool | subscriber portal (#4) |
> | law-change-crawler | scaffolded (crawler service); a focused change tracks completion |
>
> Retained for historical context and the original architecture references below.

**For full architectural context, see:**
- **/.claude/CLAUDE.md** — System identity, principles, service contracts
- **/.claude/ARCHITECTURE.md** — 5-level progressive disclosure (identity → high-level → services → deployment → implementation)
- **/.claude/AGENTS.md** — Team ownership and responsibilities
- **/.claude/decisions.md** — Validation roadmap and risk register

---

## Why

Full-time RVers and digital nomads domiciled in Texas face a critical blind spot: they research domicile law once (establishing insurance, vehicle registration, tax structure), then never revisit it. Laws change constantly across insurance, tax, DMV, and voting domains, but there's no service delivering personalized, plain-English alerts tied to *when those changes actually matter* — at renewal time.

NomadRules solves this by anchoring regulatory alerts to the user's renewal calendar. Instead of generic "here's what changed this week," we deliver "Your insurance renews in 60 days — here's what changed since last year." This is more valuable, more actionable, and easier to monetize.

We're starting with Texas (our domicile + unfair distribution advantage via community) and insurance-first (weekly changes, easiest to summarize, clearest ROI). We'll prove the model with a small, tight product before expanding categories or states.

## What Changes

- **New free tool**: Renewal Calendar. Users enter domicile state, renewal dates for insurance/registration/license/taxes. We store email + dates.
- **New automated crawler**: Watches TX insurance bulletins, tax news, DMV changes, voting rules, federal IRS news. Detects changes daily.
- **New AI summarization**: Claude API summarizes detected changes in plain English ("What changed, who it affects, what to do").
- **New subscriber API**: User account management, profile preferences, notification history, Stripe billing integration.
- **New delivery layer**: Personalized email digests (weekly) + urgent alerts (immediate).
- **New renewal-triggered pricing model**: Base pricing is monthly subscriptions, but primary CTA is event-driven ("Your renewal in 45 days — $5 to unlock").

Breaking changes: None (new product, no backward compat concerns).

## Capabilities

### New Capabilities

- `renewal-calendar-tool`: Free public web tool; users enter renewal dates (insurance, vehicle registration, license, taxes); captures email + dates for personalization
- `law-change-crawler`: Automated Playwright-based scraper for TX government sources (Division of Insurance, Comptroller, DMV, SOS) + Federal (IRS). Detects diffs daily. Initially insurance-only; tax/DMV/voting added after quality validation.
- `law-change-summarizer`: Claude API integration; takes raw HTML/text of law change, produces headline + plain-English summary + severity score (urgent/routine/informational). Includes validation checkpoints for accuracy.
- `subscriber-api`: REST API (ASP.NET Core); manages user accounts, profile updates (states of interest, categories, alert preferences), subscription lifecycle, Stripe webhooks.
- `digest-delivery`: Personalized weekly digest engine; queries subscriber profiles, matches changes to user interests, batches, renders, sends via email (Resend). Urgent alerts bypass digest (immediate send).
- `renewal-radar`: Event-triggered alert system; compares user's renewal calendar dates to detected law changes; sends personalized CTA ("Your insurance renews in 60 days, 3 things changed") 60/30/7 days before each renewal.
- `subscriber-portal`: React web app; onboarding (calendar setup), dashboard (recent changes), law change archive/search, account management.

### Modified Capabilities

None. Greenfield product.

## Impact

### Architecture

See **/.claude/ARCHITECTURE.md** for the comprehensive 5-level progressive disclosure architecture. Summary:

- **Monorepo, multiple languages**: Single git repo with crawlers (TypeScript), backend (C# ASP.NET Core with Flint pattern), portal (React/TypeScript), and infrastructure (Helm/Terraform)
- **Kubernetes-native**: All services run as K8s Deployments/StatefulSets/CronJobs; deployed via Helm charts and ArgoCD GitOps
- **Event-driven via AsyncBus**: Services communicate through RabbitMQ/AMQP; choreography, not orchestration; consumer-driven contracts
- **Crawler**: TypeScript/Node.js; Playwright for JS-rendered content; publishes `law_change_detected` events
- **Summarizer**: C# service; consumes `law_change_detected`; calls Claude API; publishes `law_change_summarized`
- **Subscriber API**: ASP.NET Core with Flint pattern; vertical slices (SubscriberRegistration, Profile, Feed, RenewalAlerts, Billing); manages subscribers, renewal dates, profiles
- **Renewal-Triggered Logic**: Event matching system that compares law change dates to subscriber renewal dates; triggers alerts at 60/30/7 days
- **Email Delivery**: C# async handler; event-driven; sends via Resend API
- **Portal**: React + TypeScript with shadcn/ui; public feed, renewal calendar, authenticated dashboard, archive with search
- **Data**: PostgreSQL (subscribers, law changes, notifications); considered SQLite for v0.1 but K8s setup recommends PostgreSQL with persistent volume
- **Payments**: Stripe (subscriptions + webhooks; handled by API)
- **Infrastructure**: Terraform optional (K8s cluster provisioning); Helm charts for templating; ArgoCD for GitOps deployments
- **Observability**: Prometheus + Grafana, Jaeger distributed tracing, structured logging to ELK/Seq

**Key Principles**:
1. Business needs first (renewal-triggered model shapes event-driven design)
2. Choreography over orchestration (no central saga coordinator)
3. Autonomous services (each owns its database schema)
4. Vertical slices in API (domain-driven, not layer-driven)
5. Consumer-driven contracts (event schemas versioned in code)
6. Progressive disclosure (documentation from exec vision to implementation)

### Constraints

- **Insurance-only for v0.1**: Tax, DMV, voting added only after insurance summary quality is proven internally (see validation plan in /.claude/decisions.md and docs/00-assumptions-and-risks.md).
- **Texas-only**: Expand to SD/FL only if Texas signal is strong (100+ free users, 5+ paying). B2B parallel testing may change this priority.
- **Pricing**: $5 Basic (1 state, digest) / $9 Pro (multi-state, urgent alerts) / $25 Business (B2B API). Anchored to renewal-event ROI, not generic monitoring.
- **Ownership**: See /.claude/AGENTS.md for detailed team responsibilities. Ryan owns crawler, API, summarizer, infrastructure, DevOps. Jenn owns portal UI/UX, email templates, community messaging.
- **Monorepo with Kubernetes**: Single git repo required; services deployed to K8s via Helm/ArgoCD. No Azure serverless — K8s aligns with production-readiness and enterprise patterns (tpl-onyx/.NET standards).
- **Tight feedback loop**: Ship calendar tool week 1, first crawler output week 2, first paid conversion week 3-4. Each artifact is public/measurable.
- **Architecture first**: All implementation tasks must align with /.claude/ARCHITECTURE.md (5-level progressive disclosure). No shortcuts that contradict the architectural principles.

### Data Model

- **subscribers**: id, email, domicile_state, renewal_dates (insurance, registration, license, taxes), categories (insurance only in v0.1), tier, stripe_customer_id
- **law_changes**: id, source_id, url, raw_content, headline, summary, severity, tags, affected_states, detected_at, processed_at
- **sources**: id, name, url, strategy (html-diff, rss, pdf), schedule (cron), state, category, last_checked_at, last_change_detected_at, enabled
- **notifications**: id, subscriber_id, law_change_id, sent_at, delivery_type (digest, urgent), opened_at

### Dependencies

- **Runtime**: Node 22, .NET 9
- **Third-party**: Claude API (Anthropic SDK), Stripe SDK, Resend SDK, @azure SDKs (Blob, CosmosDB, ServiceBus, Functions), Playwright
- **CI/CD**: GitHub Actions (tests, build, deploy to Azure)

## Success Criteria

- **Week 2**: Calendar tool public; 50+ signups (proof of distribution)
- **Week 3**: First 10 Claude summaries generated; internal quality review (proof of AI layer)
- **Week 4**: First conversion to paid (proof of willingness to pay)
- **Month 1**: 100+ free users, 5+ paying, <1% error rate on crawler (proof of v0.1 viability)
- **Month 1**: B2B outreach (5 attorneys contacted; 2+ warm leads; indicates pivot potential)

## Decisions Locked In

✅ **Kubernetes + Helm + ArgoCD** — Production-ready, aligns with enterprise patterns (tpl-onyx/.NET standards)  
✅ **Monorepo** — Single git repo with crawlers (TypeScript), backend (C#), portal (React/TypeScript), infra (Helm)  
✅ **PostgreSQL** — ACID guarantees, JSON support, persistent volume on K8s  
✅ **Flint pattern** — Convention-based .NET DI startup for ASP.NET Core API  
✅ **Event-driven via AsyncBus** — RabbitMQ/AMQP choreography, consumer-driven contracts  
✅ **Renewal-triggered pricing** — $5/$9/$25 anchored to renewal events, not generic monitoring  

## Open Questions

1. **Auto-populate renewal dates** — Pull from TX vehicle registration API, or keep manual entry with calendar tool? (Manual = faster ship, auto = better UX/retention; see /.claude/decisions.md for validation plan)
2. **Email rendering** — React template engine (JSX-to-HTML), or simple Handlebars templates? (React = consistent with codebase, Handlebars = simpler)
3. **Secrets management** — Kubernetes Sealed Secrets, or Azure Key Vault? (Sealed Secrets = K8s native, Key Vault = Azure integration if needed later)
4. **AsyncBus implementation** — Mass Transit (NServiceBus alternative) or direct AMQP.NET Lite? (Mass Transit = more features, AMQP.NET = simpler, less magic)
