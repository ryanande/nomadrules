-- Reference data: the scopes a law change can belong to. 'US' is the scope for federal changes, shown to
-- every subscriber; it is not a domicile a subscriber can pick. is_domicile marks the states subscribers
-- can choose during onboarding. Adding a state is an INSERT here.
-- Nothing reads this table yet: the API allow-list, summarizer name map and portal picker still carry their
-- own copies and should be pointed at it in a follow-up.
CREATE TABLE states (
  code TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  is_domicile BOOLEAN NOT NULL
);

INSERT INTO states (code, name, is_domicile) VALUES
  ('TX', 'Texas', TRUE),
  ('SD', 'South Dakota', TRUE),
  ('US', 'United States', FALSE);
