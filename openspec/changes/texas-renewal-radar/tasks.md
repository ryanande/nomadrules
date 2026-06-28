# Implementation Tasks — Texas Renewal Radar MVP

**Timeline:** 4 weeks to MVP (crawler → summarizer → API → portal → delivery)  
**Owner assignments:** Ryan (engineering, infrastructure) | Jenn (portal UI/UX, email templates, copy/community)  
**Architecture:** Kubernetes + Helm + Monorepo (TypeScript crawlers, C# backend, React portal)  
**References:** /.claude/ARCHITECTURE.md, /.claude/AGENTS.md, openspec/changes/texas-renewal-radar/design.md

---

## Phase 1: Monorepo Foundation & Infrastructure (Week 1)

### 1.1 Repository & Monorepo Setup

- [ ] 1.1.1 Initialize GitHub repository with main branch protection (code review required)
- [ ] 1.1.2 Create monorepo directory structure:
  - [ ] src/crawler (TypeScript)
  - [ ] src/api (C# ASP.NET Core)
  - [ ] src/summarizer (C#)
  - [ ] src/email-service (C#)
  - [ ] src/portal (React/TypeScript)
  - [ ] src/shared (shared types, AsyncBus client, event envelopes)
  - [ ] infra/helm (Helm charts for each service)
  - [ ] infra/terraform (K8s provisioning — optional)
  - [ ] infra/docker-compose.yml (local development)
  - [ ] .github/workflows (CI/CD)
  - [ ] docs/, openspec/ (documentation)
- [ ] 1.1.3 Create .gitignore (node_modules, bin/, obj/, .env, build artifacts)
- [ ] 1.1.4 Create .env.example with all required secrets (Claude API key, Stripe key, Resend key, RabbitMQ connection, PostgreSQL connection, etc.)
- [ ] 1.1.5 Create README.md with quick-start instructions and service overview

### 1.2 Kubernetes & Infrastructure Setup

- [ ] 1.2.1 Provision K8s cluster (local: minikube/kind; staging: Azure AKS or GKE)
- [ ] 1.2.2 Create two K8s namespaces:
  - [ ] nomadrules-services (Crawler, Summarizer, API, Email Service, Portal)
  - [ ] nomadrules-data (PostgreSQL, RabbitMQ, optional Redis)
- [ ] 1.2.3 Set up PostgreSQL on K8s:
  - [ ] Create PersistentVolume and PersistentVolumeClaim for data
  - [ ] Deploy PostgreSQL via Helm or kubectl manifest
  - [ ] Create initial database schema (subscribers, law_changes, sources, notifications)
  - [ ] Backup/restore procedures
- [ ] 1.2.4 Set up RabbitMQ on K8s:
  - [ ] Create StatefulSet with PersistentVolume for queue persistence
  - [ ] Configure vhosts: `/nomadrules`
  - [ ] Create users and credentials
  - [ ] Set up dead-letter exchange for failed messages
- [ ] 1.2.5 Set up secrets management (Kubernetes Sealed Secrets):
  - [ ] Install Sealed Secrets operator
  - [ ] Create sealed secrets for: Claude API key, Stripe key, Resend key, DB connection, RabbitMQ credentials, Stripe webhook secret
  - [ ] Document secret rotation procedure
- [ ] 1.2.6 Install and configure observability stack:
  - [ ] Prometheus (metrics collection)
  - [ ] Grafana (dashboards)
  - [ ] Jaeger (distributed tracing)
  - [ ] ELK or Seq (centralized logging)
- [ ] 1.2.7 Set up ArgoCD for GitOps:
  - [ ] Install ArgoCD to K8s cluster
  - [ ] Create Application resource pointing to infra/helm/
  - [ ] Configure auto-sync on main branch
- [ ] 1.2.8 Create Terraform modules (optional, for repeatability):
  - [ ] K8s cluster provisioning
  - [ ] Namespace creation
  - [ ] Sealed Secrets operator

### 1.3 Shared Libraries & Event System

- [ ] 1.3.1 Create src/shared project (C# class library):
  - [ ] Event envelope types (EventType, EventId, Timestamp, CorrelationId, Payload)
  - [ ] AsyncBus client wrapper (AMQP.NET Lite or Mass Transit)
  - [ ] Event handler base classes and interfaces
  - [ ] Retry policies and middleware
  - [ ] Structured logging utilities
- [ ] 1.3.2 Define event schemas in code (TypeScript types + C# classes):
  - [ ] LawChangeDetected event (sourceId, state, category, rawText, url, detectedAt)
  - [ ] LawChangeSummarized event (changeId, plainEnglishSummary, headline, affectedSegments, qualityScore)
  - [ ] LawChangeScored event (changeId, severity, actionRequired, deadline)
  - [ ] LawChangeTagged event (changeId, states[], categories[], segments[])
  - [ ] SubscriberRegistered event (subscriberId, email, domicile, categories)
  - [ ] SubscriberProfileUpdated event (subscriberId, renewalDates, categoryPreferences)
  - [ ] DigestReady event (subscriberId, itemCount, renderTime)
  - [ ] RenewalAlertReady event (subscriberId, changeId, renewalDaysRemaining)
  - [ ] Document all event contracts in docs/EVENT_SCHEMA.md
- [ ] 1.3.3 Set up event versioning (v1 schema suffix, support for minor version changes)

### 1.4 Local Development Environment

- [ ] 1.4.1 Create docker-compose.yml with:
  - [ ] PostgreSQL (port 5432)
  - [ ] RabbitMQ (port 5672, management UI 15672)
  - [ ] Optional: Redis (for caching layer)
- [ ] 1.4.2 Create setup script (`infra/scripts/setup-local.sh`):
  - [ ] Spin up docker-compose services
  - [ ] Wait for health checks
  - [ ] Create initial database schema
  - [ ] Create RabbitMQ vhost and users
  - [ ] Output connection strings
- [ ] 1.4.3 Create docs/LOCAL_DEVELOPMENT.md:
  - [ ] Prerequisites (Node.js, .NET SDK, Docker)
  - [ ] Quick-start (`./infra/scripts/setup-local.sh`)
  - [ ] Running each service locally
  - [ ] Testing event flow end-to-end
  - [ ] Debugging tips (logs, RabbitMQ management UI)
- [ ] 1.4.4 Create development certificate setup (HTTPS for local testing)

### 1.5 CI/CD Pipeline Foundation

- [ ] 1.5.1 Create .github/workflows/ci.yml:
  - [ ] Trigger on: push to main, pull requests
  - [ ] Jobs:
    - [ ] Lint (prettier for TypeScript/React, stylecop for C#)
    - [ ] Build (crawler, API, portal, all services)
    - [ ] Unit tests (each service)
    - [ ] Build Docker images (tag with commit SHA)
    - [ ] Push to image registry (GitHub Container Registry)
- [ ] 1.5.2 Create .github/workflows/deploy.yml:
  - [ ] Trigger on: successful CI, manual workflow_dispatch
  - [ ] Generate Helm values for each service (image tags, replicas, resource limits)
  - [ ] Push Helm chart changes to ArgoCD (GitOps)
  - [ ] ArgoCD syncs automatically
- [ ] 1.5.3 Create .github/workflows/rollback.yml:
  - [ ] Manual trigger for emergency rollback
  - [ ] Revert Helm chart to previous stable version

**Success Criteria (Week 1 end):**
- ✅ K8s cluster deployed and all namespaces created
- ✅ PostgreSQL and RabbitMQ running and healthy
- ✅ Sealed Secrets configured with all required credentials
- ✅ ArgoCD installed and pointing to infra/helm/
- ✅ local docker-compose works (developers can `docker-compose up`)
- ✅ CI pipeline runs on all PRs (lint + build)

---

## Phase 2: Core Services — Crawler & API (Week 2)

### 2.1 Crawler Service (TypeScript/Node.js)

- [ ] 2.1.1 Create src/crawler/package.json:
  - [ ] Dependencies: Playwright, TypeScript, dotenv, @azure/storage-blob or similar, AMQP.NET Lite client (or amqplib)
  - [ ] DevDependencies: jest, ts-jest, @types/node
- [ ] 2.1.2 Create TypeScript configuration: tsconfig.json with strict mode
- [ ] 2.1.3 Create source scraper interface: src/crawler/src/types/ISourceScraper.ts
  - [ ] Methods: scrape(), parseContent(), detectDiff()
  - [ ] Returns: SourceChange (with rawText, url, detectedAt, sourceId)
- [ ] 2.1.4 Implement TX Division of Insurance scraper (src/crawler/src/sources/TXInsuranceScraper.ts):
  - [ ] Playwright script to fetch https://dlr.sd.gov/insurance/bulletins.aspx (PLACEHOLDER: update to actual TX URL)
  - [ ] Extract main content zone (ignore nav, footer, ads)
  - [ ] Hash full HTML content
  - [ ] Compare to previous snapshot
  - [ ] If hash changed, extract title + publication date + raw HTML
  - [ ] Publish LawChangeDetected event to RabbitMQ
- [ ] 2.1.5 Create snapshot storage (local filesystem for v0.1):
  - [ ] Store in src/crawler/snapshots/
  - [ ] One file per source per date
  - [ ] Format: {sourceId}-{date}.html
- [ ] 2.1.6 Implement diff detection:
  - [ ] Token-based diff (ignore date, whitespace-only changes)
  - [ ] Configurable noise threshold (e.g., >5% content change triggers alert)
  - [ ] Log false positives for manual review
- [ ] 2.1.7 Implement AsyncBus client integration:
  - [ ] Connect to RabbitMQ on startup
  - [ ] Publish LawChangeDetected events
  - [ ] Implement retry logic (exponential backoff)
  - [ ] Log all published events
- [ ] 2.1.8 Create crawler entry point: src/crawler/src/index.ts
  - [ ] Load all source scrapers (dynamic import from src/crawler/src/sources/)
  - [ ] Run each scraper sequentially
  - [ ] Publish events on changes
  - [ ] Log summary (sources checked, changes found, errors)
  - [ ] Exit with code 0 on success, 1 on critical error
- [ ] 2.1.9 Create Helm chart: infra/helm/crawler/
  - [ ] deployment.yaml (CronJob scheduling daily runs at 2 AM UTC)
  - [ ] service.yaml (optional, for inter-cluster communication)
  - [ ] configmap.yaml (scraper configurations, noise thresholds)
  - [ ] secrets.yaml (RabbitMQ credentials via sealed secrets)
  - [ ] values.yaml (replicaCount: 1, image: ghcr.io/you/nomadrules-crawler, tag: latest)
- [ ] 2.1.10 Create .github/workflows/crawler-build.yml:
  - [ ] Build and test TypeScript
  - [ ] Run unit tests (jest)
  - [ ] Lint with eslint/prettier
  - [ ] Build Docker image
- [ ] 2.1.11 Test crawler locally:
  - [ ] Run against real TX Insurance page (or mock)
  - [ ] Verify snapshot creation
  - [ ] Verify diff detection (make manual change, re-run, confirm detection)
  - [ ] Verify RabbitMQ event publishing (check with rabbitmq-admin)
- [ ] 2.1.12 Deploy crawler to K8s:
  - [ ] Push Docker image to GitHub Container Registry
  - [ ] Apply Helm chart: `helm install nomadrules-crawler infra/helm/crawler/ -n nomadrules-services`
  - [ ] Verify CronJob created: `kubectl get cronjobs -n nomadrules-services`
  - [ ] Check logs: `kubectl logs -n nomadrules-services -l app=crawler`

### 2.2 Subscriber API Service (C# ASP.NET Core)

- [ ] 2.2.1 Create src/api/.csproj with Flint pattern:
  - [ ] NuGet packages: PrePass.Core.Startup (Flint), Asp.Core.ServiceBus, Dapper (or EF Core), Stripe.net, Anthropic SDK
  - [ ] Reference src/shared project
- [ ] 2.2.2 Create Program.cs with Flint startup:
  - [ ] Configure DI via Flint conventions
  - [ ] Add middleware: logging, CORS, authentication
  - [ ] Register AsyncBus client
  - [ ] Register database connection (PostgreSQL)
- [ ] 2.2.3 Create vertical slices in src/api/Features/:
  - [ ] **SubscriberRegistration** (domain: signing up)
    - [ ] Handler: `POST /api/subscribers` → CreateSubscriberCommand
    - [ ] Validates email, creates record in DB
    - [ ] Publishes SubscriberRegistered event
    - [ ] Returns { subscriberId, email, confirmationEmailSent }
  - [ ] **Profile** (domain: managing preferences)
    - [ ] Handler: `GET /api/subscribers/{id}/profile` → RetrieveProfileQuery
    - [ ] Handler: `PUT /api/subscribers/{id}/profile` → UpdateProfileCommand
    - [ ] Fields: domicileState, renewalDates (insurance, registration, license, taxes), categoryPreferences
    - [ ] Publishes SubscriberProfileUpdated event on change
  - [ ] **Feed** (domain: law changes)
    - [ ] Handler: `GET /api/subscribers/{id}/feed?state=TX&limit=10&offset=0` → LawChangeFeedQuery
    - [ ] Returns paginated list of law changes matching subscriber's state/categories
    - [ ] Includes: headline, summary, severity, tags, url, detectedAt
  - [ ] **RenewalAlerts** (domain: upcoming renewals)
    - [ ] Handler: `GET /api/subscribers/{id}/renewals` → UpcomingRenewalsQuery
    - [ ] Returns: [{ type: 'insurance', renewalDate: '2026-06-15', daysRemaining: 37, applicableLawChanges: [...] }]
  - [ ] **Billing** (domain: subscriptions)
    - [ ] Handler: `POST /webhooks/stripe` → Stripe webhook processor
    - [ ] Events: customer.subscription.created, customer.subscription.updated, customer.subscription.deleted, invoice.payment_failed
    - [ ] Updates subscriber tier in DB
    - [ ] Logs all webhook events
- [ ] 2.2.4 Create data layer (src/api/Data/):
  - [ ] SubscribersDbContext (PostgreSQL)
  - [ ] Models: Subscriber, LawChange, Notification, Source, RenewalDate
  - [ ] Migrations (EF Core or Dapper migrations)
  - [ ] Connection pooling configuration
- [ ] 2.2.5 Implement authentication (magic links):
  - [ ] Handler: `POST /api/auth/send-magic-link` → { email }
  - [ ] Generate JWT token (valid 15 minutes)
  - [ ] Send email via Resend (template: "Click to verify: {link}")
  - [ ] Handler: `GET /api/auth/verify?token={jwt}`
  - [ ] Validate token, set httpOnly cookie
  - [ ] Redirect to dashboard
- [ ] 2.2.6 Add API documentation (Swagger/OpenAPI):
  - [ ] Configure Swashbuckle
  - [ ] Document all endpoints, request/response schemas
  - [ ] Enable Swagger UI at `/swagger`
- [ ] 2.2.7 Add CORS configuration (allow portal frontend)
- [ ] 2.2.8 Create Helm chart: infra/helm/api/
  - [ ] deployment.yaml (2 replicas for HA)
  - [ ] service.yaml (ClusterIP, port 80)
  - [ ] configmap.yaml (API settings: max page size, rate limits)
  - [ ] secrets.yaml (DB connection, Stripe webhook secret)
  - [ ] values.yaml (image, replicas, resource requests/limits)
- [ ] 2.2.9 Create src/api/README.md:
  - [ ] Service overview
  - [ ] Vertical slice layout explanation
  - [ ] How to add a new endpoint
  - [ ] Local development (connection string, running migrations)
- [ ] 2.2.10 Create unit tests: src/api.Tests/
  - [ ] Test each vertical slice command/query
  - [ ] Mock database and AsyncBus
  - [ ] Test Stripe webhook validation
- [ ] 2.2.11 Build and deploy:
  - [ ] Create Dockerfile: src/api/Dockerfile (multi-stage, runtime only)
  - [ ] Build Docker image locally: `docker build -f src/api/Dockerfile -t ghcr.io/you/nomadrules-api:latest .`
  - [ ] Push to GitHub Container Registry
  - [ ] Deploy to K8s: `helm install nomadrules-api infra/helm/api/ -n nomadrules-services`
  - [ ] Verify: `kubectl get pods -n nomadrules-services -l app=api`
  - [ ] Check logs: `kubectl logs -n nomadrules-services -l app=api`
  - [ ] Test endpoint: `curl http://localhost:8000/api/subscribers` (after port-forward if needed)

**Success Criteria (Week 2 end):**
- ✅ Crawler runs daily, detects changes, publishes events
- ✅ API starts up, connects to PostgreSQL, accepts requests
- ✅ Magic link flow works (send email → verify → authenticated)
- ✅ Stripe webhook endpoint validates and logs events
- ✅ Both services deployable via Helm

---

## Phase 3: AI Processing & Delivery (Week 2-3)

### 3.1 Summarizer Service (C#)

- [ ] 3.1.1 Create src/summarizer/.csproj:
  - [ ] NuGet packages: PrePass.Core.Startup, Anthropic SDK (or HttpClient), Dapper, Asp.Core.ServiceBus
  - [ ] Reference src/shared
- [ ] 3.1.2 Create async handler: src/summarizer/Handlers/LawChangeSummarizationHandler.cs
  - [ ] Consumes LawChangeDetected events from RabbitMQ
  - [ ] Calls Claude API via Summarization prompt (see 3.1.3)
  - [ ] Extracts: headline (1 line), summary (2-3 sentences), severity (urgent/routine/informational), affected segments
  - [ ] Stores summary in law_changes table: {headline, plainEnglishSummary, severity, processedAt}
  - [ ] Publishes LawChangeSummarized event
  - [ ] Error handling: retry exponential backoff; if fails 3x, move to dead-letter queue + alert
- [ ] 3.1.3 Create summarization prompt: src/summarizer/Prompts/InsuranceLawSummarizer.md
  - [ ] Prompt template (see design.md for details)
  - [ ] Specializes in insurance law, not tax/voting
  - [ ] Emphasizes plain English, actionable language
  - [ ] Example output format
- [ ] 3.1.4 Implement quality gates:
  - [ ] Check summary length (not >500 tokens)
  - [ ] Check for hallucination signals ("I don't have access to", "I cannot verify")
  - [ ] Flag first 10 summaries as reviewed=false for manual inspection
  - [ ] Metrics: track quality score per batch
- [ ] 3.1.5 Create src/summarizer/Services/ClaudeClient.cs:
  - [ ] HTTP client wrapper (timeout 30s, retries)
  - [ ] Cost tracking: log tokens used, cost per call
  - [ ] Rate limiting: queue requests, don't exceed API quotas
- [ ] 3.1.6 Implement Scorer & Tagger (inline with Summarizer for now):
  - [ ] Handler: LawChangeScoringHandler — scoring severity, extracting tags (states, categories, segments)
  - [ ] Rules-based or Claude-based (Claude for insurance-specific intelligence)
  - [ ] Publish LawChangeScored and LawChangeTagged events
- [ ] 3.1.7 Create Helm chart: infra/helm/summarizer/
  - [ ] deployment.yaml (1-2 replicas, CPU-bound workload)
  - [ ] service.yaml (optional)
  - [ ] secrets.yaml (Claude API key, RabbitMQ credentials)
  - [ ] values.yaml (image, resource limits: 1 CPU, 1 GB memory)
- [ ] 3.1.8 Create unit tests: src/summarizer.Tests/
  - [ ] Mock Claude API responses
  - [ ] Test quality gate logic
  - [ ] Test event publishing
- [ ] 3.1.9 Build and deploy:
  - [ ] Dockerfile: src/summarizer/Dockerfile
  - [ ] Build and push image
  - [ ] Deploy: `helm install nomadrules-summarizer infra/helm/summarizer/ -n nomadrules-services`
  - [ ] Test: trigger a LawChangeDetected event manually, watch handler process it

### 3.2 Email Delivery Service (C#)

- [ ] 3.2.1 Create src/email-service/.csproj:
  - [ ] NuGet packages: PrePass.Core.Startup, Resend SDK, Asp.Core.ServiceBus, Dapper
  - [ ] Reference src/shared
- [ ] 3.2.2 Create handlers: src/email-service/Handlers/
  - [ ] **DigestEmailHandler** (consumes DigestReady event)
    - [ ] Queries law_changes for past 7 days matching subscriber's state/categories
    - [ ] Renders DigestEmail.tsx component to HTML
    - [ ] Sends via Resend API
    - [ ] Logs delivery status (success, bounce, etc.)
    - [ ] Publishes DigestSent event with delivery metadata
  - [ ] **RenewalAlertHandler** (consumes RenewalAlertReady event)
    - [ ] Renders RenewalAlertEmail.tsx
    - [ ] Sends immediately (not batched)
    - [ ] Publishes AlertSent event
- [ ] 3.2.3 Create email templates: src/email-service/Templates/
  - [ ] (Jenn to implement) DigestEmail.tsx (React component → HTML)
    - [ ] Header: "Here's what changed in [state] insurance this week"
    - [ ] List of law changes: { headline, summary, link }
    - [ ] Call-to-action: "View full archive" + "Manage preferences"
    - [ ] Footer: Unsubscribe link (CAN-SPAM)
    - [ ] Responsive layout (mobile-friendly)
  - [ ] (Jenn to implement) RenewalAlertEmail.tsx
    - [ ] Prominent: "Your [TX] insurance renews in [60] days"
    - [ ] List of recent changes (past 12 months)
    - [ ] CTA: "Upgrade to Pro for urgent alerts" (if free/basic tier)
    - [ ] Footer: Unsubscribe link
- [ ] 3.2.4 Create Resend integration: src/email-service/Services/ResendClient.cs
  - [ ] Wrapper around Resend SDK
  - [ ] Error handling for transient failures (retries)
  - [ ] Cost tracking
  - [ ] Bounce handling (update subscriber status)
- [ ] 3.2.5 Implement digest scheduling:
  - [ ] Trigger: Every Friday at 9 AM UTC (via K8s CronJob or internal timer)
  - [ ] Query subscribers where next_digest_at <= now
  - [ ] For each subscriber:
    - [ ] Gather law_changes from past 7 days
    - [ ] Render email
    - [ ] Send via Resend
    - [ ] Update notifications table with delivery metadata
- [ ] 3.2.6 Create Helm chart: infra/helm/email-service/
  - [ ] deployment.yaml (2 replicas)
  - [ ] CronJob for digest trigger (Friday 9 AM UTC)
  - [ ] service.yaml
  - [ ] secrets.yaml (Resend API key, RabbitMQ credentials)
  - [ ] values.yaml
- [ ] 3.2.7 Test email rendering:
  - [ ] Render DigestEmail.tsx to HTML manually
  - [ ] Send test email via Resend
  - [ ] Check rendering on desktop, mobile, Outlook
- [ ] 3.2.8 Build and deploy:
  - [ ] Dockerfile: src/email-service/Dockerfile
  - [ ] Build, push, deploy via Helm

**Success Criteria (Week 3 end):**
- ✅ Crawler → Summarizer flow end-to-end (event → Claude → stored summary)
- ✅ First 10 summaries manually reviewed (quality validation)
- ✅ Email templates rendered correctly on mobile
- ✅ Digest sends successfully Friday 9 AM (test with manual date override)

---

## Phase 4: Portal & Renewal Alerts (Week 3-4)

### 4.1 Renewal Calendar Tool (React Portal)

- [ ] 4.1.1 Create src/portal/vite.config.ts (React + TypeScript + Vite)
- [ ] 4.1.2 Install dependencies: React, TypeScript, Tailwind CSS, shadcn/ui, TanStack Query, Axios
- [ ] 4.1.3 Create layout component: src/portal/src/layouts/MainLayout.tsx
  - [ ] Header with NomadRules logo
  - [ ] Navigation (Public Feed, Dashboard, Account)
  - [ ] Footer
- [ ] 4.1.4 Create public pages:
  - [ ] **PublicFeed.tsx** — Display last 3 law changes per state
    - [ ] State selector dropdown (TX initially)
    - [ ] List of changes: headline, summary snippet, date, link to detail
    - [ ] CTA: "Sign up to get alerts"
  - [ ] **LawChangeDetail.tsx** — Full change details
    - [ ] Headline, full summary, original source link, date
    - [ ] CTA: "Get notified when this happens" (signup/upgrade)
- [ ] 4.1.5 Create renewal calendar page: src/portal/src/pages/RenewalCalendar.tsx (PUBLIC, no auth)
  - [ ] Form inputs:
    - [ ] Email (required)
    - [ ] State dropdown (TX only in v0.1)
    - [ ] Renewal dates (insurance, registration, license, taxes) — all optional
    - [ ] Submit button: "Start getting alerts"
  - [ ] Validation: email format, at least one renewal date
  - [ ] On submit:
    - [ ] Call API: `POST /api/subscribers`
    - [ ] On success: show confirmation ("Check your email to verify")
    - [ ] On error: show user-friendly error message
  - [ ] Form styling: responsive, accessible (label for every input, error labels)
- [ ] 4.1.6 Create authenticated pages (with magic link auth):
  - [ ] **Dashboard.tsx** — Shows subscriber's renewal calendar + recent changes
    - [ ] Countdown cards for each renewal (insurance renewal in 37 days)
    - [ ] Recent law changes matching subscriber's state
    - [ ] "View full archive" link
  - [ ] **Archive.tsx** — Searchable law change archive
    - [ ] Search input (headline + summary)
    - [ ] Filters: state, category, severity, date range
    - [ ] Paginated results
    - [ ] Click change to view details
  - [ ] **ProfilePage.tsx** — Account settings
    - [ ] Display: email, domicile state, current tier
    - [ ] Edit: renewal dates, category preferences, email
    - [ ] Save changes button
  - [ ] **BillingPage.tsx** — Subscription management
    - [ ] Current tier badge
    - [ ] Upgrade/downgrade links (to Stripe payment page)
    - [ ] Manage subscription link (Stripe Customer Portal embed)
    - [ ] Billing history
- [ ] 4.1.7 Create API client: src/portal/src/api/subscriberClient.ts
  - [ ] TanStack Query hooks for all endpoints (useSubscriber, useLawChangeFeed, etc.)
  - [ ] Axios instance with auth token in headers (from httpOnly cookie)
  - [ ] Error handling (401 → redirect to login)
- [ ] 4.1.8 Create authentication context: src/portal/src/context/AuthContext.tsx
  - [ ] useAuth hook
  - [ ] Login: (email) → send magic link
  - [ ] Verify: (token from URL) → set cookie, redirect to dashboard
  - [ ] Logout
- [ ] 4.1.9 Style with Tailwind + shadcn/ui
  - [ ] Responsive design (mobile-first)
  - [ ] Accessibility: WCAG 2.1 AA standard
    - [ ] Keyboard navigation
    - [ ] Color contrast
    - [ ] Screen reader friendly
  - [ ] Light/dark mode toggle (optional)
- [ ] 4.1.10 Create loading states and error boundaries
- [ ] 4.1.11 Create Helm chart: infra/helm/portal/
  - [ ] deployment.yaml (static assets served via nginx)
  - [ ] service.yaml (port 80)
  - [ ] configmap.yaml (API_BASE_URL environment variable)
  - [ ] values.yaml
- [ ] 4.1.12 Create Dockerfile: src/portal/Dockerfile
  - [ ] Build stage: `npm run build` → static assets
  - [ ] Runtime stage: nginx serving assets + proxy rules (to API)
- [ ] 4.1.13 Unit tests: src/portal/src/__tests__/
  - [ ] Test form submission, validation
  - [ ] Test API client hooks
  - [ ] Test authentication flow
- [ ] 4.1.14 Build and deploy:
  - [ ] `npm run build` → dist/
  - [ ] Docker image
  - [ ] Helm deploy
  - [ ] Verify portal loads at http://localhost/

### 4.2 Renewal Alert Matching & Triggering

- [ ] 4.2.1 Create renewal alert engine: src/api/Services/RenewalAlertService.cs
  - [ ] Logic: Given a subscriber and law change, determine if alert should trigger
  - [ ] Inputs:
    - [ ] Subscriber's renewalDates (e.g., insurance renews on 2026-06-15)
    - [ ] Current date
    - [ ] Law change detected date
  - [ ] Output: AlertType enum (RENEWAL_60_DAYS, RENEWAL_30_DAYS, RENEWAL_7_DAYS, ROUTINE, INFORMATIONAL)
  - [ ] Algorithm:
    - [ ] If (renewalDate - currentDate) in [60±3, 30±3, 7±3] days → send alert
    - [ ] Always filter by subscriber's state + category preferences
- [ ] 4.2.2 Create renewal alert handler: src/api/Features/RenewalAlerts/RenewalAlertHandler.cs
  - [ ] Triggered daily at 3 AM UTC (K8s CronJob)
  - [ ] Query subscribers where renewal_date is in [60±3, 30±3, 7±3] days
  - [ ] For each subscriber:
    - [ ] Find law_changes from past 12 months matching state/categories
    - [ ] Render renewal alert email with context
    - [ ] Publish RenewalAlertReady event
  - [ ] Log summary: {subscribersMatched, alertsSent, errors}
- [ ] 4.2.3 Create RenewalAlertEmail.tsx template (Jenn):
  - [ ] Header: "Your [insurance] renews in [60] days"
  - [ ] Subheader: "Here's what changed in [TX] since last year"
  - [ ] List of applicable law changes (from past 12 months)
  - [ ] CTA: "View full summary" link to Portal
  - [ ] Secondary CTA: "Upgrade for urgent alerts" (if free/basic tier)
  - [ ] Footer: Unsubscribe
- [ ] 4.2.4 Add K8s CronJob for daily renewal matching:
  - [ ] Helm chart value: `renewalAlert.schedule: "0 3 * * *"` (daily 3 AM UTC)
  - [ ] Deploy

**Success Criteria (Week 4 end):**
- ✅ Portal loads, all pages accessible
- ✅ Calendar form submits, creates subscriber
- ✅ Magic link auth works (signup → email → verify → authenticated)
- ✅ Dashboard shows renewal countdown (manually test with date override)
- ✅ Renewal alert email sends 60/30/7 days before renewal
- ✅ Archive page searchable and filterable

---

## Phase 5: Testing, Validation & Launch (Week 4+)

### 5.1 End-to-End Integration Testing

- [ ] 5.1.1 Create integration test suite:
  - [ ] Setup: Spin up local K8s (minikube), docker-compose, seed database
  - [ ] Test flow: Crawler detects change → Summarizer processes → API stores → Email sends → Portal displays
  - [ ] Verify each step (logs, database state, emails)
- [ ] 5.1.2 Load testing (optional for MVP):
  - [ ] Simulate 100 concurrent subscribers requesting feed
  - [ ] Measure latency (target: <200ms p95)
- [ ] 5.1.3 Chaos testing (optional):
  - [ ] Kill Crawler pod → system continues, sends alert
  - [ ] Kill RabbitMQ → events queue, resume when back up
- [ ] 5.1.4 Manual smoke test (full flow):
  - [ ] Deploy all services to staging K8s
  - [ ] Add test law change to TX Insurance source
  - [ ] Verify Crawler detects it
  - [ ] Verify Summarizer processes it
  - [ ] Verify API returns it in feed
  - [ ] Verify Digest sends Friday
  - [ ] Verify Renewal alert sends at 60/30/7 days

### 5.2 Monitoring & Alerting

- [ ] 5.2.1 Set up Prometheus dashboards:
  - [ ] Crawler health (last run time, success rate, changes detected)
  - [ ] Summarizer throughput (events processed/min, quality gate pass rate, cost/summary)
  - [ ] API health (requests/sec, error rate, latency p50/p95/p99)
  - [ ] Email delivery (sends/hour, bounce rate, cost)
- [ ] 5.2.2 Create Prometheus alert rules:
  - [ ] Crawler offline >1 hour → critical alert
  - [ ] Summarizer quality gate failing >10% → warning
  - [ ] API error rate >1% → critical alert
  - [ ] Email delivery failing >100 bounces/day → critical alert
- [ ] 5.2.3 Set up alert routing (Slack webhook):
  - [ ] Post critical alerts to #nomadrules-alerts channel
  - [ ] Include runbook link (e.g., "Crawler failed — check this guide")
- [ ] 5.2.4 Create on-call runbook: docs/RUNBOOK.md
  - [ ] Common issues: Crawler not running, API 500 errors, email bounces
  - [ ] Troubleshooting steps for each
  - [ ] Escalation procedures

### 5.3 Documentation for Production

- [ ] 5.3.1 Create docs/DEPLOYMENT.md:
  - [ ] Prerequisites (K8s cluster, Helm, ArgoCD)
  - [ ] Quick-start: `kubectl apply -f infra/helm/values-prod.yaml`
  - [ ] Configuration: environment variables, secrets, resource limits
  - [ ] Scaling: increasing replicas, adjusting resource limits
- [ ] 5.3.2 Create docs/OPERATIONS.md:
  - [ ] Daily checks (crawler health, email delivery)
  - [ ] Weekly reviews (dashboard metrics, error rates)
  - [ ] Backup/restore procedures (PostgreSQL)
  - [ ] Secrets rotation (API keys, webhook secrets)
- [ ] 5.3.3 Create docs/TROUBLESHOOTING.md:
  - [ ] Common issues and solutions
  - [ ] How to check logs in K8s: `kubectl logs -n nomadrules-services -l app=api`
  - [ ] How to exec into pods for debugging
- [ ] 5.3.4 Create service README.md for each service:
  - [ ] src/crawler/README.md, src/api/README.md, etc.
  - [ ] Architecture, key components, how to modify

### 5.4 Quality Assurance

- [ ] 5.4.1 Summarizer quality review:
  - [ ] Jenn manually reviews first 10 summaries
  - [ ] Score: accuracy, tone, actionability
  - [ ] Document feedback
  - [ ] Refine prompt if needed
- [ ] 5.4.2 Email template QA:
  - [ ] Test rendering on desktop, mobile, Outlook, Gmail
  - [ ] Check links (all CTAs work)
  - [ ] Check unsubscribe link (functions correctly)
- [ ] 5.4.3 Portal usability testing:
  - [ ] Test signup flow (actual user perspective)
  - [ ] Test dashboard (data loads, countdown accurate)
  - [ ] Test archive search (filters work, results correct)
  - [ ] Test on mobile (responsive layout)
- [ ] 5.4.4 Accessibility audit:
  - [ ] Use automated tools (axe, Lighthouse)
  - [ ] Manual keyboard navigation
  - [ ] Screen reader testing (NVDA or JAWS)
  - [ ] Target: WCAG 2.1 AA

### 5.5 Security & Compliance

- [ ] 5.5.1 Security review:
  - [ ] API authentication (magic link tokens not leaking)
  - [ ] Authorization (subscriber can only see own data)
  - [ ] Stripe webhook signature validation
  - [ ] SQL injection prevention (Dapper/EF Core)
  - [ ] XSS prevention (React auto-escapes)
- [ ] 5.5.2 Data privacy:
  - [ ] Privacy policy (what data we collect, how we use it)
  - [ ] Terms of service
  - [ ] Unsubscribe mechanism (CAN-SPAM)
  - [ ] Data retention policy (how long do we keep law changes?)
  - [ ] GDPR compliance (if EU users): right to access, delete data
- [ ] 5.5.3 Secrets audit:
  - [ ] No hardcoded secrets in code or Docker images
  - [ ] All secrets in Sealed Secrets
  - [ ] Regular rotation (quarterly)
- [ ] 5.5.4 Dependency audit:
  - [ ] Run `npm audit`, `dotnet list --format json`
  - [ ] Update vulnerable packages

### 5.6 Community Launch

- [ ] 5.6.1 Prepare marketing copy (Jenn):
  - [ ] Homepage headline ("Watch the laws so you don't have to")
  - [ ] Email copy for signup confirmation
  - [ ] Social media post templates (Facebook, Twitter)
  - [ ] Forum posts (Escapees, iRV2, Full Time RVers groups)
- [ ] 5.6.2 Create FAQ page:
  - [ ] How do I enter renewal dates?
  - [ ] What's included in each tier?
  - [ ] How often do you check for changes?
  - [ ] How do I cancel?
- [ ] 5.6.3 Soft launch (week 1):
  - [ ] Share calendar tool with friends, beta testers
  - [ ] Collect feedback
  - [ ] Fix critical bugs
- [ ] 5.6.4 Community launch (week 2):
  - [ ] Jenn posts in 3 major RV Facebook groups
  - [ ] Post in Escapees forums
  - [ ] Share on Twitter/Reddit (if applicable)
  - [ ] Monitor signups (track conversion, CAC)
- [ ] 5.6.5 Post-launch monitoring (week 3+):
  - [ ] Daily: check signups, error rates, crawler health
  - [ ] Weekly: review engagement metrics, churn, LTV signals
  - [ ] Respond to user feedback

**Success Criteria (Month 1 end):**
- ✅ 100+ free users (calendar tool + email confirmations)
- ✅ 5+ paid subscribers
- ✅ <1% error rate on crawler (sources checked, diffs detected)
- ✅ <2% email bounce rate
- ✅ <1% API error rate
- ✅ Summarizer quality score >90% (manual review)
- ✅ Portal uptime >99%
- ✅ Net retention rate trending positive (less than 10% churn)

---

## Appendix: Task Dependencies

```
Phase 1 (Week 1):
├── 1.1 Monorepo setup
├── 1.2 K8s infrastructure
├── 1.3 Shared libraries
├── 1.4 Local development
└── 1.5 CI/CD

Phase 2 (Week 2):
├── 2.1 Crawler service
│   └── depends on: 1.1, 1.3, 1.5
├── 2.2 Subscriber API
│   └── depends on: 1.1, 1.3, 1.5
└── (These run in parallel)

Phase 3 (Week 2-3):
├── 3.1 Summarizer service
│   └── depends on: 2.1 (Crawler events), 1.3 (AsyncBus)
└── 3.2 Email delivery service
    └── depends on: 3.1 (DigestReady events), 1.3

Phase 4 (Week 3-4):
├── 4.1 Portal
│   └── depends on: 2.2 (API endpoints)
└── 4.2 Renewal alert matching
    └── depends on: 4.1 (Calendar tool for renewal dates)

Phase 5 (Week 4+):
└── Testing, validation, launch (depends on: all of above)
```

---

## Notes for Implementers

- **Use feature branches**: Create PR for each service (e.g., `feat/crawler`, `feat/api`, `feat/portal`)
- **Commit often**: Small, atomic commits with clear messages
- **Document as you go**: Update service README.md, API docs, runbooks in parallel
- **Pair programming**: Have Ryan review all infrastructure decisions, Jenn review all portal/email template changes
- **Test locally first**: Use docker-compose to verify before deploying to K8s
- **Monitor from day 1**: Set up observability alongside feature development
- **Validate assumptions**: Track conversion rate, churn, summarizer quality — adjust tactics if signals are weak
