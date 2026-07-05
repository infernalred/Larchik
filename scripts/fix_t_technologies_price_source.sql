-- Keep Russian T-Technologies priced from MOEX while preserving AT&T on T-Bank.
-- Both instruments may have ticker "T", so match by ISIN/FIGI instead of ticker.

BEGIN;

UPDATE instruments
SET
    price_source = 'MOEX',
    exchange = 'MOEX',
    country = 'RU',
    updated_at = now()
WHERE upper(coalesce(isin, '')) = 'RU000A107UL4';

UPDATE instruments
SET
    ticker = 'T-US',
    price_source = 'TBANK',
    updated_at = now()
WHERE upper(coalesce(figi, '')) = 'BBG000BSJK37'
  AND upper(coalesce(isin, '')) <> 'RU000A107UL4';

INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    '7eb97178-d209-42e6-942e-0c2fd7dbfc28'::uuid,
    i.id,
    'T@US',
    'T@US'
FROM instruments i
WHERE upper(coalesce(i.figi, '')) = 'BBG000BSJK37'
  AND upper(coalesce(i.isin, '')) <> 'RU000A107UL4'
  AND NOT EXISTS (
      SELECT 1
      FROM instrument_aliases ia
      WHERE ia.normalized_alias_code = 'T@US'
  );

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM instruments
        WHERE upper(coalesce(isin, '')) = 'RU000A107UL4'
          AND price_source = 'MOEX'
          AND upper(coalesce(exchange, '')) = 'MOEX'
    ) THEN
        RAISE EXCEPTION 'T-Technologies price source validation failed: RU000A107UL4 is not configured for MOEX.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM instruments
        WHERE upper(coalesce(figi, '')) = 'BBG000BSJK37'
          AND upper(coalesce(isin, '')) <> 'RU000A107UL4'
          AND (
              price_source IS DISTINCT FROM 'TBANK'
              OR upper(ticker) <> 'T-US'
          )
    ) THEN
        RAISE EXCEPTION 'AT&T validation failed: BBG000BSJK37 is not configured as T-US/TBANK.';
    END IF;
END $$;

COMMIT;
