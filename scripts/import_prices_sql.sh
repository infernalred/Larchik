#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Apply consolidated historical price SQL for all providers.

Usage:
  DATABASE_URL=... ./scripts/import_prices_sql.sh [options]

Options:
  --from-year <YYYY>   First year to apply (default: 2018)
  --to-year <YYYY>     Last year to apply (default: current year)
  --allow-non-empty    Skip the preflight check that prices table is empty
  --help               Show this message
USAGE
}

normalize_year() {
  local value="$1"
  if [[ ! "$value" =~ ^[0-9]{4}$ ]]; then
    echo "Invalid year: $value" >&2
    exit 1
  fi
  echo "$value"
}

FROM_YEAR=2018
TO_YEAR="$(date +%Y)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ALLOW_NON_EMPTY=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from-year)
      FROM_YEAR="$(normalize_year "${2:-}")"
      shift 2
      ;;
    --to-year)
      TO_YEAR="$(normalize_year "${2:-}")"
      shift 2
      ;;
    --allow-non-empty)
      ALLOW_NON_EMPTY=1
      shift
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "DATABASE_URL is required" >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "Missing required binary: psql" >&2
  exit 1
fi

if (( FROM_YEAR > TO_YEAR )); then
  echo "from-year must be <= to-year" >&2
  exit 1
fi

sum_expected_rows() {
  local sql_dir="$1"
  local total=0
  local year
  for ((year = FROM_YEAR; year <= TO_YEAR; year++)); do
    shopt -s nullglob
    local sql_files=("${sql_dir}/prices_${year}.sql" "${sql_dir}/prices_${year}_"*.sql)
    shopt -u nullglob
    local sql_file
    for sql_file in "${sql_files[@]}"; do
      [[ -f "$sql_file" ]] || continue
      local rows
      rows="$(awk -F ': ' '/^-- Rows: / { sum += $2 } END { print sum + 0 }' "$sql_file")"
      rows="${rows:-0}"
      total=$((total + rows))
    done
  done
  echo "$total"
}

EXPECTED_MOEX="$(sum_expected_rows "${SCRIPT_DIR}/moex_history/sql")"
EXPECTED_TBANK="$(sum_expected_rows "${SCRIPT_DIR}/tbank_history/sql")"
EXPECTED_TOTAL=$((EXPECTED_MOEX + EXPECTED_TBANK))

if [[ "$ALLOW_NON_EMPTY" != "1" ]]; then
  existing_prices="$(psql "$DATABASE_URL" -X -A -t -v ON_ERROR_STOP=1 -c "select count(*) from prices;")"
  existing_prices="${existing_prices//[[:space:]]/}"
  if [[ "${existing_prices:-0}" != "0" ]]; then
    echo "Preflight failed: prices table is not empty (${existing_prices} rows). Use --allow-non-empty only if you intentionally want to validate against existing data." >&2
    exit 1
  fi
fi

"${SCRIPT_DIR}/moex_history/apply_prices_sql.sh" \
  --from-year "$FROM_YEAR" \
  --to-year "$TO_YEAR" \
  --sql-dir "${SCRIPT_DIR}/moex_history/sql"

"${SCRIPT_DIR}/tbank_history/apply_prices_sql.sh" \
  --from-year "$FROM_YEAR" \
  --to-year "$TO_YEAR" \
  --sql-dir "${SCRIPT_DIR}/tbank_history/sql"

read -r ACTUAL_MOEX ACTUAL_TBANK ACTUAL_TOTAL <<<"$(psql "$DATABASE_URL" -X -A -t -F ' ' -v ON_ERROR_STOP=1 -c "
with scoped as (
    select provider, count(*) as rows_count
    from prices
    where date >= make_date(${FROM_YEAR}, 1, 1)::timestamp at time zone 'UTC'
      and date < make_date($((TO_YEAR + 1)), 1, 1)::timestamp at time zone 'UTC'
      and upper(provider) in ('MOEX', 'TBANK')
    group by provider
)
select
    coalesce(max(case when upper(provider) = 'MOEX' then rows_count end), 0),
    coalesce(max(case when upper(provider) = 'TBANK' then rows_count end), 0),
    coalesce(sum(rows_count), 0)
from scoped;")"

if [[ "${ACTUAL_MOEX:-0}" != "$EXPECTED_MOEX" ]]; then
  echo "MOEX validation failed: expected ${EXPECTED_MOEX} rows, found ${ACTUAL_MOEX:-0}." >&2
  exit 1
fi

if [[ "${ACTUAL_TBANK:-0}" != "$EXPECTED_TBANK" ]]; then
  echo "TBANK validation failed: expected ${EXPECTED_TBANK} rows, found ${ACTUAL_TBANK:-0}." >&2
  exit 1
fi

if [[ "${ACTUAL_TOTAL:-0}" != "$EXPECTED_TOTAL" ]]; then
  echo "Price import validation failed: expected ${EXPECTED_TOTAL} rows, found ${ACTUAL_TOTAL:-0}." >&2
  exit 1
fi

echo "Price import validation passed. MOEX=${ACTUAL_MOEX}, TBANK=${ACTUAL_TBANK}, total=${ACTUAL_TOTAL}." >&2
