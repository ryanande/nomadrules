# Law Change Summarizer Specification

## ADDED Requirements

### Requirement: Claude API summarizes raw law changes
The system SHALL use Claude API to convert raw HTML/text of a law change into a plain-English summary.

#### Scenario: New law change is summarized
- **WHEN** law_change record is created with raw_content but headline IS NULL
- **THEN** system calls Claude API with the raw_content
- **AND** sends a prompt asking for: headline (one sentence), summary (2-3 sentences), severity (urgent/routine/informational)

#### Scenario: Claude response is formatted
- **WHEN** Claude returns response
- **THEN** system parses JSON response: { "headline": "...", "summary": "...", "severity": "..." }
- **AND** updates law_change record: headline, summary, severity, processed_at = NOW()

#### Scenario: Summary is human-readable
- **WHEN** summary is generated
- **THEN** summary uses plain English (no legalese)
- **AND** summary explains WHO is affected and WHY
- **AND** summary is 2-3 sentences maximum

### Requirement: Severity scoring
The system SHALL assign severity to law changes: urgent, routine, informational.

#### Scenario: Urgent severity
- **WHEN** law change affects insurance minimums, fees, or coverage
- **THEN** system assigns severity = "urgent"
- **AND** this triggers immediate email delivery (not waiting for digest)

#### Scenario: Routine severity
- **WHEN** law change is procedural or informational (e.g., "form updated")
- **THEN** system assigns severity = "routine"
- **AND** this is batched into weekly digest

#### Scenario: Informational severity
- **WHEN** law change is background context (e.g., "bill proposed, not yet voted on")
- **THEN** system assigns severity = "informational"
- **AND** this is included in digest but not emphasized

### Requirement: Prompts are versioned
The system SHALL store summaries prompts in code repository, enabling iteration without redeployment.

#### Scenario: Prompt is read from file
- **WHEN** summarizer function starts
- **THEN** system reads prompt from `src/processor/Prompts/SummarizeInsuranceChange.txt`
- **AND** uses that prompt text for Claude API call

#### Scenario: Prompt is updated
- **WHEN** Ryan commits updated prompt to repo (e.g., "add emphasis on RV-specific impacts")
- **THEN** new deployments use the updated prompt automatically
- **AND** old law_change summaries are NOT re-summarized (they keep original summary)

### Requirement: Error handling
The system SHALL handle Claude API failures gracefully.

#### Scenario: Claude API timeout
- **WHEN** Claude API times out (>30 sec)
- **THEN** system retries up to 2 times with exponential backoff
- **AND** if still failing, inserts law_change with headline = "[Unable to summarize - raw content follows]"
- **AND** stores raw_content for manual review

#### Scenario: Claude API quota exceeded
- **WHEN** Claude API returns 429 (quota exceeded)
- **THEN** system queues law_change for retry
- **AND** waits 1 hour before next attempt
- **AND** alerts Ryan: "Claude quota exceeded, queue building"

#### Scenario: Invalid Claude response
- **WHEN** Claude response is not valid JSON
- **THEN** system logs error (with response body)
- **AND** marks law_change as failed: processed_at = NULL, retry_count = retry_count + 1
- **AND** does NOT crash the function

### Requirement: Summary quality gates (v0.1)
The system SHALL require manual review of first 10 summaries before auto-delivery.

#### Scenario: First 10 summaries require approval
- **WHEN** first 10 law_change records are summarized
- **THEN** system does NOT automatically email these to subscribers
- **AND** sets flag: reviewed = FALSE
- **AND** sends notification to Ryan + Jenn: "First 10 summaries ready for review"

#### Scenario: Approved summaries are delivered
- **WHEN** Ryan + Jenn approve first 10 summaries (mark reviewed = TRUE)
- **THEN** system sends those summaries to matching subscribers
- **AND** subsequent summaries skip manual review (auto-deliver)

#### Scenario: Poor quality triggers pivot
- **WHEN** manual review judges summaries as "not good enough"
- **THEN** team discusses: rewrite prompt? switch to insurance-only? manual summarization?
- **AND** decision is documented in assumptions.md

### Requirement: Hallucination detection
The system SHALL flag summaries that appear to contain legal hallucinations.

#### Scenario: Obvious hallucination is flagged
- **WHEN** Claude summary contains claim not present in raw_content
- **THEN** during manual review, Jenn marks as "hallucination"
- **AND** system logs this case (for prompt tuning)

### Requirement: Cost monitoring
The system SHALL track Claude API cost per summary.

#### Scenario: Cost is logged
- **WHEN** Claude API call completes
- **THEN** system logs: { law_change_id, tokens_used, cost_usd, timestamp }
- **AND** Ryan can query monthly cost: "How much did we spend on Claude this month?"

### Requirement: Insurance-only prompting (v0.1)
The system SHALL use an insurance-specific prompt, not a generic legal summarizer.

#### Scenario: Insurance prompt is used
- **WHEN** summarizer processes law_change
- **THEN** prompt is `SummarizeInsuranceChange.txt` (not generic legal prompt)
- **AND** prompt includes context: "You are helping full-time RVers understand insurance regulation changes"
- **AND** prompt asks for: "How does this affect RV owners in Texas?"
