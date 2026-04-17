#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Apply targeted MOEX price backfill for Полипласт АО П02-БО-14
  - Полипласт АО П02-БО-14 (RU000A10ECX8)

Usage:
  DATABASE_URL=... ./scripts/apply_missing_poliplast_prices.sh
USAGE
}

if [[ "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "DATABASE_URL is required" >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "Missing required binary: psql" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_FILE="${SCRIPT_DIR}/moex_history/sql/prices_2026_ru000a10ecx8_missing.sql"

echo "Applying ${SQL_FILE}" >&2
psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -f "${SQL_FILE}"

echo "Done." >&2
