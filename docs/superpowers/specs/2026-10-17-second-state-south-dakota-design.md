# Design: Finish wiring South Dakota as the second state

**Date:** 2026-10-17
**Status:** Proposed

## Summary

NomadRules v0.1 is scoped to Texas-only, insurance-only (CLAUDE.md principle #12). The crawler already runs a South Dakota insurance scraper (`SdInsuranceBulletinsScraper`, registered in `src/crawler/src/index.ts`) alongside the Texas one, and the DB schema, email templates, and digest-matching query are already state-generic. What's missing is three small seams that still assume Texas is the only state: the summarizer's prompt, the live subscriber-signup path, and the portal's lack of any state picker. This change closes those three seams so SD subscribers get correctly-labeled summaries and are no longer silently miscategorized as Texan.

Category stays insurance-only. No new crawler work is needed — SD scraping is already live.

## Non-goals

- No third state. No multi-category expansion.
- No backfill/migration of existing subscriber rows — every subscriber created before this change signed up when TX was the only option, so their stored `state = 'TX'` is factually correct, not a mis-default to fix.
- No change to the crawler, DB schema for `law_changes`, or email templates — audited and confirmed already state-generic.
- No central "supported states" registry shared across crawler/API. The allow-list is a small constant in the API (matching the existing pattern of just-enough state tracking); revisit if a third state arrives.

## Current gaps (confirmed by code audit)

1. **Summarizer prompt is Texas-only.** `src/summarizer/NomadRules.Summarizer/Summarization/Models.cs`'s `LawChangeRow` has no `State` field, and `Prompts/SummarizeInsuranceChange.txt` hardcodes "Texas" four times. The `law_changes.state` column already exists and is already populated correctly by the crawler/ingest path — the summarizer just never reads it.
2. **Every new signup is hardcoded to `'TX'`.** `src/api/NomadRules.Api/Features/Auth/AuthService.cs`'s `ResolveOrProvisionAsync` inserts `state = 'TX'` unconditionally when JIT-provisioning a new subscriber on first Entra login. There is no path today for a subscriber to end up as anything else.
3. **Portal has no state picker anywhere.** `Profile.tsx` shows `Domicile: {subscriber.state}` as read-only text. There is no onboarding step and no way to change domicile.

## Design

### 1. Summarizer: pass state into the prompt

- Add `State` to `LawChangeRow` (`Summarization/Models.cs`) and to the `SELECT` in `LawChangeRepository.ClaimNextAsync` (`state AS State` — column already exists).
- Add a small static map `{ "TX" => "Texas", "SD" => "South Dakota" }` (prompt reads better in prose with the full name than the abbreviation). Lives next to `LawChangeRow` since it's summarizer-prompt-only concern.
- Template `SummarizeInsuranceChange.txt` with a `{State}` placeholder in place of the 4 "Texas" occurrences.
- `ClaudeSummarizer.SummarizeAsync` substitutes `{State}` with the mapped full name before sending the prompt to Claude, using the `LawChangeRow.State` claimed for that row.

### 2. API: stop hardcoding signup state; let it be set post-login

- **Migration** (`V007__subscriber_state_nullable.sql`): drop the `NOT NULL DEFAULT 'TX'` constraint on `subscribers.state` so a freshly JIT-provisioned row can exist with `state = NULL` until the subscriber picks one. (`law_changes.state` is untouched — crawler always supplies it.)
- `AuthService.ResolveOrProvisionAsync`: change the INSERT to omit state (column defaults to `NULL` post-migration) instead of hardcoding `'TX'`.
- New allow-list constant, e.g. `SupportedStates.Codes = ["TX", "SD"]`, in the API project.
- New endpoint `PUT /api/subscribers/{id}/state` (kept separate from the existing renewal-dates `PUT /profile`, which has established full-replace semantics for the renewal fields only — bolting state onto that request shape would force every renewal-date save to also resend the current state). Body: `{ state: string }`. Validates against the allow-list (400 `invalid_state` otherwise), requires the `SubscriberTenant` auth policy + the existing `IsAuthorizedAsync` ownership check, updates the row, returns the updated `Subscriber`.
- `SubscriberEndpoints.Register`'s existing state validation (currently just "non-empty") is tightened to the same allow-list check, for consistency — it's a second entry point that creates subscriber rows with a caller-supplied state.
- `LawChangeService.GetFeedAsync` is called with `sub.State`; if `State` is `null` (pre-onboarding), the query's `state = @state` comparison against `NULL` returns zero rows rather than throwing — acceptable since the portal gates the feed behind onboarding (see below), but worth a one-line comment noting it's intentionally inert rather than defended against.

### 3. Portal: one-time state picker before the feed

- `App.tsx`'s `loadMe()` already receives the full `Subscriber` (including `state`) from `api.me()`. Add a check: if `sub.state` is `null`, render a new `SelectState` page instead of `Profile`/`Feed`.
- New `src/portal/src/pages/SelectState.tsx`: a short "Where are you domiciled?" screen with a dropdown (Texas / South Dakota — matches the API's allow-list, hand-written since there are only two) and a Save button that calls a new `api.setState(id, state)` (wired to `PUT /api/subscribers/{id}/state`), then re-fetches `/api/auth/me` (or just updates local state) to proceed into the normal Profile/Feed view.
- `lib/api.ts`: add `setState: (id: string, state: string) => request<Subscriber>(...)`.
- Not building a "change your domicile later" affordance on `Profile.tsx` in this change — onboarding-only, matches what was asked for. (Flagging as a natural follow-up, not building it now — YAGNI.)

## Data flow after this change

```
Entra sign-in → /api/auth/me → JIT-provision (state = NULL) →
  portal sees state == null → SelectState screen → PUT /api/subscribers/{id}/state →
  portal proceeds to Profile/Feed, same as today
```

Existing TX subscribers are unaffected — their `state` is already `'TX'`, non-null, so they skip straight past the new check exactly as before.

## Testing

- API: unit tests for `AuthService.ResolveOrProvisionAsync` (new subscriber gets `state = NULL`, not `'TX'`), the new state-allow-list validation (reject e.g. `"CA"`), and the new endpoint's auth/ownership check (reuses existing `IsAuthorizedAsync` pattern — same test shape as `UpdateProfile`).
- Summarizer: unit test that `ClaudeSummarizer` substitutes the correct full state name into the prompt for both `TX` and `SD` rows, and a repository test that `ClaimNextAsync` returns the row's state.
- Portal: no existing test suite found for pages (`Profile.tsx`/`Feed.tsx` are untested today) — match existing practice, no new test infra introduced for `SelectState.tsx`.
- Manual verification: sign up a fresh subscriber end-to-end, confirm the state picker appears, confirm a SD-sourced law change gets summarized with "South Dakota" in the text, confirm the SD subscriber's feed only shows SD items.

## Rollout notes

This is backend + portal only — no infra/Terraform/Helm changes. Ships as a normal PR; no live deploy window needed (unlike the still-open Entra auth cutover). The migration is additive/loosening (drops a constraint), so it's safe to apply ahead of the API deploy that uses it.
