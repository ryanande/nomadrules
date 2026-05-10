# NomadRules

> Automated legal & regulatory intelligence for full-time RVers, digital nomads, and domicile-state residents.

NomadRules is a self-managing subscription service that monitors state and federal law changes — insurance regulations, tax rules, DMV requirements, voting laws — and delivers plain-English digests personalized to each subscriber's domicile state(s).

## Why this exists

Full-time RVers research domicile law once and then never revisit it. NomadRules catches the changes that happen *after* they've set it and forgotten it — before a renewal, a registration, or a coverage gap bites them.

## Documentation

| Doc | Description |
|-----|-------------|
| [Business Concept](docs/01-business-concept.md) | Market opportunity, target personas, value proposition |
| [System Architecture](docs/02-system-architecture.md) | End-to-end system design and component overview |
| [Data Sources](docs/03-data-sources.md) | Regulatory sources by category and domicile state |
| [Tech Stack](docs/04-tech-stack.md) | Technology decisions aligned to our Azure / .NET / TypeScript environment |
| [Subscription Model](docs/05-subscription-model.md) | Pricing tiers, revenue streams, affiliate strategy |
| [MVP Plan](docs/06-mvp-plan.md) | Phased build-out from proof of concept to full product |

## Repo structure

```
nomadrules/
├── docs/                   # Architecture and planning docs (you are here)
├── src/
│   ├── crawler/            # TypeScript — scheduled law change scrapers
│   ├── processor/          # C# — AI summarization + relevance engine (Azure Functions)
│   ├── api/                # C# ASP.NET Core — subscriber API
│   ├── portal/             # React / TypeScript — subscriber web portal
│   └── infra/              # Terraform — Azure infrastructure as code
├── .github/
│   └── workflows/          # CI/CD pipelines
└── README.md
```

## Stack at a glance

- **Crawlers** — Node.js / TypeScript, Playwright, Azure Container Apps (cron)
- **Processing pipeline** — C# Azure Functions, NServiceBus on Azure Service Bus
- **AI layer** — Anthropic Claude API (summarization + relevance scoring)
- **API** — ASP.NET Core, Azure App Service
- **Portal** — React + TypeScript, Azure Static Web Apps
- **Data** — Azure Cosmos DB (subscriber profiles) + Azure Blob Storage (raw diffs)
- **Billing** — Stripe
- **Email delivery** — Resend (transactional) or Beehiiv (digest newsletters)
- **Infrastructure** — Terraform, Azure
