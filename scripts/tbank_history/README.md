# T-Bank historical prices

`sql/prices_<year>.sql` in this folder are the final consolidated T-Bank imports used for rollout.

## Apply

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/tbank_history/apply_prices_sql.sh --from-year 2018 --to-year 2026
```

For the full portfolio price rollout across providers use the top-level entrypoint:

```bash
DATABASE_URL='postgresql://postgres:postgres@localhost:5432/larchik' \
./scripts/import_prices_sql.sh --from-year 2018 --to-year 2026
```

## Regeneration

`generate_prices_sql.sh` is still available for ad hoc T-Bank history rebuilds, but the committed `sql/prices_<year>.sql` files are already the production-ready merged result.

If you regenerate exploratory data, write it to a separate output directory instead of overwriting the committed consolidated files.
