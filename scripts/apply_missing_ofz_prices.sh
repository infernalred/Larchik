#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Apply targeted MOEX price backfill for missing OFZ instruments:
  - ОФЗ 26246 (RU000A108EE1 / SU26246RMFS7)
  - ОФЗ 26250 (RU000A10BVH7 / SU26250RMFS9)
  - ОФЗ 26253 (RU000A10D517 / SU26253RMFS3)
  - ОФЗ 26254 (RU000A10D533 / SU26254RMFS1)

Usage:
  DATABASE_URL=... ./scripts/apply_missing_ofz_prices.sh
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

for sql_file in \
  "${SCRIPT_DIR}/moex_history/sql/prices_2024_ofz_missing.sql" \
  "${SCRIPT_DIR}/moex_history/sql/prices_2025_ofz_missing.sql" \
  "${SCRIPT_DIR}/moex_history/sql/prices_2026_ofz_missing.sql"; do
  echo "Applying ${sql_file}" >&2
  psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -f "${sql_file}"
done

echo "Done." >&2
