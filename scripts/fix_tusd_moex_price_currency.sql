-- Fix historical MOEX prices for TUSD / RU000A1011S9.
-- MOEX reports this ETF in SUR/RUB, while the instrument currency is USD.
-- Older historical imports stored those RUB prices as USD and inflated NAV
-- by an extra USD/RUB conversion during portfolio valuation.

BEGIN;

WITH fixed AS (
    UPDATE prices p
    SET
        currency_id = 'RUB',
        source_currency_id = 'RUB',
        updated_at = now()
    FROM instruments i
    WHERE i.id = p.instrument_id
      AND upper(coalesce(i.ticker, '')) = 'TUSD'
      AND upper(coalesce(i.isin, '')) = 'RU000A1011S9'
      AND upper(coalesce(p.provider, '')) = 'MOEX'
      AND p.currency_id = 'USD'
      AND p.value > 1
    RETURNING p.id
)
SELECT count(*) AS fixed_tusd_moex_price_rows
FROM fixed;

COMMIT;

