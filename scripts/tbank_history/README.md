# T-Bank historical prices import (yearly SQL)

This folder contains a script that generates yearly `INSERT` SQL files for table `prices` using T-Bank Invest API daily candles.

## Official sources

- T-Bank Invest API overview and auth: https://developer.tbank.ru/invest/
- MarketDataService `GetCandles` (REST): https://developer.tbank.ru/invest/api/market-data-service-get-candles
- Historical archives endpoint (`history-data`, yearly zip by instrument): https://developer.tbank.ru/invest/services/quotes

## What the script does

- Loads `FIGI` values from your `instruments` table (`type in (1,2,3)` and `figi is not null`).
- Requests daily candles per FIGI and per year via `GetCandles`.
- Builds `sql/prices_<year>.sql` with `INSERT ... ON CONFLICT (instrument_id, date, provider) DO UPDATE`.

## Prerequisites

- `DATABASE_URL` (PostgreSQL URI)
- `TINVEST_TOKEN` (Bearer token from T-Bank Invest API)
- binaries: `curl`, `jq`, `psql`

## Generate SQL (example from 2018)

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
TINVEST_TOKEN='your_token_here' \
./scripts/tbank_history/generate_prices_sql.sh --from-year 2018 --to-year 2026
```

## Apply generated SQL

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/tbank_history/apply_prices_sql.sh --from-year 2018 --to-year 2026 --sql-dir ./scripts/tbank_history/sql
```

## Notes

- If `figi` is missing for an instrument, it is skipped.
- Daily price is taken from candle `close`.
- `id` is deterministic (UUID from `md5(instrument_id|date|provider)`), reruns are stable.
