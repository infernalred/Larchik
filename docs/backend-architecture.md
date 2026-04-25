# Backend Architecture

## Runtime Shape
- `src/Larchik.API` is the synchronous request host: controllers stay thin, delegate to MediatR, and rely on middleware for exception handling and auth plumbing.
- `src/Larchik.Application` owns business use-cases, validation, import parsing, and valuation rules. Handlers are expected to stay orchestration-focused; shared logic lives in helpers/services inside the feature area instead of being copy-pasted across handlers.
- `src/Larchik.Persistence` owns EF Core mapping and migrations. The `LarchikContext` defaults to `QueryTrackingBehavior.NoTracking`; write paths must opt into `AsTracking()` explicitly when they intend to modify loaded entities.
- `src/Larchik.Infrastructure` contains cross-cutting runtime services such as user access, job planning/execution, clocks, recalculation orchestration, and external HTTP integrations.
- `src/Larchik.Jobs` is a separate background host for recurring FX/price syncs and job scheduler/executor processes.

## Domain Conventions
- `Operation` remains a single journal table. Type-specific correctness is enforced in application validation and write helpers instead of splitting the model into many persistence tables.
- `Instrument.CurrencyId` is the default quote/nominal currency for price history and valuation. `Operation.CurrencyId` is the settlement currency of a specific trade or cash movement and may differ.
- `Instrument.Isin` is nullable in persistence. Application validation requires it for `Equity`, `Bond`, and `Etf`; other instrument types may legitimately have no ISIN.
- Historical prices are canonical in `prices`. The old `instruments.price` column is removed from the active model and must not be referenced by scripts or application code.
- Legacy persisted `lots` and `cash_balances` are no longer part of the active EF model. Cost basis and cash are derived from operations, valuation strategies, and snapshots.

## Sync And Import Rules
- `price_source` is nullable and only meaningful for tradable instruments. `MOEX` sync must process only instruments assigned to `MOEX`; `TBANK` sync must process only instruments assigned to `TBANK`.
- Russian instruments may use either `MOEX` or `TBANK` when explicitly assigned; sync eligibility is driven by `price_source`, not by country.
- Price and FX sync code paths must persist UTC `DateTime` values only.
- `scripts/import_reference_data.sql` and reset scripts are expected to stay aligned with the current persistence model, including the removal of `instruments.price`, `lots`, and `cash_balances`.

## Refactoring Rules
- Work test-first: add or update coverage, refactor production code, then rerun the relevant tests and the full solution tests when the change is broad.
- Remove unused `using` directives while editing.
- Prefer consolidating repeated logic into shared helpers/extensions when the behavior is truly common and already covered by tests.
