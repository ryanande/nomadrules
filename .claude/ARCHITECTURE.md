---
title: NomadRules System Architecture
summary: Comprehensive architecture for Texas Renewal Radar MVP - progressive disclosure from vision to implementation
tags: [system, architecture, kubernetes, microservices, event-driven, vertical-slice]
status: active
---

# NomadRules System Architecture

**Status:** In design (MVP phase)  
**Version:** 0.1 (Texas Renewal Radar MVP)  
**Last updated:** 2026-05-09

> Progressive disclosure: Start at **System Identity** for the why. Move to **Services** for the what. Go to **Deployment** for the how. **Implementation** has the details.

---

## Level 0: System Identity

### What

NomadRules monitors regulatory law changes across multiple domains (insurance, tax, DMV, voting) in a user's domicile state and delivers personalized alerts **tied to their renewal calendar** — not random "here's what changed this week," but "Your insurance renews in 60 days, here's what changed since last year."

### Why

Full-time RVers and digital nomads set domicile law once, then never revisit it. Laws change constantly. Missing a change at renewal time = surprise liability, invalid registration, or lapsed coverage. **The market need is acute and under-served.**

### Who

- **Primary:** Full-time RVers, digital nomads, remote workers (domiciled in US)
- **Secondary:** RV attorneys, mail forwarding services, domicile tax preparers
- **Initial focus:** Texas (founder domicile, strong community distribution)

### Value Prop

> "We watch the laws so you don't have to — and tell you what matters at the moment you need to know."

**For consumers:** Peace of mind + actionable alerts at renewal time.  
**For B2B:** White-label regulatory feed to power client retention.

---

## Level 1: System Architecture — High Level

```
┌─────────────────────────────────────────────────────────────────┐
│                    EXTERNAL SOURCES                             │
│  (TX Division of Insurance, TX Comptroller, TX DMV, IRS, etc)   │
└────────────────────┬────────────────────────────────────────────┘
                     │ (Scrape / Poll)
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│               CRAWLER SERVICE (TypeScript/Node)                 │
│  • Playwright scrapers per source                              │
│  • Diff detection (cosmetic noise filtered)                    │
│  • Publishes law_change_detected events → AsyncBus            │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼ Event: law_change_detected { raw_content, source, url, timestamp }
┌─────────────────────────────────────────────────────────────────┐
│              ASYNC MESSAGE BUS (Cloud Events)                  │
│  (Azure Service Bus, RabbitMQ, or Kafka depending on K8s)      │
│  • Decoupled communication                                     │
│  • Choreography-based (no central orchestrator)               │
│  • Dead letter handling for failures                           │
└────────────────────┬────────────────────────────────────────────┘
         ┌───────────┼───────────┬───────────┐
         │           │           │           │
         ▼           ▼           ▼           ▼
    ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐
    │Summarizer│  │Scorer  │  │Tagger  │  │Archive │
    │(Claude)  │  │        │  │        │  │Service │
    │ .NET     │  │ .NET   │  │ .NET   │  │ .NET   │
    └────┬─────┘  └────┬───┘  └────┬───┘  └────┬───┘
         │             │           │           │
         │  Events: law_change_summarized, law_change_scored, law_change_tagged
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│            SUBSCRIBER API (ASP.NET Core, Flint pattern)         │
│  • User registration, profile management                       │
│  • Law change feed endpoints                                   │
│  • Stripe webhook handling                                     │
│  • Renewal calendar matching                                   │
└────────────────────┬────────────────────────────────────────────┘
         ┌───────────┤
         │           │
         ▼           ▼
    ┌─────────┐  ┌────────────┐
    │Database │  │Email       │
    │(PostgreSQL)│Delivery Svc│
    │(K8s persistent)│(Resend)|
    └─────────┘  └────────────┘
         ▲
         │ (Queries, writes)
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│           PORTAL (React + TypeScript, shadcn/ui)                │
│  • Public feed, calendar signup                                │
│  • Authenticated dashboard, archive, profile                   │
│  • Stripe payment integration                                  │
│  • Runs on Azure Static Web Apps OR Nginx in K8s             │
└─────────────────────────────────────────────────────────────────┘
```

### Design Principles Applied

1. **Business Needs First** — Alert design (renewal-triggered) solves real user pain, not arbitrary tech choice
2. **Choreography over Orchestration** — Services react to events independently (Summarizer, Scorer, Tagger run in parallel)
3. **Async over Sync** — Crawler → events → independent processing. No request/response chains.
4. **Consumer-Driven Contracts** — Subscribers consume events; Summarizer publishes; contracts are versioned in code
5. **Autonomous Services** — Each service (Crawler, Summarizer, API) can deploy independently
6. **Vertical Slices** — Each service is self-contained (code + infra + tests together)
7. **Observable Services** — All services emit logs, metrics, traces (OpenTelemetry)
8. **Pragmatism** — K8s is professional and scalable, but we start simple (no service mesh initially)

---

## Level 2: Services — Identity, Contracts, Boundaries

### Service 1: Law Change Crawler

**Type:** Data ingestion service  
**Language:** TypeScript / Node.js  
**Owned by:** Ryan (crawlers + pipeline)  
**Bounded context:** "Law Source Monitoring"

**Responsibility:**
- Monitor TX Division of Insurance (+ tax, DMV, voting in v0.2+)
- Detect meaningful diffs (ignore cosmetic changes)
- Publish `law_change_detected` events

**Contract (Consumer-Driven):**
```typescript
// Event: law_change_detected
// Topic: nomadrules.law-changes.detected
interface LawChangeDetected {
  change_id: string;         // UUID
  source_id: string;         // "tx-insurance-bulletins"
  source_name: string;       // "TX Division of Insurance"
  source_url: string;        // https://dlr.sd.gov/...
  state: "TX" | "SD" | "FL";
  category: "insurance" | "tax" | "dmv" | "voting";
  raw_content: string;       // HTML or text of the change
  detected_at: ISO8601;
  content_hash: string;      // For idempotency
}
```

**Deployment:** K8s CronJob (daily at 2 AM UTC)  
**Monitoring:** Error on scrape failure, metrics on changes detected

---

### Service 2: Law Change Summarizer

**Type:** Processing service (enrichment)  
**Language:** C# / .NET (Azure Function or K8s Worker)  
**Owned by:** Ryan (Claude integration + quality gates)  
**Bounded context:** "Change Interpretation"

**Responsibility:**
- Consume `law_change_detected` events
- Call Claude API to summarize raw content
- Publish `law_change_summarized` event
- Implement quality gates (manual review of first 10)

**Contract:**
```typescript
// Event: law_change_summarized
// Topic: nomadrules.law-changes.summarized
interface LawChangeSummarized {
  change_id: string;
  headline: string;          // One sentence
  summary: string;           // 2-3 sentences, plain English
  severity: "urgent" | "routine" | "informational";
  affected_segment: string;  // "all_rvers" | "motorhome_owners" | etc
  summarized_at: ISO8601;
  quality_reviewed: boolean; // v0.1: manual gate; v0.2: auto-trusted
}
```

**Error handling:**
- Retries on Claude timeout (exponential backoff)
- Falls back to raw content if summarization fails
- Dead-letter queue for persistent failures

**Deployment:** K8s Deployment (polling AsyncBus, scales horizontally)

---

### Service 3: Change Scorer & Tagger

**Type:** Processing service (enrichment)  
**Language:** C# / .NET  
**Owned by:** Ryan (scoring logic + taxonomy)  
**Bounded context:** "Change Metadata"

**Responsibility:**
- Consume `law_change_summarized` events
- Score urgency (urgent → immediate send; routine → digest; informational → archive)
- Extract tags (affected states, subscriber segments, renewal type)
- Publish `law_change_scored` and `law_change_tagged` events

**Contracts:**
```typescript
// Event: law_change_scored
interface LawChangeScored {
  change_id: string;
  severity: "urgent" | "routine" | "informational";
  urgency_reasons: string[];  // Why urgent? ["affects_insurance_minimums", "registration_deadline"]
}

// Event: law_change_tagged
interface LawChangeTagged {
  change_id: string;
  tags: string[];             // ["insurance", "TX", "motorhome_owners"]
  affected_states: string[];  // ["TX", "SD"]
}
```

**Deployment:** K8s Deployment (can be same pod as Summarizer for v0.1, split later)

---

### Service 4: Subscriber API (Experience Service)

**Type:** API service (synchronous, Flint pattern)  
**Language:** C# / .NET / ASP.NET Core 9  
**Owned by:** Ryan (features, API contracts)  
**Bounded context:** "Subscriber Management"

**Responsibility:**
- Register subscribers (email, renewal dates, preferences)
- Serve law change feed (filtered by state + preferences)
- Manage renewal calendar (when do their renewals occur?)
- Handle Stripe webhooks (subscription lifecycle)
- Serve portal backend

**Vertical Slices (Features):**
```
Features/
  ├── SubscriberRegistration/
  │   ├── RegisterSubscriberCommand.cs
  │   ├── SendMagicLinkCommand.cs
  │   ├── VerifyMagicLinkQuery.cs
  │   └── SubscriberRegistrationController.cs
  ├── SubscriberProfile/
  │   ├── GetProfileQuery.cs
  │   ├── UpdateProfileCommand.cs
  │   └── SubscriberProfileController.cs
  ├── LawChangeFeed/
  │   ├── GetFeedQuery.cs (filters by state, categories, renewal date)
  │   ├── SearchChangesQuery.cs
  │   └── FeedController.cs
  ├── RenewalAlerts/
  │   ├── GetUpcomingRenewalsQuery.cs
  │   ├── SendRenewalAlertCommand.cs (triggered by scheduled job)
  │   └── RenewalAlertsController.cs
  └── BillingIntegration/
      ├── HandleStripeWebhookCommand.cs
      ├── GetBillingStatusQuery.cs
      └── BillingController.cs
```

**Contracts:**
```
GET    /api/subscribers/{id}/profile          → SubscriberProfile
PUT    /api/subscribers/{id}/profile          → SubscriberProfile
GET    /api/law-changes?state=TX              → LawChangeFeed[]
GET    /api/law-changes/search?q=insurance    → LawChangeFeed[]
GET    /api/renewals/upcoming                 → UpcomingRenewal[]
POST   /webhooks/stripe                       → StripeWebhook handling
```

**Database schema:**
```sql
subscribers (id, email, state, renewal_dates_json, tier, stripe_customer_id, created_at)
law_changes (id, source_id, headline, summary, severity, tags_json, created_at)
notifications (id, subscriber_id, change_id, sent_at, delivery_type)
```

**Deployment:** K8s Deployment (ASP.NET Core, replicas for HA)  
**Communication:** Consumes events from AsyncBus (for renewal alert triggers)

---

### Service 5: Email Delivery Service

**Type:** Delivery service (async handler)  
**Language:** C# / .NET  
**Owned by:** Ryan (templates) + Jenn (voice/copy)  
**Bounded context:** "Notification Delivery"

**Responsibility:**
- Listen for `renewal_alert_ready` events
- Listen for `digest_ready` events
- Render email (React template → HTML)
- Send via Resend API
- Track delivery status

**Events consumed:**
```typescript
interface RenewalAlertReady {
  subscriber_id: string;
  changes: LawChangeSummarized[];
  days_until_renewal: number;  // 60 | 30 | 7
}

interface DigestReady {
  subscriber_id: string;
  changes: LawChangeSummarized[];
  period: "weekly";
}
```

**Deployment:** K8s Deployment (event-driven, scales with queue depth)

---

### Service 6: Portal (React + TypeScript)

**Type:** Frontend application  
**Language:** TypeScript / React 19 + shadcn/ui + Tailwind CSS  
**Owned by:** Ryan + Jenn (implementation + voice)  
**Bounded context:** "Subscriber Experience"

**Responsibility:**
- Public law change feed (no auth required)
- Renewal calendar signup form (captures email + dates)
- Authenticated dashboard (shows personalized changes)
- Law change archive with search
- Profile management + billing
- Magic link auth flow

**Components:**
```
src/portal/
  ├── pages/
  │   ├── public/
  │   │   ├── feed.tsx          (public law changes)
  │   │   ├── calendar.tsx       (signup form)
  │   │   └── landing.tsx
  │   └── app/
  │       ├── dashboard.tsx      (authenticated)
  │       ├── archive.tsx        (search + filter)
  │       ├── profile.tsx
  │       └── billing.tsx
  ├── components/
  │   ├── LawChangeCard.tsx
  │   ├── RenewalCountdown.tsx
  │   └── (shadcn/ui primitives)
  ├── hooks/
  │   ├── useSubscriber.ts
  │   └── useLawChanges.ts
  └── lib/
      ├── api-client.ts
      └── auth.ts (magic link)
```

**API dependency:** Calls Subscriber API (GET /api/law-changes, POST /api/subscribers, etc.)  
**Deployment:** K8s Deployment (Nginx) or Azure Static Web Apps (initially)

---

## Level 3: Deployment Architecture — Kubernetes, Helm, ArgoCD

### Cluster Setup

```
┌────────────────────────────────────────────────────────────────┐
│                  Kubernetes Cluster (On-Prem or Cloud)          │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    Ingress Controller (Nginx)            │  │
│  │  Routes:                                                 │  │
│  │    nomadrules.com          → Portal Service             │  │
│  │    api.nomadrules.com      → Subscriber API Service     │  │
│  └─────────────────┬──────────────────────────────────────┘  │
│                    │                                            │
│  ┌─────────────────┼──────────────────────────────────────┐  │
│  │                 │   nomadrules-services Namespace      │  │
│  │                 │                                       │  │
│  │  Portal (React)         Subscriber API (ASP.NET)      │  │
│  │  ├─ Replicas: 2         ├─ Replicas: 2               │  │
│  │  ├─ Resources:          ├─ Resources:                 │  │
│  │  │  CPU: 256m           │  CPU: 512m                  │  │
│  │  │  Memory: 256Mi        │  Memory: 512Mi              │  │
│  │  └─ Readiness probe      └─ Liveness probe            │  │
│  │                                                       │  │
│  │  Crawler (TypeScript)   Summarizer (C#/.NET)         │  │
│  │  ├─ Type: CronJob       ├─ Type: Deployment           │  │
│  │  │  (daily 2 AM UTC)    ├─ Replicas: 2                │  │
│  │  │                      └─ Auto-scales on queue depth │  │
│  │  │                                                     │  │
│  │  Email Service                                        │  │
│  │  ├─ Type: Deployment                                 │  │
│  │  ├─ Replicas: 1                                      │  │
│  │  └─ Event-driven (AsyncBus consumer)                │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              nomadrules-data Namespace                    │  │
│  │                                                            │  │
│  │  PostgreSQL (StatefulSet)    RabbitMQ (StatefulSet)     │  │
│  │  ├─ PVC: 20Gi               ├─ PVC: 10Gi                │  │
│  │  ├─ Replicas: 1             ├─ Replicas: 1              │  │
│  │  └─ Backup sidecar          └─ Persistent queues        │  │
│  │                                                            │  │
│  │  Redis (for caching, optional)                           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │         Observability (Prometheus + Grafana + Jaeger)    │  │
│  │         ├─ Prometheus scrapes /metrics endpoints        │  │
│  │         ├─ Grafana dashboards                           │  │
│  │         └─ Jaeger for distributed tracing               │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │     ArgoCD (GitOps deployment automation)               │  │
│  │     ├─ Watches this repo for changes                    │  │
│  │     ├─ Applies Helm charts on git push                 │  │
│  │     └─ Syncs cluster state to git state                │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└────────────────────────────────────────────────────────────────┘
```

### Helm Charts (IaC)

```
infra/helm/
  ├── Chart.yaml
  ├── values.yaml
  ├── values-dev.yaml
  ├── values-prod.yaml
  └── templates/
      ├── namespace.yaml
      ├── services/
      │   ├── portal-deployment.yaml
      │   ├── subscriber-api-deployment.yaml
      │   ├── crawler-cronjob.yaml
      │   ├── summarizer-deployment.yaml
      │   └── email-service-deployment.yaml
      ├── data/
      │   ├── postgres-statefulset.yaml
      │   ├── rabbitmq-statefulset.yaml
      │   └── persistent-volumes.yaml
      ├── ingress.yaml
      ├── secrets.yaml (sealed-secrets for prod)
      ├── configmaps.yaml
      ├── observability/
      │   ├── prometheus-deployment.yaml
      │   └── grafana-deployment.yaml
      └── argocd/
          └── application.yaml (argocd application config)
```

### Terraform (Cluster Provisioning — Optional, depends on cloud)

```
infra/terraform/
  ├── main.tf                (cluster provisioning)
  ├── variables.tf           (cluster config)
  ├── outputs.tf
  ├── k8s-namespaces.tf      (namespaces + RBAC)
  └── addons.tf              (ingress controller, storage class, etc)
```

### Deployment Flow

```
Developer pushes code → GitHub Actions CI
  ├─ Run tests (unit, integration, contract)
  ├─ Build Docker images (tag with commit SHA)
  ├─ Push images to registry
  └─ Update Helm values (image tags)

Git push to infra/helm/ or src/*/Dockerfile → ArgoCD detects
  ├─ Reads updated values
  ├─ Renders Helm templates
  ├─ Applies to K8s cluster
  └─ Reconciles to match git state

Cluster updates:
  ├─ Scheduler pulls new images
  ├─ Starts new pods
  ├─ Old pods drain gracefully
  └─ Health checks verify readiness
```

---

## Level 4: Implementation Details

### Monorepo Structure

```
nomadrules/
├── .github/
│   └── workflows/
│       ├── ci.yml              (test all services)
│       ├── build-and-push.yml  (build Docker images)
│       └── deploy.yml          (trigger ArgoCD sync)
│
├── src/
│   ├── crawler/                (TypeScript/Node)
│   │   ├── src/
│   │   │   ├── scrapers/
│   │   │   │   ├── tx-insurance.ts
│   │   │   │   ├── tx-comptroller.ts
│   │   │   │   └── tx-dmv.ts
│   │   │   ├── diff-engine.ts
│   │   │   ├── event-publisher.ts
│   │   │   └── index.ts (entry point)
│   │   ├── Dockerfile
│   │   ├── package.json
│   │   └── tests/
│   │
│   ├── api/                   (ASP.NET Core, Flint pattern)
│   │   ├── Experience.NomadRules.Api/
│   │   │   ├── Program.cs
│   │   │   ├── Features/
│   │   │   │   ├── SubscriberRegistration/
│   │   │   │   ├── SubscriberProfile/
│   │   │   │   ├── LawChangeFeed/
│   │   │   │   ├── RenewalAlerts/
│   │   │   │   └── BillingIntegration/
│   │   │   ├── Infrastructure/
│   │   │   │   ├── Persistence/
│   │   │   │   │   ├── AppDbContext.cs
│   │   │   │   │   └── Migrations/
│   │   │   │   ├── Services/
│   │   │   │   │   ├── MagicLinkService.cs
│   │   │   │   │   ├── StripeService.cs
│   │   │   │   │   └── EventPublisher.cs
│   │   │   │   └── Configuration/
│   │   │   └── Dockerfile
│   │   ├── Experience.NomadRules.Contracts/
│   │   │   ├── Subscribers/
│   │   │   ├── LawChanges/
│   │   │   └── Events/
│   │   ├── tests/
│   │   │   ├── UnitTests/
│   │   │   ├── IntegrationTests/
│   │   │   └── ContractTests/
│   │   ├── NomadRules.sln
│   │   └── .csproj files
│   │
│   ├── summarizer/             (C#/.NET Azure Function or Worker)
│   │   ├── NomadRules.Summarizer/
│   │   │   ├── LawChangeSummarizer.cs (handler)
│   │   │   ├── ClaudeClient.cs
│   │   │   ├── QualityGate.cs
│   │   │   ├── Prompts/
│   │   │   │   └── SummarizeInsuranceChange.txt
│   │   │   └── Dockerfile
│   │   └── tests/
│   │
│   ├── email-service/          (C#/.NET Worker)
│   │   ├── NomadRules.EmailService/
│   │   │   ├── EmailDeliveryHandler.cs
│   │   │   ├── Templates/
│   │   │   │   ├── RenewalAlertEmail.tsx
│   │   │   │   └── DigestEmail.tsx
│   │   │   ├── ResendClient.cs
│   │   │   └── Dockerfile
│   │   └── tests/
│   │
│   └── portal/                 (React + TypeScript)
│       ├── src/
│       │   ├── pages/
│       │   │   ├── public/
│       │   │   │   ├── feed.tsx
│       │   │   │   ├── calendar.tsx
│       │   │   │   └── landing.tsx
│       │   │   └── app/
│       │   │       ├── dashboard.tsx
│       │   │       ├── archive.tsx
│       │   │       ├── profile.tsx
│       │   │       └── billing.tsx
│       │   ├── components/
│       │   ├── hooks/
│       │   └── lib/
│       ├── Dockerfile
│       ├── vite.config.ts
│       ├── package.json
│       └── tests/
│
├── infra/
│   ├── helm/                  (K8s deployment)
│   │   ├── Chart.yaml
│   │   ├── values.yaml
│   │   ├── values-dev.yaml
│   │   ├── values-prod.yaml
│   │   └── templates/
│   │
│   ├── terraform/             (Cluster provisioning, optional)
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   │
│   └── scripts/
│       ├── setup-cluster.sh
│       └── install-argocd.sh
│
├── .claude/
│   ├── CLAUDE.md              (System passport)
│   ├── AGENTS.md              (Agent responsibilities)
│   ├── ARCHITECTURE.md        (This file)
│   ├── decisions.md           (Architecture decisions + validation)
│   └── skills/                (AI skills for the project)
│
├── docs/
│   ├── SYSTEM_ARCHITECTURE.md (Mirrors ARCHITECTURE.md for web)
│   ├── SERVICE_CONTRACTS.md   (PactNet contracts)
│   ├── DEPLOYMENT.md          (K8s + Helm guide)
│   ├── DEVELOPMENT.md         (Local dev setup)
│   ├── API.md                 (API reference)
│   ├── CONTRIBUTING.md
│   └── STYLE_GUIDE.md
│
├── openspec/
│   └── changes/
│       └── texas-renewal-radar/
│           ├── proposal.md
│           ├── design.md
│           ├── specs/
│           └── tasks.md
│
├── Dockerfile                 (multi-stage, shared patterns)
├── docker-compose.yml         (local dev orchestration)
├── CLAUDE.md                  (system passport)
├── AGENTS.md                  (agents)
├── README.md
├── .gitignore
└── .editorconfig
```

---

## Level 5: Progressive Disclosure — How Docs Unfold

**For different audiences:**

| Audience | Start here | Then | Then | Deep dive |
|----------|-----------|------|------|-----------|
| **Executive/Product** | System Identity (this doc) | Level 1 arch diagram | Business value prop | (skip) |
| **New Engineer** | CLAUDE.md → AGENTS.md → Level 2 Services | Service you own | Deployment (Level 3) | Implementation details (Level 4) |
| **DevOps/SRE** | Level 3 Deployment | Helm templates | Terraform | Observability setup |
| **AI Agent** | CLAUDE.md (concise) | AGENTS.md (responsibilities) | Level 2 Services (contracts) | Implementation (code) |

**Documents layering:**
- **CLAUDE.md** (~ 500 words) — Identity, bounded context, key contracts, navigation
- **AGENTS.md** — Which agent owns which services/concerns
- **ARCHITECTURE.md** (this file) — Levels 0-5, unfolding from vision to implementation
- **Service-specific docs** — Service X: Features, Contracts, Configuration
- **Code comments** — Only "why," not "what"

---

## Alignment with Architectural Principles

| Principle | How Implemented |
|-----------|-----------------|
| **Business Needs First** | Renewal-triggered alerts solve real pain (not random tech choice) |
| **Solution over Trends** | K8s is industry-standard for scalability, not chosen for hype |
| **Well-Architected Framework** | Observable services, autonomous components, phased modernization |
| **Choreography over Orchestration** | Event-driven (AsyncBus), no central orchestrator; services react independently |
| **Async over Sync** | Crawler → events → Summarizer/Scorer run async; only API is sync request/response |
| **Consumer-Driven Contracts** | Events are versioned in code; services define what they consume/produce |
| **Autonomous Services** | Each service (Crawler, API, Email) can deploy independently |
| **Coarser Services before Granular** | 5 services for MVP; can split Summarizer + Scorer later if needed |
| **Make Old Depend on New** | Portal v0.1 depends on API v0.1; API can evolve without portal changes (backwards-compatible contracts) |
| **Vertical Slices** | API organized by feature (SubscriberRegistration, LawChangeFeed, etc), not technical layers |
| **Build Like the Business Works** | Renewal calendar mirrors how RVers actually think about law changes |
| **Observable Services** | All services emit logs → Loki, metrics → Prometheus, traces → Jaeger |
| **Pragmatism** | Start with simple messaging (RabbitMQ), upgrade to async only when needed; no service mesh yet |

---

## Open Questions / Decisions to Make

1. **AsyncBus choice** — RabbitMQ (simple, stateful), Azure Service Bus (managed), or Kafka (distributed)?
   - *Recommendation:* RabbitMQ in K8s for MVP (simple, single StatefulSet). Migrate to Service Bus if Azure-native.

2. **Database** — PostgreSQL (open-source, portable) or Azure SQL (managed)?
   - *Recommendation:* PostgreSQL in K8s (helm-postgres chart) for v0.1. Migrate to managed DB at scale.

3. **Email rendering** — Server-side React → HTML, or template strings?
   - *Recommendation:* React templates (TSX → Mjml → HTML). Simple and version-controlled.

4. **Secrets management** — Sealed Secrets, External Secrets, or plain K8s Secrets?
   - *Recommendation:* Sealed Secrets (simple, git-friendly). Use External Secrets if you add Vault later.

5. **Logging/Tracing** — Full OpenTelemetry, or lighter approach?
   - *Recommendation:* OpenTelemetry SDK in each service (instrumentation libraries do the heavy lifting). Start with Jaeger for traces, Loki for logs.

6. **Image registry** — DockerHub, GitHub Container Registry, or private registry?
   - *Recommendation:* GitHub Container Registry (free, integrated with repo).

---

## Success Criteria (MVP)

- **Week 2:** All services build locally (`docker-compose up`), tests pass
- **Week 3:** Helm charts deploy to local K8s (via minikube or Docker Desktop K8s)
- **Week 4:** ArgoCD syncs from git; Jenn publishes; first users sign up
- **Month 1:** Crawler runs daily, Summarizer processes (manual QA), Emails deliver, Portal shows feed

---

## Next Steps

1. **CLAUDE.md** — Concise system passport (← start here for agents)
2. **AGENTS.md** — Define which agent owns each service
3. **Detailed service docs** — One per service (features, contracts, setup)
4. **OpenSpec mapping** — Break this architecture into implementation tasks (proposal → design → specs → tasks)
5. **Helm chart scaffolding** — Bootstrap templates, ready to populate
6. **GitHub Actions workflows** — CI/CD pipeline for build → test → push → deploy

