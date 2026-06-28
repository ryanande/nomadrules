# AGENTS.md — Service Ownership & Team Responsibilities

## Team Structure

| Role | Owner | Primary Domains | Responsibilities |
|------|-------|-----------------|------------------|
| **Backend Architect & DevOps** | Ryan | Infrastructure, Crawler, API, Summarizer | System design, Kubernetes, monorepo, async messaging, data pipeline |
| **Frontend & Product** | Jenn | Portal, Marketing, Copy | Portal UI/UX, email templates, community messaging, content strategy |
| **AI Specialist** (optional) | Ryan (interim) | Summarizer prompts, quality gates | Claude API integration, prompt engineering, hallucination detection, cost optimization |

---

## Service Ownership Matrix

### Service 1: Crawler (TypeScript, Playwright)

**Owner**: Ryan

**Responsibilities**:
- Source scraper implementations (HTML diff, RSS, PDF, Federal Register API)
- Playwright automation for JS-rendered content
- Snapshot storage and diff detection
- Change event publishing to AsyncBus
- Rate limiting and IP rotation (if needed)
- Source monitoring and alerting

**Key Files**:
- src/crawler/README.md (service setup guide)
- src/crawler/src/sources/ (one file per source)
- src/crawler/src/diff-engine.ts
- infra/helm/crawler/ (K8s deployment)

**Success Metrics**:
- Crawler uptime >99%
- Diff detection false-negative rate <1%
- Crawler latency <5s per source
- Cost per run <$0.10

**On-call**: Ryan (alert if crawler fails 2+ times in a day)

---

### Service 2: Summarizer (C#, Azure Function or dedicated service)

**Owner**: Ryan (infrastructure), Ryan/Jenn (prompts)

**Responsibilities**:
- Consumes law_change_detected events
- Calls Claude API with summarization prompt
- Extracts headline, summary, affected segments
- Scores quality and detects hallucination
- Stores summaries in database
- Publishes law_change_summarized event
- Monitors Claude API costs and quality

**Key Files**:
- src/summarizer/README.md
- src/summarizer/Prompts/*.md (prompt library, versioned)
- src/summarizer/Services/SummarizationHandler.cs
- src/summarizer/Services/QualityGateService.cs
- infra/helm/summarizer/

**Success Metrics**:
- Summarization latency <10s (p95)
- Quality gate pass rate >95%
- Claude API cost <$0.20 per summary
- Hallucination rate <2% (manual review of sample)

**On-call**: Ryan (alert if >10% of summaries fail quality gate)

**Jenn's Input**:
- Reviews first 10 summaries per source (quality calibration)
- Suggests prompt improvements based on user feedback
- Owns tone/voice of summaries (insurance-specific, actionable)

---

### Service 3: Scorer & Tagger (C#)

**Owner**: Ryan

**Responsibilities**:
- Consumes law_change_summarized events
- Scores severity (urgent/routine/informational) via Claude or rules engine
- Extracts tags: states, categories, affected segments
- Publishes law_change_scored and law_change_tagged events
- Maintains tag taxonomy in code

**Key Files**:
- src/api/Services/ScoringService.cs
- src/api/Services/TaggingService.cs
- src/shared/Events/LawChangeScored.cs
- src/shared/Events/LawChangeTagged.cs
- docs/TAG_TAXONOMY.md

**Success Metrics**:
- Tagging accuracy >95%
- Severity scoring alignment with manual review >90%

**On-call**: Ryan (alerts for tag mismatches)

---

### Service 4: Subscriber API (C# ASP.NET Core, Flint pattern)

**Owner**: Ryan

**Responsibilities**:
- Subscriber registration and profile management
- Domicile state and renewal date management
- Law change feed retrieval and filtering
- Stripe webhook handling
- Vertical slices: SubscriberRegistration, Profile, Feed, RenewalAlerts, Billing
- API documentation and versioning
- Rate limiting and CORS

**Key Files**:
- src/api/README.md
- src/api/Features/SubscriberRegistration/ (vertical slice)
- src/api/Features/Profile/
- src/api/Features/Feed/
- src/api/Features/RenewalAlerts/
- src/api/Features/Billing/
- src/api/Program.cs (Flint startup)
- infra/helm/api/

**Success Metrics**:
- API uptime >99.9%
- P95 latency <200ms
- Stripe webhook processing <1s
- Error rate <0.1%

**On-call**: Ryan (alert for API errors or Stripe failures)

---

### Service 5: Email Delivery Service (C#, async handlers)

**Owner**: Ryan (infrastructure), Jenn (templates)

**Responsibilities**:
- Consumes renewal_alert_ready and digest_ready events
- Renders personalized HTML emails
- Sends via Resend API
- Handles bounces and unsubscribes
- Tracks delivery and engagement
- Publishes digest_sent and alert_sent events

**Key Files**:
- src/email-service/README.md
- src/email-service/Handlers/DigestEmailHandler.cs
- src/email-service/Handlers/RenewalAlertHandler.cs
- src/email-service/Templates/*.html (Jenn's domain)
- infra/helm/email-service/

**Success Metrics**:
- Email delivery rate >98%
- Bounce rate <1%
- SMTP timeout rate <0.5%
- Resend API cost <$0.001 per email

**On-call**: Ryan (alert for delivery failures)

**Jenn's Input**:
- Owns email HTML templates and styling
- Sets email tone and copy (NomadRules voice)
- Iterates based on user feedback and engagement metrics
- A/B tests subject lines and CTAs

---

### Service 6: Portal (React, TypeScript, shadcn/ui)

**Owner**: Jenn

**Responsibilities**:
- Public law change feed (last 3 items per state)
- Renewal calendar onboarding (no auth required initially)
- Authenticated dashboard with countdown to renewals
- Law change archive with search and filters
- Profile management (email, domicile, preferences)
- Billing page (Stripe Customer Portal embed)
- Mobile responsive and accessible (WCAG 2.1 AA)

**Key Files**:
- src/portal/README.md
- src/portal/src/pages/PublicFeed.tsx
- src/portal/src/pages/RenewalCalendar.tsx
- src/portal/src/pages/Dashboard.tsx
- src/portal/src/pages/Archive.tsx
- src/portal/src/components/ (shadcn/ui components)
- infra/helm/portal/

**Success Metrics**:
- Portal uptime >99.5%
- P95 load time <2s
- Accessibility audit score 90+
- Mobile bounce rate <5%

**On-call**: Jenn (alert for portal errors or downtime)

---

### Service 7: AsyncBus / Message Router (underlying all services)

**Owner**: Ryan

**Responsibilities**:
- Event envelope and contract management
- RabbitMQ/AMQP transport configuration
- Dead-letter queue handling
- Retry policies and exponential backoff
- Event versioning and backward compatibility
- Message tracing and correlation IDs

**Key Files**:
- src/shared/Events/ (all event types)
- src/shared/AsyncBus/ (client library)
- infra/helm/rabbitmq/ (RabbitMQ deployment)

**Success Metrics**:
- Message delivery success rate >99.9%
- DLQ message rate <0.1%
- End-to-end latency <5s (Crawler → Summarizer → API)

---

## Cross-Cutting Concerns

### Observability (Ryan owns, all services contribute)

**Responsibilities**:
- Prometheus metrics instrumentation
- Structured logging to ELK/Seq
- Distributed tracing with Jaeger
- Grafana dashboards
- Alert rules for critical paths

**Key Files**:
- infra/helm/prometheus/
- infra/helm/grafana/
- infra/helm/jaeger/
- src/shared/Observability/ (client library)

### Infrastructure & Deployments (Ryan owns)

**Responsibilities**:
- Kubernetes cluster provisioning (optional Terraform)
- Helm chart templating and updates
- ArgoCD GitOps configuration
- Secrets management (Key Vault)
- CI/CD pipeline configuration (.github/workflows/)
- Local docker-compose for development

**Key Files**:
- infra/helm/
- infra/terraform/
- .github/workflows/
- docker-compose.yml

### Security (Ryan owns, all services follow)

**Responsibilities**:
- API authentication (magic links → JWT)
- Authorization (subscriber can only see own profile)
- Stripe webhook signature verification
- Input validation and SQL injection prevention
- Secrets rotation and management
- CORS and CSRF protection

### Data & Database (Ryan owns)

**Responsibilities**:
- Schema design and migrations
- Backup and recovery procedures
- Query optimization
- Database connection pooling

**Key Files**:
- src/api/Data/ (migrations, context)
- docs/DATABASE_SCHEMA.md

---

## Weekly Standups & Syncs

| Cadence | Participants | Topics |
|---------|--------------|--------|
| **Daily async** | Ryan, Jenn | PR reviews, blockers, daily metrics |
| **Weekly sync** (1h) | Ryan, Jenn | Progress on tasks, validation signals, pivots, community feedback |
| **Bi-weekly deep dive** (1h) | Ryan, Jenn, optional AI specialist | Architecture decisions, performance analysis, roadmap |

---

## Escalation & On-Call

**Critical Alerts** (PagerDuty if integrated):
- Crawler offline >2 hours → Ryan
- Summarizer quality gate failing >10% → Ryan
- API errors >1% → Ryan
- Email delivery failing >100 bounces in 1h → Ryan, Jenn

**Non-Critical Issues**:
- Use GitHub issues, tagged by service
- Assign to service owner
- Triage in weekly sync

---

## Onboarding New Team Members

1. **Read CLAUDE.md** — system identity and principles
2. **Read this file (AGENTS.md)** — ownership and responsibilities
3. **Read /.claude/ARCHITECTURE.md** — system design
4. **Clone repo and run docker-compose** — get comfortable with local setup
5. **Pick a service from the table above** — read its README.md and understand its job
6. **Pair with service owner** — first PR review, architecture questions
7. **Join weekly sync** — understand team rhythm and decision-making

---

## Service Skill Matrix

| Service | Language | Framework | Owner Expertise | Backup |
|---------|----------|-----------|-----------------|--------|
| Crawler | TypeScript | Playwright, Node | Ryan (expert) | Jenn (learning) |
| Summarizer | C# | Azure Functions | Ryan (expert) | — |
| API | C# | ASP.NET Core, Flint | Ryan (expert) | — |
| Email Service | C# | Custom async handlers | Ryan (expert) | — |
| Portal | React/TS | React, shadcn/ui, Vite | Jenn (expert) | Ryan (learning) |

**Upskilling**:
- Ryan → React fundamentals (portal troubleshooting, design feedback)
- Jenn → C# and ASP.NET basics (API integration, backend feature requests)
- Both → Kubernetes and Helm (deployment and scaling)
- Both → AsyncBus and event-driven patterns (system behavior)

---

## Handoff Procedure (if owner unavailable)

**If Ryan unavailable** (vacation, sabbatical):
1. Jenn owns Portal and email template updates
2. Crawler and API in maintenance mode (no new features)
3. Critical bugs → escalate to backup engineer (TBD)
4. New feature development pauses

**If Jenn unavailable**:
1. Ryan owns Portal and email template updates (basic HTML only)
2. Community communication pauses
3. Marketing features on hold

---

## Future: Contractor & New Hires

When hiring:
- **AI specialist** → Owns summarizer prompts, quality gates, cost optimization
- **Frontend engineer** → Pairs with Jenn on Portal, takes ownership of responsive design and accessibility
- **DevOps engineer** → Pairs with Ryan on K8s, Helm, ArgoCD, observability
- **Data analyst** → Owns metrics dashboard, user behavior analysis, churn reduction

For each hire, update this file with service ownership and escalation paths.
