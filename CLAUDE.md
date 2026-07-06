# CLAUDE.md — NomadRules System Passport

## System Identity

**NomadRules** is a personalized legal & regulatory intelligence service for full-time RVers, digital nomads, and domicile-state residents. It monitors state and federal law changes (insurance, tax, DMV, voting) and delivers plain-English digests anchored to each subscriber's renewal calendar.

**Wedge**: Event-triggered renewal alerts (60/30/7 days before insurance, registration, license, and tax renewals) create a clear ROI moment and reduce churn vs. generic monitoring.

**Success vision**: Self-managing pipeline that requires zero weekly human intervention after initial setup.

---

## Bounded Context

**Domains Owned By This System**:
- Subscriber identity and profile management (domicile state, renewal dates, category preferences)
- Law change detection and monitoring (crawling, diffing, change classification)
- AI-powered summarization and relevance scoring (Claude API integration)
- Renewal calendar and alert triggering (event-driven scheduling)
- Billing and subscription lifecycle (Stripe integration)
- Digest and alert delivery (email via Resend)

**Domains This System Does NOT Own**:
- RV community forums, social platforms, or content creation (Jenn's domain - marketing)
- Government regulatory source maintenance or scraper resilience beyond our crawlers
- Email rendering engine or deliverability optimization beyond Resend API
- Legal expertise or compliance validation (editorial review happens externally)

---

## Core Architecture Principles

1. **Business needs first** — Revenue model (renewal alerts) shapes technical decisions (event-driven, not time-triggered)
2. **Choreography over orchestration** — Services emit events via AsyncBus; no central saga coordinator
3. **Async over sync** — All inter-service communication is event-driven
4. **Consumer-driven contracts** — Each service defines what events it produces/consumes; versioned in code
5. **Autonomous services** — Services own their database schema; no shared tables across services
6. **Vertical slices** — API organized by domain concept (SubscriberRegistration, LawChangeFeed, RenewalAlerts), not technical layers
7. **Observable services** — All services emit logs, metrics, traces; central observability stack (Prometheus, Grafana, Jaeger)
8. **Pragmatism over dogmatism** — SQLite for v0.1 (not Cosmos), simple queue table (not NServiceBus); superseded by Azure Database for PostgreSQL Flexible Server once every service moved into AKS (see `openspec/changes/aks-secure-platform-deployment/`). Auth was magic links for v0.1; superseded by Azure Entra External ID (CIAM) for subscribers + Entra ID RBAC for team/admin access, both provisioned via Terraform (see `openspec/changes/azure-entra-auth-iac/`)
9. **Progressive disclosure** — Documentation unfolds from executive vision (Level 0) to implementation details (Level 4)
10. **Monorepo, multiple languages** — Single git repo; crawlers in TypeScript, backend in .NET (Flint pattern), portal in React/TypeScript
11. **Kubernetes-native deployment** — All services run as K8s Deployments/StatefulSets/CronJobs; Helm templates, ArgoCD GitOps
12. **Insurance-first, insurance-only for MVP** — v0.1 covers insurance-only and Texas-only; multi-category and multi-state follow
13. **Tight feedback loop** — Weekly validation signals from real users drive feature prioritization

---

## Service Contracts (Event Schema)

### Events Published

All events follow a common envelope:
```json
{
  "eventType": "string (PascalCase)",
  "eventId": "UUID",
  "timestamp": "ISO 8601",
  "correlationId": "UUID",
  "payload": { /* service-specific data */ }
}
```

**Crawler** publishes:
- `law_change_detected` — { sourceId, state, category, rawText, url, detectedAt }

**Summarizer** publishes:
- `law_change_summarized` — { changeId, plainEnglishSummary, headline, affectedSegments, qualityScore }

**Scorer/Tagger** publishes:
- `law_change_scored` — { changeId, severity, actionRequired, deadline }
- `law_change_tagged` — { changeId, states[], categories[], segments[] }

**Subscriber API** publishes:
- `subscriber_registered` — { subscriberId, email, domicile, categories }
- `subscriber_profile_updated` — { subscriberId, renewalDates, categoryPreferences }

**Email Delivery** publishes:
- `digest_sent` — { subscriberId, itemCount, sentAt }
- `urgent_alert_sent` — { subscriberId, changeId, sentAt }

**Portal** consumes all events for real-time feed and notification history.

### Service Dependencies

```
Crawler → AsyncBus
  └─→ Summarizer → AsyncBus
       └─→ Scorer/Tagger → AsyncBus
            └─→ Subscriber API (matches change to profiles)
                 └─→ Email Delivery Service (routes to digest or urgent)
                      └─→ Portal (displays feed)
```

---

## Technology Stack at a Glance

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Crawlers** | TypeScript, Playwright, Node.js | Playwright handles JS-rendered content; node ecosystem strong for web scraping |
| **Backend API** | C# ASP.NET Core, Flint pattern | Type-safe, LINQ for Cosmos/SQL queries, DI via Flint convention-based startup |
| **Summarizer** | C# Azure Functions or dedicated service | CPU-bound; Functions for cost efficiency, dedicated svc for throughput control |
| **Email Service** | C# async handler, Resend API | Event-driven delivery; Resend for simplicity and relay support |
| **Portal** | React, TypeScript, shadcn/ui, Vite | Modern, accessible, responsive; shadcn/ui for pre-built components |
| **Messaging** | AsyncBus (RabbitMQ, AMQP.NET Lite) | Choreography-friendly; RabbitMQ for HA; built-in DLQ/retry handling |
| **Database** | Azure Database for PostgreSQL Flexible Server, VNet-integrated | ACID guarantees; managed backups/patching; Dapper for data access |
| **Deployment** | Kubernetes (AKS), Helm, CI-driven `helm upgrade` | Native K8s for production readiness; Helm for templating; ArgoCD/GitOps deferred until a second environment justifies it (see `openspec/changes/aks-secure-platform-deployment/design.md`) |
| **Observability** | Prometheus + Grafana, Jaeger, ELK/Seq | Standard K8s monitoring; Jaeger for distributed tracing |

---

## Key Design Decisions

**Decision: Event-triggered alerts (not time-based monitoring)**
- **Why**: Renewal dates create a natural ROI moment ("protect your insurance renewal"); lower churn; better B2B positioning
- **Trade-off**: Requires accurate renewal date entry (mitigation: calendar tool with public feed for validation)

**Decision: Insurance-only for v0.1 (multi-category in v1.1)**
- **Why**: Reduces scraper fragility, Claude hallucination risk, and complexity; allows fast iteration
- **Trade-off**: Limits TAM initially; multi-category pivot requires new scrapers and categorization logic

**Decision: Monorepo with Kubernetes**
- **Why**: Single change root; easier refactoring across services; K8s is industry standard for production
- **Trade-off**: Steeper setup (mitigated by Helm charts and local docker-compose)

**Decision: Flint pattern for .NET backend**
- **Why**: Convention-based startup reduces boilerplate; aligns with PrePass.Core.Startup patterns
- **Trade-off**: Team must be familiar with Flint; less flexible than explicit DI config

**Decision: AsyncBus choreography (not orchestration)**
- **Why**: No single point of failure; easier to add new consumers; simpler than Saga pattern
- **Trade-off**: Event ordering and consistency harder to reason about; requires careful event design

---

## Repo Structure & Navigation

```
nomadrules/
├── .claude/
│   ├── CLAUDE.md (you are here)
│   ├── AGENTS.md (team/agent ownership)
│   ├── ARCHITECTURE.md (5-level progressive disclosure)
│   ├── decisions.md (decision log + risk register)
│   └── memory/ (persistent memory for future sessions)
├── docs/
│   ├── 00-assumptions-and-risks.md (public: assumptions, risks, validation signals)
│   ├── 01-business-concept.md
│   ├── 02-system-architecture.md
│   ├── 03-data-sources.md
│   ├── 04-tech-stack.md
│   ├── 05-subscription-model.md
│   └── 06-mvp-plan.md
├── openspec/
│   └── changes/texas-renewal-radar/
│       ├── proposal.md
│       ├── design.md
│       ├── tasks.md
│       └── specs/ (7 detailed specifications)
├── src/
│   ├── crawler/ (TypeScript)
│   ├── api/ (C# ASP.NET Core)
│   ├── summarizer/ (C# Azure Function or service)
│   ├── email-service/ (C# async handler)
│   ├── portal/ (React + TypeScript)
│   └── shared/ (shared types, event envelopes, async bus client)
├── infra/
│   ├── helm/ (Kubernetes Helm charts)
│   ├── terraform/ (optional: K8s cluster provisioning)
│   └── docker-compose.yml (local development)
├── .github/workflows/ (CI/CD pipelines)
├── README.md
└── CLAUDE.md
```

**Where to Look For**:
- Business context → docs/01-business-concept.md + /.claude/decisions.md
- Assumptions & risks → docs/00-assumptions-and-risks.md
- Architecture decisions → /.claude/ARCHITECTURE.md (Levels 1-4)
- Implementation details → openspec/changes/texas-renewal-radar/
- Agent/team ownership → /.claude/AGENTS.md
- Service setup → src/[service]/README.md (to be created)
- Deployment → infra/helm/README.md

---

## Success Criteria (MVP)

**Week 1**: Renewal calendar public and collecting emails (50+)
**Week 2**: Crawler finding changes, first summaries processed; API accepting subscribers
**Week 3**: Renewal alerts triggering for calendar users; first paid conversion
**Month 1**: 100+ free users, 5+ paid subscribers, <1% error rate in summarization

---

## Running the System Locally

See **docs/LOCAL_DEVELOPMENT.md** for the full runbook:
- Postgres via `infra/docker-compose.yml` + the DbUp migration runner
- Running each service as a process (api, ingest, summarizer, email-delivery)
- Crawler → local-file queue → ingest wiring (no local broker; Service Bus in prod)
- Portal dev server and its Entra config

---

## Critical Links & Contacts

| Resource | Owner | Purpose |
|----------|-------|---------|
| NomadRules repo | Ryan (architecture, backend, DevOps) | Single source of truth |
| Decisions log | Team | Validation signals, pivot triggers |
| Stripe API keys | Ryan | Billing; stored in Key Vault |
| Resend API key | Ryan | Email delivery |
| Claude API key | Ryan | Summarization; cost monitoring |
| Frontend/copy | Jenn | Portal UI, email templates, community messaging |
| Crawler sources | Both | Data sources list in docs/03-data-sources.md |

---

## Validation & Pivots

**Load-bearing assumptions**:
1. Claude can reliably summarize insurance law changes without hallucination
2. Renewal dates from calendar tool are accurate enough for 60/30/7-day triggering
3. RVers have $9–$25/month willingness-to-pay
4. Scrapers won't break frequently (monitored via alerts)

**Pivot triggers**:
- Claude hallucination rate >5% → switch to hybrid (Claude + human review) or GPT-4
- Renewal date accuracy <80% → implement auto-population via data partnerships
- Churn >10% by month 2 → pivot to time-based monitoring or increase B2B focus
- Scraper failure rate >10% per week → re-prioritize source resilience or reduce source count

See /.claude/decisions.md for full risk register and validation roadmap.

---

## Quick Start for New Team Members

1. Read /.claude/CLAUDE.md (this file) — system identity & structure
2. Read /.claude/AGENTS.md — who owns what
3. Read /.claude/ARCHITECTURE.md — how it all fits together
4. Read openspec/changes/texas-renewal-radar/proposal.md — what we're building
5. Read docs/LOCAL_DEVELOPMENT.md — how to run it locally
6. Pick a service from /.claude/AGENTS.md and read src/[service]/README.md

---

## Questions? Gaps? Decisions Needed?

Check /.claude/ARCHITECTURE.md for "Open questions:" section. If something is unclear or a decision needs to be made, add it there and ping the team.
