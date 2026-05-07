-- Fix VEON / US91822M5022 historical T-Bank prices before the ADR ratio change.
-- T-Bank history contains post-ratio prices starting 2023-01-03, while portfolio
-- holdings are still pre-ratio until the synthetic 1:25 reverse split operation.
-- Divide those pre-split prices by 25 so valuation stays on the same share scale.

BEGIN;

WITH fixed AS (
    UPDATE prices p
    SET
        value = (p.value / 25.0)::numeric(18,4),
        source_currency_id = 'USD',
        updated_at = now()
    FROM instruments i
    WHERE i.id = p.instrument_id
      AND upper(coalesce(i.ticker, '')) = 'VEON'
      AND upper(coalesce(i.isin, '')) = 'US91822M5022'
      AND upper(coalesce(p.provider, '')) = 'TBANK'
      AND p.date::date >= DATE '2023-01-03'
      AND p.date::date <= DATE '2023-03-08'
      AND p.currency_id = 'USD'
      AND p.value > 2
    RETURNING p.id
)
SELECT count(*) AS fixed_veon_tbank_presplit_price_rows
FROM fixed;

COMMIT;

