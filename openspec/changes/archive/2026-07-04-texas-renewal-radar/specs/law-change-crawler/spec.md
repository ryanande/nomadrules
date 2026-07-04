# Law Change Crawler Specification

## ADDED Requirements

### Requirement: Crawler scrapes TX Division of Insurance bulletins
The system SHALL automatically check the TX Division of Insurance bulletins page daily and detect when content changes.

#### Scenario: Crawler fetches page
- **WHEN** cron trigger fires at 2 AM UTC daily
- **THEN** system fetches https://dlr.sd.gov/insurance/bulletins.aspx
- **AND** waits for all JavaScript to load (Playwright headless mode)
- **AND** extracts text content from the main content area

#### Scenario: Crawler detects changes
- **WHEN** fetched content is different from previous day's snapshot
- **THEN** system calculates a hash of the content
- **AND** compares hash to stored previous hash
- **AND** if different (excluding noise like dates/whitespace), inserts record into law_changes table: { source_id: "tx-insurance", url, raw_content, detected_at }

#### Scenario: No change detected
- **WHEN** content hash matches previous day
- **THEN** system does NOT create a law_change record
- **AND** updates sources table: { last_checked_at: NOW() }

#### Scenario: Scraper encounters error
- **WHEN** page fails to load or JavaScript times out
- **THEN** system logs error with source_id, URL, error message
- **AND** sends alert to Ryan (email or Slack integration)
- **AND** does NOT create a false "change detected" record

### Requirement: Crawler stores snapshots
The system SHALL store previous page snapshots so diffs can be detected.

#### Scenario: Snapshot is stored
- **WHEN** crawler finishes fetching a page
- **THEN** system stores snapshot to Azure Blob or local file: `snapshots/tx-insurance-{date}.html`

#### Scenario: Snapshot is used for diff
- **WHEN** next day's crawl completes
- **THEN** system reads previous snapshot from blob/file
- **AND** diffs previous vs. current

### Requirement: Crawler handles JS-rendered content
The system SHALL use Playwright (headless browser) to load pages with JavaScript, not just static HTML.

#### Scenario: Page with JS content loads
- **WHEN** TX Division of Insurance page uses JavaScript to load bulletins
- **THEN** system waits for content to render (via Playwright)
- **AND** captures fully rendered content (not just static HTML)

### Requirement: Crawler is single-source (insurance-only for v0.1)
The system SHALL monitor only TX Division of Insurance in v0.1. Tax, DMV, voting sources added after insurance is validated.

#### Scenario: Crawler only monitors insurance in v0.1
- **WHEN** cron trigger fires
- **THEN** system crawls TX Division of Insurance ONLY
- **AND** does NOT crawl TX Comptroller, TX DMV, or other sources

### Requirement: Crawler is scheduled daily
The system SHALL run once per day on a fixed schedule.

#### Scenario: Crawler runs on schedule
- **WHEN** Azure Container Apps cron fires at 2 AM UTC daily
- **THEN** crawler executes, completes within 5 minutes
- **AND** logs completion status (success/failure)

#### Scenario: Scheduled run fails gracefully
- **WHEN** cron execution fails (container crash, timeout)
- **THEN** Container Apps automatically retries (up to 3 times)
- **AND** alerts Ryan after 3 failures

### Requirement: Rate limiting
The system SHALL NOT hammer government websites with requests.

#### Scenario: Crawler respects robots.txt and rate limits
- **WHEN** crawler fetches government page
- **THEN** system checks robots.txt
- **AND** delays between requests (min 1 second)
- **AND** sets User-Agent header
- **AND** respects 429 (Too Many Requests) responses

### Requirement: Diff detection filters noise
The system SHALL ignore cosmetic changes (timestamps, whitespace, navigation changes) and only flag content changes.

#### Scenario: Cosmetic changes are ignored
- **WHEN** page is refetched and only the "Last updated: [date]" changes
- **THEN** system does NOT flag as a change
- **AND** compares semantic content, not exact HTML

#### Scenario: Actual content changes are detected
- **WHEN** new bulletin paragraph is added to the page
- **THEN** system flags as a change
- **AND** creates law_change record
