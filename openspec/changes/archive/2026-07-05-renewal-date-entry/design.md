## Context

Renewal dates are stored month-only: `subscribers.insurance_renewal_month`, `registration_renewal_month`, `license_renewal_month`, `tax_due_month` (INTEGER 1-12, V001). The portal (`Profile.tsx`) captures them via month `<select>` dropdowns; the subscriber API (`Features/Subscribers/`) reads/writes them; email-delivery's `RenewalTriggers.Anchor(int month, DateOnly today)` maps a month to the 1st of that month for 60/30/7-day offset math.

The email-delivery design (Decision 1) chose month-anchoring for MVP speed and explicitly scoped the fix here: "only the anchor computation changes — trigger logic and idempotency are unaffected."

## Goals / Non-Goals

**Goals:**
- Renewal alerts fire relative to the subscriber's real renewal **day**, not always the 1st.
- Fully backward compatible: subscribers with no day keep today's behavior exactly.
- One native date input in the portal; no new frontend dependency.
- Server-side validation that a submitted day is a real calendar day for its month.

**Non-Goals:**
- Storing a full date with year (renewals recur annually; year is derived per cycle).
- Changing trigger offsets (still 60/30/7) or idempotency (still per subscriber/category/offset/year).
- Multi-state / multi-category expansion.
- Reconstructing a `subscriber-api` baseline spec (pre-existing gap, out of scope).

## Decisions

### Decision 1: Add a nullable `*_renewal_day`, keep `*_renewal_month`

**Chosen:** Four new nullable columns `insurance_renewal_day`, `registration_renewal_day`, `license_renewal_day`, `tax_due_day` (INTEGER 1-31), each paired with its existing month. Migration `V004__renewal_days.sql`.

**Alternatives:**
- Replace month with a full `DATE` — breaks existing rows and the year-agnostic recurrence; forces a data backfill.
- Store a single `MM-DD` string — stringly-typed, needs parsing everywhere; two INTEGERs are simpler to validate and query.

**Rationale:** A renewal is an annual event, so month+day is the complete unit; the year is always "this year or next." Keeping month and adding day means zero migration of existing data and a trivial fallback (`day ?? 1`). Nullable day is the compatibility hinge.

**Trade-off:** Two columns per category instead of one. Cheap; mirrors how the data is actually entered.

### Decision 2: Anchor uses `day ?? 1`

**Chosen:** `RenewalTriggers.Anchor(int month, int? day, DateOnly today)` builds `new DateOnly(anchorYear, month, day ?? 1)` — same year-roll logic as today. When day is null, the result is identical to the current 1st-of-month anchor.

**Rationale:** This is the single behavioral change the email-delivery design predicted. `DueOffset`, reservation, idempotency keys, and templates are untouched. A null day is not a special case — it's just `day = 1`.

**Trade-off:** A day beyond the month's length (e.g. Feb 30) must be prevented before it reaches the anchor (see Decision 3), since `new DateOnly` would throw.

### Decision 3: Validate the day at the API boundary

**Chosen:** The subscriber API rejects a `*Day` that isn't a valid calendar day for its `*Month` (e.g. day 31 with month 2, or a day without a month). Validation lives at the trust boundary (registration + profile update), returning a 400 with a clear message; the DB stores only validated values.

**Rationale:** The anchor's `new DateOnly(...)` throws on an impossible date; validating at the boundary keeps bad data out of the DB entirely, so email-delivery never has to defend against it. A day supplied without a month is meaningless and rejected.

**Trade-off:** Duplicated "valid day for month" check isn't shared with the worker — acceptable; the worker trusts validated storage, and a stored-then-invalid case (month cleared, day kept) is defended by the anchor falling back safely.

### Decision 4: Portal uses a native `<input type="date">`

**Chosen:** Replace each month `<select>` with a native date input; extract month+day from the picked date (year ignored). ponytail: native platform feature over a picker library.

**Rationale:** The browser's date input is accessible, localized, and dependency-free. We only persist month+day, so the chosen year is discarded on submit.

**Trade-off:** The user picks a specific year in the UI that we don't store; the field is labeled to make clear only month/day matter (e.g. "renewal date — year ignored").

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Impossible date (Feb 30) reaches the anchor and throws | API validation (Decision 3) rejects it before storage |
| Month cleared but day left set | Anchor keys off month; a null month means no alert for that category (existing behavior). A stray day with null month is inert |
| Existing subscribers silently keep 1st-of-month | Intended and documented — day is opt-in; no behavior change until a day is entered |
| Leap-day renewal (Feb 29) in a non-leap year | Anchor clamps or rolls — validated as a real day; non-leap-year anchoring resolves to the nearest valid instance (define in impl: clamp to Feb 28) |

## Migration Plan

1. `V004__renewal_days.sql` — add the four nullable day columns.
2. Subscriber API — add `*Day` to request/response models, INSERT/UPDATE, and boundary validation.
3. Email-delivery — thread day through `SubscriberRow` + the per-category read; `Anchor` takes `int? day`.
4. Portal — swap month selects for native date inputs; map to/from month+day; update `lib/api.ts` types.
5. Verify: a subscriber with a day 60 days out triggers on the exact day; one with only a month is unchanged (1st).
