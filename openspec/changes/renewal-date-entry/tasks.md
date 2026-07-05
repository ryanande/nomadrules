## 1. Migration

- [x] 1.1 Add `Scripts/V004__renewal_days.sql` to `NomadRules.DbMigrations`: nullable `insurance_renewal_day`, `registration_renewal_day`, `license_renewal_day`, `tax_due_day` (INTEGER)
- [x] 1.2 Run migration runner locally; confirm the four columns + `SchemaVersions` has V004

## 2. Subscriber API

- [x] 2.1 Add `*Day` fields to `RegisterRequest`, `UpdateProfileRequest`, `Subscriber` (`Features/Subscribers/Models.cs`)
- [x] 2.2 `SubscriberService.cs`: include the day columns in INSERT and the COALESCE UPDATE
- [x] 2.3 Validate at the boundary: reject a `*Day` that isn't a valid calendar day for its `*Month`, or a day with no month (400 + clear message); clamp Feb 29 handling per design
- [x] 2.4 Return `*Day` in the subscriber response

## 3. Email Delivery

- [x] 3.1 `RenewalTriggers.Anchor(int month, int? day, DateOnly today)` → `new DateOnly(anchorYear, month, day ?? 1)`, same year-roll
- [x] 3.2 Thread the day through `SubscriberRow` + the per-category read (`SubscribersWithRenewalAsync`)
- [x] 3.3 Alert copy names the date when a day is known; keeps "around <Month>" when it isn't
- [x] 3.4 Extend `--selfcheck`: day-accurate anchor, null-day fallback to the 1st, year-roll with a day

## 4. Portal

- [x] 4.1 `Profile.tsx`: replace each month `<select>` with a native `<input type="date">`; map picked date → month+day (year ignored), and month+day → a date value for display
- [x] 4.2 `lib/api.ts`: add `*Day` to the renewal types and the update payload
- [x] 4.3 Label fields so it's clear only month/day are stored (year ignored)

## 5. Verification

- [x] 5.1 Migration applies; four day columns present
- [x] 5.2 `--selfcheck` passes (day anchor + fallback + roll)
- [x] 5.3 API: register/update with a valid day stores it; invalid day (e.g. month=2 day=30, or day without month) → 400
- [x] 5.4 Seed a subscriber with a renewal month+day exactly 60 days out; `--run-now` sends on that exact day (not the 1st)
- [x] 5.5 Seed a subscriber with month only (no day); behavior identical to today (anchors to the 1st)
- [x] 5.6 Portal: date input round-trips month+day through the API
