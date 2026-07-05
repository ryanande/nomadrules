## Why

The renewal alert is the product wedge — "your insurance renews in 60 days." But today renewal dates are stored **month-only** (`subscribers.*_renewal_month`, 1-12), and email-delivery anchors every renewal to the **1st of the month**. A subscriber whose real renewal is the 20th gets the 60/30/7-day alerts ~19 days early. The email-delivery design (Decision 1) explicitly shipped month-anchoring as an MVP shortcut and deferred day-accuracy to this change: "When we add full-date entry, only the anchor computation changes."

This makes the alerts land on the real cadence subscribers are paying for.

## What Changes

- Add a **day** alongside each renewal month, so renewals are captured as month+day (the natural annual unit — the year rolls each cycle).
- **Backward compatible:** existing subscribers have no day → the anchor keeps falling back to the 1st (exactly today's behavior). Only subscribers who enter a day get day-accurate triggers.
- Portal profile captures the day via a **native `<input type="date">`** (no picker dependency).
- Subscriber API accepts and stores the day, validating it's a real calendar day for the chosen month.
- Email-delivery's anchor uses the day when present (`new DateOnly(year, month, day ?? 1)`); trigger offsets and idempotency are untouched.
- Alert copy still says "around <Month>" only when the day is unknown; with a day it can name the date.
- Insurance-only / TX MVP scope unchanged.

## Capabilities

### New Capabilities

<!-- No new capability; this refines existing behavior across two specs. -->

### Modified Capabilities

- `email-delivery`: the renewal-alert anchor becomes day-accurate when a renewal day is known, replacing the always-1st-of-month approximation (design Decision 1 trade-off).

<!-- The subscriber-api and portal changes (accept/validate/capture a renewal day) are supporting
     implementation for the above. subscriber-api has no baseline spec yet, so there is no delta to emit
     against it here; that pre-existing baseline gap is out of scope for this change. -->


## Impact

- New migration `V004__renewal_days.sql` — adds nullable `insurance_renewal_day`, `registration_renewal_day`, `license_renewal_day`, `tax_due_day` (INTEGER 1-31).
- Subscriber API: `RegisterRequest` / `UpdateProfileRequest` / `Subscriber` (`Features/Subscribers/Models.cs`) gain `*Day` fields; `SubscriberService.cs` INSERT/UPDATE + validation.
- Portal: `Profile.tsx` swaps the month `<select>` per field for a native date input capturing month+day; `lib/api.ts` types.
- Email-delivery: `RenewalTriggers.Anchor` gains an optional day; `SubscriberRow` + the per-category read carry the day. Trigger/idempotency logic unchanged.
- No breaking changes — day is additive and nullable everywhere.
