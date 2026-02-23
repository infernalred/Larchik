# MOEX historical prices import (yearly SQL)

This folder contains scripts to build yearly `INSERT` SQL files for table `prices` starting from MOEX ISS history endpoints.

## Data sources (official MOEX ISS)

- ISS endpoint directory (contains history routes): https://iss.moex.com/iss/reference/
- History by market/board/security list: https://iss.moex.com/iss/reference/70
- History by market/board/single security: https://iss.moex.com/iss/reference/67
- Security directory (for symbol checks if needed): https://iss.moex.com/iss/reference/5

Main endpoint used by generator:

`/iss/history/engines/stock/markets/{shares|bonds}/boards/{BOARD}/securities/{TICKER}.json`

with params `from`, `till`, `start`, `iss.only=history`, `history.columns=...`.

## Scripts

- `generate_prices_sql.sh` - fetches MOEX history and creates `sql/prices_<year>.sql`
- `apply_prices_sql.sh` - applies generated yearly SQL files via `psql`

## Prerequisites

- `DATABASE_URL` (PostgreSQL connection string)
- binaries: `curl`, `jq`, `psql`

## Generate SQL (example: from 2018)

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/moex_history/generate_prices_sql.sh --from-year 2018 --to-year 2026
```

## Apply SQL to DB

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/moex_history/apply_prices_sql.sh --from-year 2018 --to-year 2026
```

## Notes

- Instruments are loaded from your `instruments` table (`type in (1,2,3)`), so script imports only symbols that exist in your DB.
- For each date script takes first non-empty price in priority order:
  `LEGALCLOSEPRICE -> MARKETPRICE2 -> CLOSE -> WAPRICE -> LCLOSEPRICE -> LAST`.
- SQL uses `INSERT ... ON CONFLICT (instrument_id, date, provider) DO UPDATE`.
- `id` is deterministic (UUID from `md5(instrument_id|date|provider)`), so reruns are stable.
