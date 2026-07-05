-- Day-of-month for each renewal, paired with the existing *_renewal_month. Nullable: a NULL day means
-- "anchor to the 1st" (the prior month-only behavior), so existing subscribers are unaffected. See
-- renewal-date-entry design Decision 1. Validity (a real calendar day for the month) is enforced at the
-- subscriber-API boundary before storage.
ALTER TABLE subscribers ADD COLUMN insurance_renewal_day INTEGER;
ALTER TABLE subscribers ADD COLUMN registration_renewal_day INTEGER;
ALTER TABLE subscribers ADD COLUMN license_renewal_day INTEGER;
ALTER TABLE subscribers ADD COLUMN tax_due_day INTEGER;
