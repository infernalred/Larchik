BEGIN;

UPDATE instruments
SET price_source = CASE
    WHEN NOT is_trading THEN NULL
    WHEN type IN (1, 2, 3, 4)
         AND (
             upper(coalesce(country, '')) = 'RU'
             OR upper(coalesce(isin, '')) LIKE 'RU%'
             OR upper(coalesce(exchange, '')) IN ('TQBR', 'TQTF', 'TQIF', 'TQCB', 'TQOB', 'CETS', 'MTQR', 'CNGD')
         ) THEN 'MOEX'
    WHEN type IN (1, 2, 3, 4)
         AND coalesce(figi, '') <> '' THEN 'TBANK'
    ELSE NULL
END,
updated_at = now();

COMMIT;
