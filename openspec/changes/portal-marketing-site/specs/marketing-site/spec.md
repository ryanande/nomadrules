## ADDED Requirements

### Requirement: Landing page communicates the value proposition
The marketing home page SHALL present the core promise — renewal-anchored legal & regulatory monitoring for full-time RVers and digital nomads — through a hero, a story-driven value narrative, and a "how it works" section.

#### Scenario: Visitor reads the core promise
- **WHEN** a visitor loads the home page
- **THEN** a hero states the core promise and primary call to action above the fold

#### Scenario: Visitor understands how it works
- **WHEN** a visitor scrolls the home page
- **THEN** a "how it works" section explains detection → plain-English summary → renewal-anchored alert

### Requirement: Marketing pages build confidence with trust signals
The marketing site SHALL include trust and confidence signals — such as concrete sample artifacts (a digest/alert example), coverage clarity (insurance-first, states covered), and human, plain-language copy targeted at full-time RVers.

#### Scenario: Visitor sees a concrete example of the deliverable
- **WHEN** a visitor views the product/value detail page
- **THEN** a representative sample of a digest or renewal alert is shown, including the 60/30/7-day timeline

#### Scenario: Coverage is stated honestly
- **WHEN** a visitor views coverage information
- **THEN** current category and state coverage is stated plainly (no overstated claims)

### Requirement: Marketing site captures leads into the acquisition funnel
The marketing site SHALL provide an email-capture call to action that submits a visitor's email to a lead-capture endpoint and confirms submission.

#### Scenario: Visitor submits their email
- **WHEN** a visitor enters a valid email and submits the capture CTA
- **THEN** the email is posted to the lead-capture endpoint and a confirmation is shown

#### Scenario: Invalid email is rejected client-side
- **WHEN** a visitor submits an empty or malformed email
- **THEN** an inline validation message is shown and no request is sent

### Requirement: Marketing site is responsive and accessible
The marketing site SHALL render usably on mobile and desktop viewports and meet accessibility basics (semantic landmarks, keyboard-navigable nav, sufficient contrast, alt text on meaningful images).

#### Scenario: Mobile visitor navigates the site
- **WHEN** a visitor loads the marketing site on a mobile-width viewport
- **THEN** navigation and content are usable without horizontal scrolling

### Requirement: Shared marketing chrome
The marketing pages SHALL share a consistent header (nav to home, product, pricing, business, contact, plus sign-in) and footer.

#### Scenario: Visitor navigates between marketing pages
- **WHEN** a visitor uses the header navigation
- **THEN** they can reach home, product/value, pricing, business, and contact pages, and the sign-in CTA

### Requirement: Imagery uses free, licensed stock photography
Where the marketing site uses photographic imagery, it SHALL use free, properly-licensed stock photos (e.g. Unsplash/Pexels) with license/attribution tracked in the repo, and images SHALL be optimized and lazy-loaded below the fold.

#### Scenario: Imagery is licensed and tracked
- **WHEN** a photographic image is added to the marketing site
- **THEN** its source and license permit commercial use and the attribution/license is recorded

#### Scenario: Below-the-fold images are lazy-loaded
- **WHEN** a visitor loads a marketing page with imagery below the fold
- **THEN** those images are lazy-loaded rather than blocking initial render
