\set ON_ERROR_STOP on

-- Idempotent one-command import for the eight MOEX bonds missing from broker reports.
-- Run with psql so the relative \ir includes are resolved from this script's directory:
--   psql "$DATABASE_URL" -X -v ON_ERROR_STOP=1 -f scripts/add_missing_moex_bonds_20260808.sql

\ir add_ru_missing_20260808.sql
\ir cbr_fx_missing_20260808.sql
\ir moex_history/sql/prices_2025_missing_20260808.sql
\ir moex_history/sql/prices_2026_missing_20260808.sql

DO $$
DECLARE
    invalid_targets text;
BEGIN
    WITH expected (isin, expected_prices, first_price_date, last_price_date) AS (
        VALUES
            ('RU000A10BV55', 287, DATE '2025-06-24', DATE '2026-08-07'),
            ('RU000A10FMQ8', 21, DATE '2026-07-10', DATE '2026-08-07'),
            ('RU000A10FJS0', 26, DATE '2026-07-03', DATE '2026-08-07'),
            ('RU000A10FNA0', 19, DATE '2026-07-14', DATE '2026-08-07'),
            ('RU000A10FNB8', 19, DATE '2026-07-14', DATE '2026-08-07'),
            ('RU000A10FMY2', 20, DATE '2026-07-13', DATE '2026-08-07'),
            ('RU000A10FGH9', 14, DATE '2026-07-21', DATE '2026-08-07'),
            ('RU000A10CS75', 224, DATE '2025-09-19', DATE '2026-08-07')
    ),
    actual AS (
        SELECT
            expected.*,
            i.currency_id AS instrument_currency_id,
            count(p.id)::integer AS actual_prices,
            min(p.date)::date AS actual_first_price_date,
            max(p.date)::date AS actual_last_price_date,
            bool_and(p.currency_id = i.currency_id) AS prices_use_instrument_currency
        FROM expected
        LEFT JOIN instruments i
          ON upper(coalesce(i.isin, '')) = expected.isin
        LEFT JOIN prices p
          ON p.instrument_id = i.id
         AND upper(p.provider) = 'MOEX'
         AND p.date::date BETWEEN expected.first_price_date AND expected.last_price_date
        GROUP BY
            expected.isin,
            expected.expected_prices,
            expected.first_price_date,
            expected.last_price_date,
            i.currency_id
    )
    SELECT string_agg(
        format(
            '%s: prices=%s/%s, range=%s..%s/%s..%s, currency_ok=%s',
            isin,
            actual_prices,
            expected_prices,
            actual_first_price_date,
            actual_last_price_date,
            first_price_date,
            last_price_date,
            prices_use_instrument_currency
        ),
        '; ' ORDER BY isin
    )
    INTO invalid_targets
    FROM actual
    WHERE actual_prices <> expected_prices
       OR actual_first_price_date <> first_price_date
       OR actual_last_price_date <> last_price_date
       OR prices_use_instrument_currency IS NOT TRUE;

    IF invalid_targets IS NOT NULL THEN
        RAISE EXCEPTION 'MOEX missing-bond import validation failed: %', invalid_targets;
    END IF;

    RAISE NOTICE 'MOEX missing-bond import passed: 8 instruments and 630 prices through 2026-08-07.';
END $$;
