\set ON_ERROR_STOP on

-- Idempotent one-command import for RU000A10FTR1 and its full MOEX history.
-- Run with psql so the relative \ir includes are resolved from this script's directory:
--   psql "$DATABASE_URL" -X -v ON_ERROR_STOP=1 -f scripts/add_ru000a10ftr1_with_prices_20260816.sql

\ir add_ru000a10ftr1_20260816.sql
\ir moex_history/sql/prices_2026_missing_20260816.sql

DO $$
DECLARE
    actual_prices integer;
    first_price_date date;
    last_price_date date;
    prices_use_instrument_currency boolean;
BEGIN
    SELECT
        count(p.id)::integer,
        min(p.date)::date,
        max(p.date)::date,
        bool_and(p.currency_id = i.currency_id)
    INTO
        actual_prices,
        first_price_date,
        last_price_date,
        prices_use_instrument_currency
    FROM instruments i
    LEFT JOIN prices p
      ON p.instrument_id = i.id
     AND upper(p.provider) = 'MOEX'
     AND p.date::date BETWEEN DATE '2026-08-06' AND DATE '2026-08-14'
    WHERE upper(coalesce(i.isin, '')) = 'RU000A10FTR1'
    GROUP BY i.id;

    IF actual_prices <> 7
       OR first_price_date <> DATE '2026-08-06'
       OR last_price_date <> DATE '2026-08-14'
       OR prices_use_instrument_currency IS NOT TRUE THEN
        RAISE EXCEPTION
            'RU000A10FTR1 validation failed: prices=%/7, range=%..%/2026-08-06..2026-08-14, currency_ok=%',
            actual_prices,
            first_price_date,
            last_price_date,
            prices_use_instrument_currency;
    END IF;

    RAISE NOTICE 'RU000A10FTR1 import passed: instrument and 7 MOEX prices through 2026-08-14.';
END $$;
