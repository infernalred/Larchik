# MOEX historical prices

`sql/prices_<year>.sql` in this folder are the final consolidated MOEX imports used for rollout.

## Apply

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/moex_history/apply_prices_sql.sh --from-year 2018 --to-year 2026
```

For the full portfolio price rollout across providers use the top-level entrypoint:

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/import_prices_sql.sh --from-year 2018 --to-year 2026
```

## Targeted OFZ backfill

To backfill only the missing MOEX history for `ОФЗ 26246/26250/26253/26254` without rerunning the full price import:

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/apply_missing_ofz_prices.sh
```

Or run the combined SQL directly:

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f ./scripts/fix_missing_ofz_prices.sql
```

## Regeneration

`generate_prices_sql.sh` is still available for ad hoc MOEX history rebuilds, but the committed `sql/prices_<year>.sql` files are already the production-ready merged result.

If you regenerate exploratory data, write it to a separate output directory instead of overwriting the committed consolidated files.
