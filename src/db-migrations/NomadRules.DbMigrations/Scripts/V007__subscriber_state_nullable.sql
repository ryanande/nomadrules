-- A subscriber JIT-provisioned on first Entra login has no domicile until they pick one during onboarding,
-- so subscribers.state must be able to be NULL (previously NOT NULL DEFAULT 'TX', which silently labeled
-- every new signup as Texan). Existing rows are untouched: they all signed up when TX was the only option,
-- so their stored 'TX' is correct. law_changes.state is unchanged (the crawler always supplies it).
ALTER TABLE subscribers ALTER COLUMN state DROP DEFAULT;
ALTER TABLE subscribers ALTER COLUMN state DROP NOT NULL;

-- The anonymous register endpoint previously accepted any non-blank state. Rows holding a state the product
-- doesn't support would never see the onboarding picker (it only shows for NULL) and would get an empty feed,
-- so send them back through onboarding.
UPDATE subscribers SET state = NULL WHERE state IS NOT NULL AND UPPER(state) NOT IN ('TX', 'SD');
