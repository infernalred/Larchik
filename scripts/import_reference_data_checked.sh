#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Apply reference data import SQL with preflight and post-run summary.

Usage:
  DATABASE_URL=... ./scripts/import_reference_data_checked.sh [options]

Options:
  --allow-non-empty    Skip the preflight check that target tables are empty
  --help               Show this message
USAGE
}

ALLOW_NON_EMPTY=0
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

while [[ $# -gt 0 ]]; do
  case "$1" in
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

if [[ "$ALLOW_NON_EMPTY" != "1" ]]; then
  read -r instruments_count aliases_count corporate_actions_count fx_count <<<"$(psql "$DATABASE_URL" -X -A -t -F ' ' -v ON_ERROR_STOP=1 -c "
select
    (select count(*) from instruments),
    (select count(*) from instrument_aliases),
    (select count(*) from instrument_corporate_actions),
    (select count(*) from fx_rates);")"

  if [[ "${instruments_count:-0}" != "0" || "${aliases_count:-0}" != "0" || "${corporate_actions_count:-0}" != "0" || "${fx_count:-0}" != "0" ]]; then
    echo "Preflight failed: expected empty instruments/instrument_aliases/instrument_corporate_actions/fx_rates tables, got instruments=${instruments_count:-0}, aliases=${aliases_count:-0}, corporate_actions=${corporate_actions_count:-0}, fx_rates=${fx_count:-0}." >&2
    exit 1
  fi
fi

psql "$DATABASE_URL" -X -v ON_ERROR_STOP=1 -f "${SCRIPT_DIR}/import_reference_data.sql"

read -r final_instruments final_aliases final_corporate_actions final_fx <<<"$(psql "$DATABASE_URL" -X -A -t -F ' ' -v ON_ERROR_STOP=1 -c "
select
    (select count(*) from instruments),
    (select count(*) from instrument_aliases),
    (select count(*) from instrument_corporate_actions),
    (select count(*) from fx_rates);")"

echo "Reference import finished. instruments=${final_instruments:-0}, aliases=${final_aliases:-0}, corporate_actions=${final_corporate_actions:-0}, fx_rates=${final_fx:-0}." >&2
