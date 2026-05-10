# Data Sources

Regulatory sources organized by category and domicile state. Each source entry includes the URL, update frequency, scraper strategy, and priority for initial build-out.

Priority levels: `P0` = MVP, `P1` = v1.1, `P2` = future

---

## Insurance regulation

### South Dakota

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| SD Division of Insurance — bulletins | https://dlr.sd.gov/insurance/bulletins.aspx | Weekly | HTML diff | P0 |
| SD Division of Insurance — news | https://dlr.sd.gov/insurance/newsroom.aspx | Weekly | HTML diff | P0 |
| SD Legislature — Commerce & Energy committee | https://sdlegislature.gov | Session-period daily | Bill tracker RSS | P1 |

### Texas

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| TX Dept of Insurance — bulletins | https://www.tdi.texas.gov/bulletins/ | Weekly | HTML diff | P0 |
| TX Dept of Insurance — news releases | https://www.tdi.texas.gov/news/ | Weekly | RSS | P0 |
| TX Legislature Online | https://capitol.texas.gov | Session-period daily | Bill tracker | P1 |

### Florida

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| FL Office of Insurance Regulation — orders | https://www.floir.com/siteDocuments/Orders | Weekly | HTML + PDF diff | P0 |
| FL OIR — press releases | https://www.floir.com/PressReleases | Weekly | RSS | P0 |
| FL Legislature — Insurance & Banking committee | https://www.flsenate.gov | Session-period daily | Bill tracker | P1 |

### Federal (all subscribers)

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| NAIC model law updates | https://www.naic.org/model_laws.htm | Monthly | HTML diff | P1 |
| HHS / CMS — ACA rulemaking | https://www.federalregister.gov (filtered) | Weekly | Federal Register API | P0 |
| CMS — Medicare plan updates | https://www.cms.gov/newsroom | Weekly | RSS | P1 |

---

## Tax regulation

### South Dakota

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| SD Dept of Revenue — tax bulletins | https://dor.sd.gov/businesses/tax-bulletins/ | Monthly | HTML diff | P0 |
| SD Dept of Revenue — news | https://dor.sd.gov/news/ | Monthly | HTML diff | P0 |

### Texas

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| TX Comptroller — tax publications | https://comptroller.texas.gov/taxes/publications/ | Monthly | HTML diff | P0 |
| TX Comptroller — news | https://comptroller.texas.gov/about/media-center/ | Monthly | RSS | P0 |

### Florida

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| FL Dept of Revenue — general tax admin | https://floridarevenue.com/taxes/taxesfees | Monthly | HTML diff | P0 |
| FL Dept of Revenue — news | https://floridarevenue.com/news | Monthly | RSS | P0 |

### Federal

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| IRS — news releases | https://www.irs.gov/newsroom/irs-news-releases | Weekly | RSS | P0 |
| IRS — tax code changes (TCJA updates etc.) | https://www.irs.gov/tax-professionals/tax-code-regulations-and-official-guidance | Monthly | HTML diff | P1 |
| Federal Register — Treasury / IRS rules | https://www.federalregister.gov (filtered) | Weekly | Federal Register API | P1 |

---

## DMV / vehicle registration

### South Dakota

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| SD Motor Vehicle Division — fee schedules | https://dor.sd.gov/motor-vehicles/ | Quarterly | HTML diff | P0 |
| SD Dept of Public Safety — driver licensing | https://dps.sd.gov/licensing/drivers-licensing | Quarterly | HTML diff | P0 |

### Texas

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| TX DMV — registration fees | https://www.txdmv.gov/motorists/register-your-vehicle | Quarterly | HTML diff | P0 |
| TX DPS — driver license requirements | https://www.dps.texas.gov/section/driver-license | Quarterly | HTML diff | P0 |

### Florida

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| FL DMV (HSMV) — vehicle registration | https://www.flhsmv.gov/motor-vehicles-tags-titles/ | Quarterly | HTML diff | P0 |
| FL HSMV — driver licenses | https://www.flhsmv.gov/driver-licenses-id-cards/ | Quarterly | HTML diff | P0 |

---

## Voting / domicile establishment

| Source | URL | Frequency | Strategy | Priority |
|--------|-----|-----------|----------|----------|
| SD SOS — voter registration rules | https://sdsos.gov/elections-voting/ | Monthly | HTML diff | P0 |
| TX SOS — voter registration | https://www.sos.state.tx.us/elections/voter/ | Monthly | HTML diff | P0 |
| FL SOS — voter registration | https://dos.myflorida.com/elections/for-voters/voter-registration/ | Monthly | HTML diff | P0 |

> **Note:** SD voting rules changed significantly in 2025 (HB 1208). This category can generate high-urgency alerts when state legislatures are in session.

---

## Scraper strategy guide

### HTML diff
Best for pages that publish bulletins or news as HTML. Store full rendered HTML (after JS execution via Playwright), strip nav/footer noise with CSS selectors, then diff the content zone. A diff above a configurable token threshold triggers a `LawChangeDetected` event.

### RSS / Atom feed
Best for agencies that publish proper feeds. Poll on schedule, store seen GUIDs, emit event on new items. Simpler and more reliable than HTML diff when available.

### PDF extraction
For agencies that publish regulatory bulletins as PDFs. Use `pdfjs-dist` (TypeScript) or `iTextSharp` (C#) to extract text. Hash the full text — any hash change triggers processing.

### Federal Register API
The Federal Register offers a proper REST API (`api.federalregister.gov`). Filter by agency (IRS, HHS, CMS, etc.) and document type (rule, proposed rule). No scraping needed.

---

## Source onboarding checklist

When adding a new source:

- [ ] Identify the source URL and update cadence
- [ ] Choose scraper strategy
- [ ] Implement `ISourceScraper` in `/src/crawler/sources/`
- [ ] Add CSS selector or extraction config to isolate content zone
- [ ] Set noise threshold for diff detection
- [ ] Add source record to Cosmos DB `sources` container
- [ ] Add to Terraform Container App cron configuration
- [ ] Manual test run — verify diff detection works on known-good vs known-changed content
- [ ] Document in this file
