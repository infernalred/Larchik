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
- You can override DB discovery with `--targets-file path/to/file.tsv`, where each line is:
  `TICKER<TAB>shares|bonds`
- For each date script takes first non-empty price in priority order:
  `LEGALCLOSEPRICE -> MARKETPRICE2 -> CLOSE -> WAPRICE -> LCLOSEPRICE -> LAST`.
- SQL uses `INSERT ... ON CONFLICT (instrument_id, date, provider) DO UPDATE`.
- `id` is deterministic (UUID from `md5(instrument_id|date|provider)`), so reruns are stable.

## 2024 T-Bank report helper files

- `sql/report_2024_missing_instruments.sql` fixes `MTSS` ISIN and inserts the
  missing instruments from `/Users/alex/Downloads/broker-report-2024-01-01-2024-12-31.xlsx`.
- `targets/report_2024_missing_prices.tsv` contains MOEX symbols that still need
  historical prices after the reference script is applied.
- `sql/report_2025_missing_instruments.sql` fixes `TRNFP` ISIN and inserts the
  missing bond instruments from `/Users/alex/Downloads/broker-report-2025-01-01-2025-12-31.xlsx`.
- `targets/report_2025_missing_prices.tsv` contains the 2025-report bond symbols
  that need MOEX history generation.
- `sql/report_2026_missing_instruments.sql` fixes `TATNP`, `LKOH`, and `BSPB`
  ISIN values for `/Users/alex/Downloads/broker-report-2026-01-01-2026-03-05.xlsx`.

Example:

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/moex_history/sql/report_2024_missing_instruments.sql

./scripts/moex_history/generate_prices_sql.sh \
  --from-year 2020 \
  --to-year 2026 \
  --targets-file scripts/moex_history/targets/report_2024_missing_prices.tsv \
  --out-dir scripts/moex_history/sql/report_2024_missing

DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/moex_history/apply_prices_sql.sh \
  --from-year 2020 \
  --to-year 2026 \
  --sql-dir scripts/moex_history/sql/report_2024_missing
```
