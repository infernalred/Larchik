# Financial and Broker Accounting Checklist

## Goal
Strengthen portfolio accounting correctness for valuation, cash ledger, FX conversion, and broker statement reconciliation.

## Verification Checklist

- [ ] Validate valuation parity across `adjustingAvg`, `staticAvg`, `fifo`, `lifo` on shared scenario fixtures.
- [ ] Cover partial sell flows with explicit fees in both realized P&L and cash balances.
- [ ] Cover mixed-currency operations where `operation.currency_id` differs from `instrument.currency_id`.
- [ ] Verify missing market prices do not silently break NAV math and are visible as zero market value with retained cost basis.
- [ ] Verify all FX paths use date-correct rates and UTC timestamps.
- [ ] Ensure `price_source` filtering is enforced by each sync handler (`MOEX` vs `TBANK`) and ignores `is_trading = false`.
- [ ] Keep accounting invariants guarded by tests: no negative position quantity after valid flows, deterministic cost-basis behavior, stable realized/unrealized breakdown.
- [ ] Maintain broker import reconciliation behavior for manual-to-imported operation matching.

## Task Backlog (Implementation-Ready)

## 1) Core valuation and cash scenarios

- [x] Add regression test: cross-currency buy with historical FX conversion in summary output.
- [x] Add regression test: partial sell with fee affects realized P&L and cash.
- [x] Add regression test: missing as-of price keeps position with zero market value and negative unrealized.

## 2) Time and FX data quality

- [x] Add validator test: reject non-UTC trade date.
- [x] Add validator test: reject settlement date earlier than trade date.
- [x] Add test harness helper for explicit FX rates to keep tests deterministic.

## 3) Price sync source safety

- [x] Add explicit tests that each sync handler skips instruments with mismatched `price_source`.
- [x] Add explicit tests that each sync handler skips instruments where `is_trading = false`.

## 4) Reconciliation and operations control

- [x] Add daily reconciliation report DTO/helper (statement vs internal summary deltas with tolerances).
- [x] Wire reconciliation report generation into jobs pipeline with warning-level logs on delta breach.
- [x] Add regression tests for reconciliation tolerances and mismatch classification.

## 5) Definition of done for accounting changes

- [ ] `dotnet build Larchik.sln` is green.
- [ ] Targeted tests are green:
  - `tests/Larchik.Application.Tests/Portfolios/GetPortfoliosSummaryQueryHandlerTests.cs`
  - `tests/Larchik.Application.Tests/Validation/OperationValidatorTests.cs`
  - `tests/Larchik.Application.Tests/Prices/SyncMoexPricesCommandHandlerTests.cs`
  - `tests/Larchik.Application.Tests/Prices/SyncTbankPricesCommandHandlerTests.cs`
- [ ] New scenarios are documented in this checklist and kept in sync with tests.
