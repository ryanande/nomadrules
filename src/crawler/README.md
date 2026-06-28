# NomadRules — Crawler

Scheduled TypeScript/Playwright scrapers that monitor regulatory sources for law changes.

## Quick start

```bash
docker-compose up -d
cp .env.example .env
npm install && npx playwright install chromium
npm run crawl
ls ./local-queue/
```

## Execution modes

| Command | Behaviour |
|---------|-----------|
| `npm run crawl` | Run all scrapers once and exit |
| `npm run crawl -- --source=sd-insurance-bulletins` | Run one scraper |
| `npm run dev` | Live reload on file changes |
| `npm start` | Long-running cron scheduler (Container Apps) |
