# Tech Stack

All technology decisions are made to align with our existing expertise and Azure-first infrastructure strategy.

> **Note:** This document predates the v0.1 pivots recorded in `CLAUDE.md` (SQLite/Postgres instead of Cosmos DB, a simple AsyncBus queue instead of NServiceBus, AKS instead of Azure Functions/Container Apps) and has not been fully updated to match. Treat `CLAUDE.md`'s "Technology Stack at a Glance" table as the authoritative current state; this file needs a broader refresh beyond the scope of the change that added this note (see `openspec/changes/aks-secure-platform-deployment`).

---

## Crawler layer

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Runtime | Node.js 22 / TypeScript | Team expertise; strong ecosystem for web scraping |
| Browser automation | Playwright | Handles JS-rendered government sites; reliable cross-browser |
| Scheduling | Azure Container Apps — cron triggers | Serverless, no infra to manage, scales to zero |
| PDF extraction | `pdfjs-dist` | Pure JS, no native dependencies, well-maintained |
| HTML diffing | `htmldiff-js` or custom DOM-aware diff | Noise-resilient; ignores nav/date/whitespace changes |
| Blob storage client | `@azure/storage-blob` | Official SDK, MSI auth support |
| Message publishing | `@azure/service-bus` | Publishes to Azure Service Bus for NServiceBus consumption |

**Scraper interface (TypeScript):**
```typescript
export interface ISourceScraper {
  sourceId: string;
  schedule: string; // cron expression
  scrape(): Promise<ScrapeResult>;
}

export interface ScrapeResult {
  sourceId: string;
  content: string;       // normalized text content
  contentHash: string;
  url: string;
  scrapedAt: Date;
}
```

---

## Processing layer

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Runtime | .NET 9 / C# | Team expertise; strong Azure Functions support |
| Serverless host | Azure Functions (isolated worker) | Consumption plan — zero cost at idle |
| Message bus | NServiceBus 9 on Azure Service Bus | Retry, dead-letter, sagas, outbox — reliability backbone |
| AI client | `Anthropic.SDK` (unofficial) or raw `HttpClient` | Call Claude API for summarization and scoring |
| Cosmos DB client | `Microsoft.Azure.Cosmos` | Official SDK with LINQ support |
| Dependency injection | `Microsoft.Extensions.DependencyInjection` | Standard .NET DI |
| Logging | `Microsoft.Extensions.Logging` → Application Insights | Structured logging, zero config in Azure Functions |

**NServiceBus message contracts:**
```csharp
public record LawChangeDetected(
    string SourceId,
    string RawContent,
    string Url,
    DateTimeOffset DetectedAt
);

public record LawChangeSummarized(
    Guid LawChangeId,
    string Headline,
    string Summary,
    string Severity,         // "urgent" | "routine" | "informational"
    string[] Tags,           // ["insurance", "SD", "vehicle"]
    string[] AffectedStates
);

public record SubscriberNotificationQueued(
    Guid SubscriberId,
    Guid LawChangeId,
    string DeliveryType      // "digest" | "immediate"
);
```

---

## Subscriber API

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Framework | ASP.NET Core 9 (minimal API) | Team expertise; excellent Azure App Service support |
| Auth | Azure Entra External ID (CIAM) | Decided over Auth0/B2C — managed identity, no shared secret to rotate; see `openspec/changes/azure-entra-auth-iac/` |
| ORM / data access | `Microsoft.Azure.Cosmos` (direct) | Schema-flexible; matches Cosmos document model |
| Validation | `FluentValidation` | Clean, expressive validation rules |
| API docs | Swagger / Scalar | Auto-generated from minimal API route definitions |
| Stripe | `Stripe.net` | Official .NET SDK |
| Email | `Resend` SDK or `SendGrid` | Resend preferred for simplicity |

---

## Portal (subscriber web app)

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Framework | React 19 + TypeScript | Team expertise |
| Build tool | Vite | Fast dev experience |
| Routing | React Router v7 | File-based or code-based routing |
| State management | Zustand | Lightweight; avoids Redux overhead for this app size |
| Data fetching | TanStack Query | Caching, background refresh, optimistic updates |
| UI components | shadcn/ui + Tailwind CSS | Unstyled primitives; full control over design |
| Forms | React Hook Form + Zod | Type-safe validation, minimal re-renders |
| Stripe | `@stripe/stripe-js` + `@stripe/react-stripe-js` | Customer Portal embed for billing management |
| Hosting | Azure Static Web Apps | Free tier, built-in GitHub Actions CI/CD, API routes |

---

## Infrastructure

| Concern | Choice | Rationale |
|---------|--------|-----------|
| IaC | Terraform | Team expertise; provider coverage for all Azure resources |
| State backend | Azure Blob Storage (remote state) | Standard Terraform Azure pattern |
| Secrets | Azure Key Vault | Centralized; MSI access from all compute |
| Container registry | Azure Container Registry | Crawler container images |
| Observability | Azure Monitor + Application Insights | Unified logging, metrics, alerting |
| CI/CD | GitHub Actions | Free for public repos; integrates with Azure |

**Core Terraform modules planned:**
```
infra/
├── modules/
│   ├── crawler/         # Container Apps environment + cron apps
│   ├── processor/       # Azure Functions + Service Bus
│   ├── api/             # App Service plan + App Service
│   ├── portal/          # Static Web App
│   ├── data/            # Cosmos DB + Blob Storage
│   └── shared/          # Key Vault, App Insights, ACR
├── environments/
│   ├── dev/
│   └── prod/
└── main.tf
```

---

## Data model (Cosmos DB)

**Containers:**

### `subscribers`
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "domicileStates": ["SD"],
  "categories": ["insurance", "tax", "dmv"],
  "tier": "pro",
  "alertPreferences": {
    "digestDay": "friday",
    "urgentAlerts": true
  },
  "stripeCustomerId": "cus_xxx",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

### `lawChanges`
```json
{
  "id": "uuid",
  "sourceId": "sd-insurance-bulletins",
  "url": "https://dlr.sd.gov/...",
  "headline": "SD raises minimum liability coverage for RVs over 26,000 lbs",
  "summary": "Plain-English summary...",
  "severity": "routine",
  "tags": ["insurance", "vehicle", "SD"],
  "affectedStates": ["SD"],
  "rawContentRef": "blob://snapshots/sd-insurance/2025-06-01.html",
  "detectedAt": "2025-06-01T02:34:00Z",
  "processedAt": "2025-06-01T02:35:12Z"
}
```

### `sources`
```json
{
  "id": "sd-insurance-bulletins",
  "name": "SD Division of Insurance — Bulletins",
  "url": "https://dlr.sd.gov/insurance/bulletins.aspx",
  "strategy": "html-diff",
  "schedule": "0 2 * * *",
  "state": "SD",
  "category": "insurance",
  "enabled": true,
  "lastCheckedAt": "2025-06-01T02:00:00Z",
  "lastChangeDetectedAt": "2025-05-15T02:00:00Z"
}
```

---

## Local development

```bash
# Prerequisites
node >= 22
dotnet >= 9
terraform >= 1.9
az cli (logged in)
docker (for Azurite, Cosmos emulator)

# Crawler
cd src/crawler
npm install
npm run dev        # runs scrapers locally against Azurite

# Processor (Azure Functions)
cd src/processor
func start         # Azure Functions Core Tools

# API
cd src/api
dotnet run

# Portal
cd src/portal
npm install
npm run dev        # Vite dev server on :5173
```

Docker Compose for local dependencies (Azurite, Cosmos emulator, Service Bus emulator) lives at `/docker-compose.yml`.
