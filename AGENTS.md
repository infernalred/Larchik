# Repository Guidelines

## Project Structure & Module Organization
- `src/Larchik.API`: ASP.NET Core 10 host with NSwag docs at `/swagger`, JWT auth, CORS, exception middleware, HTTP clients for imports. Controllers inject application `*Handler` types directly (no MediatR).
- `src/Larchik.Application`: CQRS-style feature folders (portfolios, operations, prices, fx, valuations) with commands/queries, handlers, DTOs, validators, parser/import logic, and shared calculation helpers. Handlers are registered in DI via `AddApplicationHandlers()` (`DependencyInjection/ApplicationHandlersServiceCollectionExtensions.cs`), which scans for concrete types whose names end with `Handler`. Command success without a payload uses `Larchik.Application.Helpers.Unit` (not MediatR’s `Unit`).
- `src/Larchik.Persistence`: EF Core/PostgreSQL context; entities for portfolios, instruments, operations, prices, FX rates, snapshots, and job scheduling. Legacy `Lot` / `CashBalance` entity files remain for migration history only and are not mapped by the current context. Configurations and migrations live under `Migrations/`; snake_case is applied via `EFCore.NamingConventions`.
- `src/Larchik.Infrastructure`: Cross-cutting services for user access, jobs, recalculation orchestration, schedulers, clocks, and external HTTP integrations.
- `src/Larchik.Jobs`: separate background host for recurring FX/price syncs and scheduler/executor processing.
- `docs/`: repo documentation for current backend architecture and domain conventions. `src/larchik_client/`: React + TS client. `Tests/`: active automated test suite for handlers, valuation, parsers, infrastructure, jobs, repository consistency, and regression scenarios.

## Build, Run, and Data
- Restore/build: `dotnet restore && dotnet build Larchik.sln` (project targets `.NET 10` / `net10.0` and uses C# 14).
- Database/migrations: `dotnet ef database update --project src/Larchik.Persistence --startup-project src/Larchik.API` (PostgreSQL). Provide `ConnectionStrings__DefaultConnection`.
- Run API: `dotnet run --project src/Larchik.API`. Secrets: `TokenKey`, optional `DaysTokenLife`; CORS origins via `Cors:Origins`.
- Price import: `POST /api/prices/sync` with array of instrument/date/provider prices. FX import (CBR): `POST /api/fxrates/sync/cbr?date=YYYY-MM-DD` (admin).
- Portfolio summary: `GET /api/portfolios/{id}/summary?method=adjustingAvg|staticAvg|fifo|lifo` returns NAV, cash, avg cost, realized/unrealized P&L.

## Operational Notes
- Trading instruments now have a nullable `price_source` enum (`MOEX`, `TBANK`); non-trading/manual instruments may keep it `NULL`.
- Price sync jobs must only process `is_trading = true` instruments and only those whose `price_source` matches the job: `MOEX` for MOEX sync, `TBANK` for T-Bank sync.
- Russian instruments may use either `MOEX` or `TBANK` when explicitly assigned; price sync jobs must respect `price_source` instead of deriving the source from country.
- `instrument.currency_id` means the instrument quote/nominal currency used by price history and valuation by default. `operation.currency_id` is the settlement currency of a concrete operation and may differ.
- `instrument.isin` is nullable at the database level. It is required by application validation for `Equity`, `Bond`, and `Etf`, but may be absent for `Currency`, `Commodity`, `Crypto`, and manual/non-standard instruments.
- Admin instrument create/edit UI must expose `price_source` as a fixed list of supported sync providers, not a free-form text field.
- Backfill for `price_source` lives both in EF migration `20260410081231_AddInstrumentPriceSource` and in `scripts/set_instrument_price_sources.sql`; `scripts/import_reference_data.sql` should also leave a clean database with the right source assignments.
- Repository reset/import scripts must not reference removed persisted `lots` / `cash_balances` tables or the deleted `instruments.price` column.
- Background jobs run in the separate `src/Larchik.Jobs` host. Production diagnostics should make it obvious when the jobs host, scheduler, and executor have started.
- Grafana logs should be split into separate dashboards for API and jobs; do not merge them into one dashboard with multiple panels.
- For PostgreSQL `timestamp with time zone`, application code must write UTC `DateTime` values only; avoid `DateTimeKind.Unspecified` in price and FX sync code paths.

## Coding Style & Naming Conventions
- The project uses `.NET 10`, C# 14, and nullable reference types.
- Use the newest available C# 14 syntax and modern language constructs where they improve clarity and reduce boilerplate.
- Prefer expression-bodied members and immutability where practical.
- Remove unused `using` directives while editing code; do not leave stale namespace imports behind after refactoring.
- CQRS naming: `...Command`/`...Query` + `...Handler` per feature with `Handle(...)`; controllers stay thin and call the injected handler only (no mediator pipeline).
- Entities/configs map to snake_case columns; migrations live in `src/Larchik.Persistence/Migrations/`.
- DTOs favor `record`/init-only props; validation through FluentValidation.

## Testing Guidelines
- The repository already has an active `Tests/Larchik.Application.Tests` project. Extend the existing suite before adding a new test project unless there is a strong boundary reason to split.
- For valuation/FX/price logic, use fixture data with known FX and closing prices; prefer disposable Postgres schemas or SQLite in-memory for isolation.
- For any refactoring, work in test-first order: first add or update tests that cover the relevant scenarios, then refactor production code, then verify the tests still pass. Do not start refactoring application logic before the target behavior is covered by tests.
- For repository-level refactors in `scripts/`, `docs/`, or project wiring, add consistency tests when the change can silently drift again.

## Commit & Pull Request Guidelines
- Commit messages are short and imperative (e.g., `add fifo valuation`, `update fx sync`).
- PRs: describe scope, link issues, highlight schema/migration impact, and note commands run (build, migrations, key endpoints). Include screenshots/gifs for client changes.
