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

"${SCRIPT_DIR}/moex_history/apply_prices_sql.sh" \
  --from-year "$FROM_YEAR" \
  --to-year "$TO_YEAR" \
  --sql-dir "${SCRIPT_DIR}/moex_history/sql"

"${SCRIPT_DIR}/tbank_history/apply_prices_sql.sh" \
  --from-year "$FROM_YEAR" \
  --to-year "$TO_YEAR" \
  --sql-dir "${SCRIPT_DIR}/tbank_history/sql"
