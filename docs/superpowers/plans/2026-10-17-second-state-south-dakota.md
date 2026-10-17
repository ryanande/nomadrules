# Second State (South Dakota) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the three seams that still assume Texas is the only state, so South Dakota subscribers get correctly labeled summaries and are no longer silently assigned `'TX'` at signup.

**Architecture:** Summarizer reads `law_changes.state` and substitutes a full state name into a templated prompt. API stops hardcoding `'TX'` on JIT provisioning (column becomes nullable), validates against an API-local `SupportedStates` allow-list, and gains `PUT /api/subscribers/{id}/state`. Portal shows a one-time `SelectState` screen when `subscriber.state` is `null`. Email-delivery is intentionally untouched (a NULL `state` matches zero rows and the worker skips empty digests).

**Tech Stack:** .NET 10 / ASP.NET Core / Dapper / Npgsql, DbUp SQL migrations, xUnit (new), React + TypeScript + Vite.

**Spec:** `docs/superpowers/specs/2026-10-17-second-state-south-dakota-design.md`

## Global Constraints

- Insurance-only; supported states are exactly `TX` and `SD` (CLAUDE.md principle #12; spec Non-goals: no third state, no multi-category).
- No backfill of existing subscriber rows; existing `state = 'TX'` rows are correct and must be left untouched.
- No change to crawler, `law_changes` schema, email-service, or email templates.
- No shared cross-service state registry: the allow-list is a constant in the API project only.
- State codes are stored uppercase (`TX`, `SD`); input is accepted case-insensitively and normalized with `ToUpperInvariant()`.
- Migration naming: `V00N__description.sql` with a leading comment header; next number is `V007`.
- Dapper record materialization needs exact column types (`LawChangeRow` stays a positional record; `State` is `string`).
- Portal: no new test infra (spec Testing); no "change domicile later" UI.
- Local Postgres: `Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules` (`infra/docker-compose.yml`). Migrations run from `src/db-migrations/NomadRules.DbMigrations` with `POSTGRES_CONNECTION_STRING=<conn> ASPNETCORE_ENVIRONMENT=Development dotnet run`.

## Review Focus

1. Existing TX subscribers must skip onboarding: `GET /api/auth/me` for a row with `state = 'TX'` returns `state: "TX"` and the portal does not render `SelectState` (manual check, Task 7).
2. A never-onboarded subscriber (`state` NULL) must get an empty feed, not a 500: `LawChangeService.GetFeedAsync(null, ...)` returns `([], 0)` (test in Task 3).
3. `POST /api/subscribers` must reject unsupported states such as `"CA"` with 400 `invalid_state` (curl check in Task 4; allow-list unit test in Task 2).
4. `PUT /api/subscribers/{id}/state` must 403 for a token whose `oid` does not own `{id}`, and 400 for `"CA"` (curl check in Task 4; no HTTP harness exists).
5. Lowercase input (`"sd"`) is accepted and stored as `SD`; an unmapped state code in the summarizer prompt falls back to the code rather than throwing (tests in Tasks 2, 4, 5).

---

### Task 1: Migration — make `subscribers.state` nullable

**Files:**
- Create: `src/db-migrations/NomadRules.DbMigrations/Scripts/V007__subscriber_state_nullable.sql`

**Interfaces:**
- Produces: `subscribers.state` is `TEXT NULL` with no default. Tasks 3-4 tests rely on it.

- [ ] **Step 1: Create `V007__subscriber_state_nullable.sql`** with a comment header explaining that a JIT-provisioned subscriber has no domicile until onboarding, then two statements: `ALTER TABLE subscribers ALTER COLUMN state DROP DEFAULT;` and `ALTER TABLE subscribers ALTER COLUMN state DROP NOT NULL;`. Do not touch `law_changes.state` or existing rows.
- [ ] **Step 2: Start Postgres and apply**

```bash
docker compose -f infra/docker-compose.yml up -d
cd src/db-migrations/NomadRules.DbMigrations
POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules" ASPNETCORE_ENVIRONMENT=Development dotnet run
```
Expected: runner reports V007 applied, exit 0.
- [ ] **Step 3: Verify the column**

```bash
docker compose -f infra/docker-compose.yml exec -T postgres psql -U nomadrules -d nomadrules_dev -c "\d subscribers" | grep ' state '
```
Expected: `state | text | | |` (no `not null`, no default). If the compose service is not named `postgres`, use the name from `infra/docker-compose.yml`.
- [ ] **Step 4: Commit**

```bash
git add src/db-migrations/NomadRules.DbMigrations/Scripts/V007__subscriber_state_nullable.sql
git commit -m "feat(db): make subscribers.state nullable for post-signup onboarding"
```

---

### Task 2: Bootstrap `Api.Tests` and add `SupportedStates`

**Files:**
- Create: `src/api/NomadRules.Api.Tests/` (via `dotnet new xunit`), `TestDb.cs`, `SupportedStatesTests.cs`
- Create: `src/api/NomadRules.Api/Features/Subscribers/SupportedStates.cs`
- Delete: the template `UnitTest1.cs`
- Modify: `NomadRules.slnx` (add the test project)

**Interfaces:**
- Produces:
  - `NomadRules.Api.Features.Subscribers.SupportedStates` — `public static readonly string[] Codes = ["TX", "SD"]`; `public static bool IsSupported(string? state)` (case-insensitive, false for null/empty/unknown).
  - `NomadRules.Api.Tests.TestDb` — `public static Db Create()` building `Db` from in-memory config key `ConnectionStrings:Postgres`, defaulting to the local dev string above, overridable by env var `NOMADRULES_TEST_PG`.

- [ ] **Step 1: Scaffold**

```bash
cd src/api
dotnet new xunit -n NomadRules.Api.Tests
dotnet add NomadRules.Api.Tests reference NomadRules.Api/NomadRules.Api.csproj
dotnet sln ../../NomadRules.slnx add NomadRules.Api.Tests/NomadRules.Api.Tests.csproj --solution-folder src/api/
rm NomadRules.Api.Tests/UnitTest1.cs
```
Expected: project targets `net10.0`; `dotnet build NomadRules.Api.Tests` succeeds.
- [ ] **Step 2: Write `TestDb.cs`** per the Interfaces block above.
- [ ] **Step 3: Write failing `SupportedStatesTests.cs`** — `[Theory]` `IsSupported` true for `"TX"`, `"SD"`, `"sd"`, `"Tx"`; `[Theory]` false for `"CA"`, `""`, `"  "`, `null`; `[Fact]` `Codes` equals `["TX","SD"]`.
- [ ] **Step 4: Run to verify it fails**

Run: `dotnet test src/api/NomadRules.Api.Tests`
Expected: compile FAIL, `SupportedStates` not defined.
- [ ] **Step 5: Implement `SupportedStates`** in `Features/Subscribers/SupportedStates.cs` (static class, signatures above, `Codes.Contains(state, StringComparer.OrdinalIgnoreCase)`).
- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test src/api/NomadRules.Api.Tests`
Expected: all `SupportedStatesTests` PASS.
- [ ] **Step 7: Commit**

```bash
git add NomadRules.slnx src/api/NomadRules.Api.Tests src/api/NomadRules.Api/Features/Subscribers/SupportedStates.cs
git commit -m "feat(api): add SupportedStates allow-list and bootstrap Api.Tests"
```

---

### Task 3: Stop hardcoding `'TX'`; allow a null state end to end

**Files:**
- Modify: `src/api/NomadRules.Api/Features/Auth/AuthService.cs:49-52`
- Modify: `src/api/NomadRules.Api/Features/Subscribers/Models.cs:31` (`Subscriber.State`)
- Modify: `src/api/NomadRules.Api/Features/LawChanges/LawChangeService.cs` (`GetFeedAsync` parameter)
- Test: `src/api/NomadRules.Api.Tests/AuthServiceTests.cs`, `src/api/NomadRules.Api.Tests/LawChangeFeedTests.cs`

**Interfaces:**
- Consumes: `TestDb.Create()` (Task 2); `AuthService.ResolveOrProvisionAsync(string entraOid, string email, bool emailVerified)`.
- Produces: `Subscriber.State` is `string?`; `LawChangeService.GetFeedAsync(string? state, int limit, int offset)`; JIT-provisioned subscribers have `State == null`.

- [ ] **Step 1: Write failing `AuthServiceTests`** (integration, implements `IDisposable`; each test uses a unique `Guid` oid/email and deletes the row in `Dispose`):
  - `NewSubscriber_IsProvisionedWithNullState` — `ResolveOrProvisionAsync(oid, email, true)` returns a `Subscriber` with `State == null` and `Tier == "free"`.
  - `ExistingSubscriber_KeepsItsState` — insert a row directly with `state = 'TX'` and a known `entra_oid`; `ResolveOrProvisionAsync` for that oid returns `State == "TX"`.
- [ ] **Step 2: Write failing `LawChangeFeedTests`** — `GetFeedAsync(null, 20, 0)` returns `items` empty and `total == 0` (Review Focus #2).
- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test src/api/NomadRules.Api.Tests`
Expected: `NewSubscriber_IsProvisionedWithNullState` FAILS (state is `"TX"`, not null). The feed test may already pass at runtime, since `state = NULL` matches no rows; it pins the behavior against regression.
- [ ] **Step 4: Implement**
  - `Models.cs`: `public string? State { get; set; }`. `RegisterRequest.State` stays `string`.
  - `AuthService.cs`: INSERT becomes `INSERT INTO subscribers (id, email, tier, entra_oid, created_at, updated_at) VALUES (@id, @email, 'free', @entraOid, now()::text, now()::text)`.
  - `LawChangeService.GetFeedAsync(string? state, int limit, int offset)`; add one comment: a null state is intentionally inert (`state = NULL` matches no rows), the portal gates the feed behind onboarding.
- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test src/api/NomadRules.Api.Tests` and `dotnet build src/api/NomadRules.Api`
Expected: all PASS, no build errors (fix any other nullable-state warnings surfaced in `NomadRules.Api`).
- [ ] **Step 6: Commit**

```bash
git add src/api
git commit -m "fix(api): stop hardcoding TX on signup; allow null subscriber state"
```

---

### Task 4: `PUT /api/subscribers/{id}/state` and tightened `Register` validation

**Files:**
- Modify: `src/api/NomadRules.Api/Features/Subscribers/Models.cs` (add `SetStateRequest`)
- Modify: `src/api/NomadRules.Api/Features/Subscribers/SubscriberService.cs` (add `SetStateAsync`)
- Modify: `src/api/NomadRules.Api/Features/Subscribers/SubscriberEndpoints.cs` (route, `SetState`, `Register` check)
- Test: `src/api/NomadRules.Api.Tests/SubscriberServiceStateTests.cs`

**Interfaces:**
- Consumes: `SupportedStates.IsSupported`, `SupportedStates.Codes` (Task 2); nullable `Subscriber.State` (Task 3); `AuthService.ResolveOrProvisionAsync`.
- Produces:
  - `public record SetStateRequest(string State);`
  - `Task<Subscriber> SubscriberService.SetStateAsync(string id, string state)` — stores `state` as given (caller normalizes), bumps `updated_at`, returns the re-read row.
  - Route `PUT /api/subscribers/{id}/state`, policy `SubscriberTenant`.

- [ ] **Step 1: Write failing `SubscriberServiceStateTests`** (integration, cleanup in `Dispose`):
  - `SetState_OnProvisionedSubscriber_StoresSd` — provision via `AuthService` (null state), `SetStateAsync(id, "SD")` returns `State == "SD"`, and `GetByIdAsync(id)` also reads `"SD"`.
  - `SetState_CanOverwriteExistingState` — `CreateAsync(new RegisterRequest(email, "TX", null, null, null, null))`, then `SetStateAsync(id, "SD")` returns `"SD"`.
- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test src/api/NomadRules.Api.Tests --filter SubscriberServiceStateTests`
Expected: compile FAIL, `SetStateAsync` not defined.
- [ ] **Step 3: Implement `SetStateAsync`** in `SubscriberService`: `UPDATE subscribers SET state = @state, updated_at = now()::text WHERE id = @id`, then return `(await GetByIdAsync(id))!`.
- [ ] **Step 4: Add `SetStateRequest`** to `Models.cs`.
- [ ] **Step 5: Wire the endpoint** in `SubscriberEndpoints.Map`: `group.MapPut("/{id}/state", SetState).RequireAuthorization("SubscriberTenant");`. `SetState(string id, [FromBody] SetStateRequest req, ClaimsPrincipal user, SubscriberService svc)` flow, in order: `IsAuthorizedAsync` false → `Results.Forbid()`; `!SupportedStates.IsSupported(req.State)` → `Results.BadRequest(new { error = "invalid_state", message = $"State must be one of: {string.Join(", ", SupportedStates.Codes)}" })`; `GetByIdAsync(id)` null → `Results.NotFound()`; else `Results.Ok(await svc.SetStateAsync(id, req.State.ToUpperInvariant()))`.
- [ ] **Step 6: Tighten `Register`**: replace `string.IsNullOrWhiteSpace(req.State)` with `!SupportedStates.IsSupported(req.State)` and reuse the same `invalid_state` message.
- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test src/api/NomadRules.Api.Tests`
Expected: all PASS.
- [ ] **Step 8: Manual HTTP checks** (no HTTP test harness exists; covers Review Focus #3 and #4). Run the API locally per `docs/LOCAL_DEVELOPMENT.md`, then:
  - `POST /api/subscribers` with `"state":"CA"` → 400 `invalid_state`; with `"state":"sd"` → 201 and stored `SD`.
  - `PUT /api/subscribers/{id}/state` with a valid token for the owner and `{"state":"SD"}` → 200, `state: "SD"`; with `{"state":"CA"}` → 400; with a token for a different `oid` → 403; without a token → 401.
  - If obtaining Entra tokens locally is not possible, say so explicitly in the task report and rely on the service-level tests plus code review of `SetState`'s ordering (authorization first).
- [ ] **Step 9: Commit**

```bash
git add src/api
git commit -m "feat(api): add PUT /subscribers/{id}/state and enforce supported states on register"
```

---

### Task 5: Summarizer — state-aware prompt (`PromptBuilder`, `StateNames`)

**Files:**
- Create: `src/summarizer/NomadRules.Summarizer.Tests/` (via `dotnet new xunit`), `PromptBuilderTests.cs`
- Create: `src/summarizer/NomadRules.Summarizer/Summarization/PromptBuilder.cs`
- Modify: `src/summarizer/NomadRules.Summarizer/Summarization/Models.cs` (add `StateNames`)
- Modify: `src/summarizer/NomadRules.Summarizer/Prompts/SummarizeInsuranceChange.txt` (lines 1, 3, 7, 13: "Texas" → `{State}`)
- Delete: template `UnitTest1.cs`; Modify: `NomadRules.slnx`

**Interfaces:**
- Produces:
  - `public static class StateNames` in `NomadRules.Summarizer.Summarization` — `public static string FullName(string code)`: case-insensitive map `TX`→`Texas`, `SD`→`South Dakota`; unknown code returns the code unchanged.
  - `public static class PromptBuilder` — `public static string Build(string template, string stateCode)` replacing every `{State}` with `StateNames.FullName(stateCode)`.

- [ ] **Step 1: Scaffold**

```bash
cd src/summarizer
dotnet new xunit -n NomadRules.Summarizer.Tests
dotnet add NomadRules.Summarizer.Tests reference NomadRules.Summarizer/NomadRules.Summarizer.csproj
dotnet sln ../../NomadRules.slnx add NomadRules.Summarizer.Tests/NomadRules.Summarizer.Tests.csproj --solution-folder src/summarizer/
rm NomadRules.Summarizer.Tests/UnitTest1.cs
```
- [ ] **Step 2: Write failing `PromptBuilderTests`** (pure, no DB, no Anthropic):
  - `Build_Tx_SubstitutesTexas` — `Build("in {State}, for {State}", "TX")` == `"in Texas, for Texas"`.
  - `Build_Sd_SubstitutesSouthDakota` — `Build("{State}", "SD")` == `"South Dakota"`.
  - `Build_LowercaseCode_Works` — `"sd"` → `"South Dakota"`.
  - `Build_UnknownCode_FallsBackToCode` — `Build("{State}", "ZZ")` == `"ZZ"` (Review Focus #5).
  - `ShippedTemplate_HasPlaceholderAndNoHardcodedTexas` — reads `ClaudeSummarizer.PromptPath`; asserts it contains `{State}` and does not contain `Texas`. If the file is not copied to the test output, add a `<Content Include>`/link in the test csproj rather than weakening the test.
- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test src/summarizer/NomadRules.Summarizer.Tests`
Expected: compile FAIL, `PromptBuilder`/`StateNames` not defined.
- [ ] **Step 4: Implement** `StateNames` (in `Models.cs`, beside `LawChangeRow`) and `PromptBuilder.cs`, and edit the prompt template so all four "Texas" occurrences become `{State}`.
- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test src/summarizer/NomadRules.Summarizer.Tests`
Expected: all PASS.
- [ ] **Step 6: Commit**

```bash
git add NomadRules.slnx src/summarizer
git commit -m "feat(summarizer): template the prompt on state via PromptBuilder"
```

---

### Task 6: Summarizer — carry `State` from the claimed row into the prompt

**Files:**
- Modify: `src/summarizer/NomadRules.Summarizer/Summarization/Models.cs` (`LawChangeRow`)
- Modify: `src/summarizer/NomadRules.Summarizer/Summarization/LawChangeRepository.cs:12-24` (`ClaimNextAsync` SELECT)
- Modify: `src/summarizer/NomadRules.Summarizer/Summarization/ClaudeSummarizer.cs:12,29,38`
- Modify: `src/summarizer/NomadRules.Summarizer/Workers/SummarizerWorker.cs:58`

**Interfaces:**
- Consumes: `PromptBuilder.Build(string template, string stateCode)` (Task 5).
- Produces: `LawChangeRow(string Id, string RawContent, int RetryCount, string State)`; `ClaudeSummarizer.SummarizeAsync(string rawContent, string state, CancellationToken ct)`.

- [ ] **Step 1: Change `LawChangeRow`** to add `string State` as the fourth positional parameter (`law_changes.state` is `NOT NULL`, so `string` is exact).
- [ ] **Step 2: Add `state AS State`** to the `ClaimNextAsync` SELECT.
- [ ] **Step 3: Update `ClaudeSummarizer`**: rename the `_systemPrompt` Lazy to `_promptTemplate` (still `File.ReadAllText(PromptPath)`), add the `state` parameter to `SummarizeAsync`, and set `System = PromptBuilder.Build(_promptTemplate.Value, state)`.
- [ ] **Step 4: Update `SummarizerWorker.ProcessAsync`** call site to `summarizer.SummarizeAsync(row.RawContent, row.State, stoppingToken)`.
- [ ] **Step 5: Verify build and tests**

Run: `dotnet build NomadRules.slnx && dotnet test src/summarizer/NomadRules.Summarizer.Tests`
Expected: build succeeds with no remaining references to the old signature; tests PASS.
- [ ] **Step 6: Run the summarizer's self-check** if available: `dotnet run --project src/summarizer/NomadRules.Summarizer -- --selfcheck`. Expected: exits 0 (does not call Anthropic). Skip with a note if it requires an API key.
- [ ] **Step 7: Manual check** (spec Manual verification): insert a `law_changes` row with `state = 'SD'` and `raw_content` set, run the summarizer once with a real key, and confirm the stored summary says "South Dakota", not "Texas". If no API key is available, state that this was not verified.
- [ ] **Step 8: Commit**

```bash
git add src/summarizer
git commit -m "feat(summarizer): summarize each law change in its own state's terms"
```

---

### Task 7: Portal — `SelectState` onboarding screen

**Files:**
- Modify: `src/portal/src/lib/api.ts` (`Subscriber.state` type; `api.setState`)
- Create: `src/portal/src/pages/SelectState.tsx`
- Modify: `src/portal/src/App.tsx` (`loadMe` flow)

**Interfaces:**
- Consumes: `PUT /api/subscribers/{id}/state` body `{ state: string }` returning the updated `Subscriber` (Task 4).
- Produces: `api.setState(id: string, state: string): Promise<Subscriber>`; `SelectState` default-exported page taking props `{ subscriber: Subscriber; onSaved: (s: Subscriber) => void }` (match the existing pages' export/prop style in `Profile.tsx`).

- [ ] **Step 1: Update `api.ts`**: `Subscriber.state: string | null`; add `setState: (id, state) => request<Subscriber>(`/api/subscribers/${id}/state`, { method: 'PUT', body: JSON.stringify({ state }) })`, following the neighboring methods' style.
- [ ] **Step 2: Create `SelectState.tsx`**: heading "Where are you domiciled?", a select with options `TX` → "Texas" and `SD` → "South Dakota" (hand-written, matches the API allow-list), a Save button disabled until a value is chosen, calling `api.setState(subscriber.id, value)` then `onSaved(updated)`; show the API error message on failure. Reuse the existing shadcn/ui components used by `Profile.tsx`.
- [ ] **Step 3: Update `App.tsx`**: keep the `Subscriber` returned by `api.me()` in state; when `sub.state === null`, render `SelectState` before Profile/Feed; `onSaved` replaces the stored subscriber and proceeds to the normal view.
- [ ] **Step 4: Typecheck and lint**

Run: `cd src/portal && npx tsc --noEmit && npm run lint --if-present`
Expected: no errors (fix any other `subscriber.state` null-unsafe usages, e.g. `Profile.tsx`'s `Domicile: {subscriber.state}`).
- [ ] **Step 5: Manual browser verification** (start the API and `npm run dev` per `docs/LOCAL_DEVELOPMENT.md`; UI changes must be exercised, not just typechecked):
  - Fresh subscriber (null state) sees `SelectState`; choosing South Dakota and saving lands on Profile/Feed showing SD; reload does not show `SelectState` again.
  - Existing TX subscriber goes straight to Profile/Feed with no `SelectState` (Review Focus #1).
  - The SD subscriber's feed shows only SD items (spec Manual verification).
  - If Entra sign-in cannot be completed locally, state that explicitly instead of claiming success.
- [ ] **Step 6: Commit**

```bash
git add src/portal
git commit -m "feat(portal): one-time state picker for newly signed-up subscribers"
```
