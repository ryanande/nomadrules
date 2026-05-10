# System Architecture

## Overview

NomadRules is built as a self-managing pipeline: data flows from government sources through an AI processing layer, gets matched to subscriber profiles, and is delivered automatically with no weekly human intervention required.

```
┌─────────────────────────────────────────────────────────────────┐
│  DATA LAYER                                                      │
│  State legislatures · DMV agencies · Insurance regulators · IRS │
└────────────────────────┬────────────────────────────────────────┘
                         │ scheduled scrape + diff detection
┌────────────────────────▼────────────────────────────────────────┐
│  CRAWLER LAYER  (TypeScript / Node — Azure Container Apps)       │
│  Playwright scrapers · RSS monitors · Diff engine · Change queue │
└────────────────────────┬────────────────────────────────────────┘
                         │ NServiceBus message → Azure Service Bus
┌────────────────────────▼────────────────────────────────────────┐
│  PROCESSING LAYER  (C# Azure Functions)                         │
│  AI Summarizer (Claude API) · Severity scorer · Tagger          │
│  Relevance engine → subscriber profile matching                 │
└────────┬───────────────────────────────────────┬────────────────┘
         │                                       │
┌────────▼─────────┐                  ┌──────────▼──────────────┐
│  DELIVERY LAYER  │                  │  STORAGE LAYER           │
│  Weekly digest   │                  │  Cosmos DB               │
│  Urgent alerts   │                  │  • Subscriber profiles   │
│  Subscriber API  │                  │  • Law change records    │
│  (Resend/Beehiiv)│                  │  Blob Storage            │
└────────┬─────────┘                  │  • Raw HTML diffs        │
         │                            │  • Processed summaries   │
┌────────▼─────────┐                  └─────────────────────────┘
│  SUBSCRIBER      │
│  PORTAL          │
│  React / TypeScript                 
│  Azure Static    │
│  Web Apps        │
└──────────────────┘
```

## Component breakdown

### 1. Crawler layer

**Technology:** TypeScript / Node.js, Playwright, Azure Container Apps

**Responsibility:** Scheduled scraping of government sources, detecting meaningful diffs, and publishing change events to the message bus.

**Key behaviors:**
- Runs on a configurable cron schedule per source (some daily, some weekly)
- Stores a snapshot of each page/document in Azure Blob Storage
- On next run, diffs current content against stored snapshot
- If diff exceeds a noise threshold (filters out nav changes, dates, etc.), publishes a `LawChangeDetected` NServiceBus message
- Supports multiple scraper strategies: full-page HTML diff, structured RSS/Atom feed, PDF text extraction (for regulatory bulletins)

**Extensibility:** Each state/agency gets its own scraper module implementing a shared `ISourceScraper` interface. Adding a new source = adding one file.

### 2. Processing layer

**Technology:** C# Azure Functions (consumption plan), NServiceBus on Azure Service Bus

**Responsibility:** Consuming raw change events, enriching them with AI-generated summaries and metadata, then routing to the correct subscribers.

**Message flow:**
```
LawChangeDetected
  → SummarizationHandler        (calls Claude API, stores PlainEnglishSummary)
  → SeverityScoringHandler      (urgent / routine / informational)
  → TaggingHandler              (domicile state, category, affected subscriber segments)
  → RelevanceMatchingHandler    (queries Cosmos DB, emits SubscriberNotificationQueued per match)
  → NotificationDispatchHandler (writes to delivery queue — Resend or Beehiiv)
```

**Why NServiceBus:** We get retry policies, dead-letter handling, saga support (for digest batching), and outbox pattern for exactly-once processing — all out of the box. This is the reliability backbone of the self-managing design.

### 3. AI layer (Claude API)

**Technology:** Anthropic Claude API (`claude-sonnet-4-20250514`), called from C# processing functions

**Responsibility:** Converting raw legislative/regulatory text into subscriber-friendly content.

**Prompts (to be refined — see `/src/processor/Prompts/`):**
- **Summarization prompt:** Given raw text of a law change, produce a plain-English summary (2–3 sentences), a one-line headline, and an explanation of who is affected and how.
- **Severity scoring prompt:** Given the summary, rate the change as `urgent` (action required within 30 days), `routine` (awareness, no immediate action), or `informational` (background context).
- **Tagging prompt:** Extract affected domicile states, categories (insurance / tax / DMV / voting / business), and subscriber segments (all RVers / motorhome owners / pre-Medicare / etc.).

### 4. Subscriber API

**Technology:** ASP.NET Core, Azure App Service

**Responsibility:** All subscriber-facing operations — account management, profile preferences, notification history, billing webhooks from Stripe.

**Key endpoints:**
- `POST /subscribers` — registration
- `GET/PUT /subscribers/{id}/profile` — domicile states, categories of interest, alert preferences
- `GET /subscribers/{id}/feed` — paginated law change feed relevant to this subscriber
- `POST /webhooks/stripe` — subscription lifecycle events

### 5. Portal

**Technology:** React + TypeScript, Azure Static Web Apps

**Responsibility:** Subscriber-facing web UI — onboarding, profile management, searchable law change archive, notification history.

**Key views:**
- Onboarding wizard (domicile state selection, category preferences)
- Dashboard (recent changes, urgency-sorted)
- Law library (searchable archive, filterable by state / category / date)
- Account & billing (Stripe Customer Portal embed)

### 6. Infrastructure

**Technology:** Terraform, Azure

**Resources provisioned:**
- Azure Container Apps (crawler workers)
- Azure Functions (processing)
- Azure Service Bus (Standard tier, NServiceBus transport)
- Azure Cosmos DB (serverless, subscriber + content data)
- Azure Blob Storage (raw diffs, snapshots)
- Azure App Service (subscriber API)
- Azure Static Web Apps (portal)
- Azure Key Vault (secrets — API keys, Stripe webhook secret, connection strings)
- Azure Monitor + Application Insights (observability)

## Data flow — end to end

```
1. Cron fires (e.g., daily at 02:00 UTC)
2. Crawler fetches SD insurance commissioner bulletin page
3. Diffs against yesterday's snapshot → detects new paragraph
4. Publishes LawChangeDetected { source, rawText, url, detectedAt }
5. SummarizationHandler: calls Claude API → stores PlainEnglishSummary
6. SeverityScoringHandler: scores as "routine"
7. TaggingHandler: tags { state: "SD", category: "insurance", segment: "all" }
8. RelevanceMatchingHandler: queries Cosmos for subscribers with domicile=SD
9. SubscriberNotificationQueued emitted per match
10. Digest batcher: accumulates weekly items per subscriber using NServiceBus saga
11. On digest send day (Friday): renders personalized email → Resend API
12. Urgent items bypass digest → immediate send
```

## Observability

- All Azure Functions and Container Apps report to Application Insights
- NServiceBus poison message handling → dead-letter queue → alert via Azure Monitor
- Weekly automated report: sources checked, diffs found, summaries generated, emails sent
- Stripe webhook failures → retry queue + Slack alert

## Self-managing design principles

The system is designed to require **zero weekly human intervention** after setup:

| Concern | Self-managing mechanism |
|---------|------------------------|
| New law changes | Crawler cron, fully automated |
| Summarization quality | Claude API + prompt versioning |
| Subscriber billing | Stripe + webhook handlers |
| Failed messages | NServiceBus retry + dead-letter |
| Source goes offline | Crawler error → alert only, does not block other sources |
| New source onboarding | Add one scraper file, redeploy Container App |
