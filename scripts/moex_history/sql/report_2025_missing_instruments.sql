BEGIN;

-- Existing TRNFP row has FIGI stored in both isin/figi. Fix the ISIN in-place
-- so report imports resolve to the existing instrument and reuse its prices.
UPDATE instruments
SET isin = 'RU0009091573',
    figi = COALESCE(figi, 'BBG00475KHX6'),
    updated_at = now(),
    updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
WHERE upper(ticker) = 'TRNFP'
  AND upper(isin) = 'BBG00475KHX6';

WITH src (name, ticker, isin, figi, type, currency_id, category_id, exchange, country, price) AS (
    VALUES
        ('МОНОПОЛИЯ оббП03', 'RU000A10ARS4', 'RU000A10ARS4', NULL::text, 2, 'RUB', 14, NULL::text, 'RU', NULL::numeric(18,4)),
        ('ТГК-14 оббП05', 'RU000A10AS02', 'RU000A10AS02', NULL::text, 2, 'RUB', 14, NULL::text, 'RU', NULL::numeric(18,4)),
        ('ВИС ФИНАНС оббП07', 'RU000A10AV15', 'RU000A10AV15', NULL::text, 2, 'RUB', 14, NULL::text, 'RU', NULL::numeric(18,4))
),
actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
)
INSERT INTO instruments (
    id,
    name,
    ticker,
    isin,
    figi,
    type,
    currency_id,
    category_id,
    exchange,
    country,
    price,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    (
        substring(md5('report-2025:' || src.ticker) from 1 for 8) || '-' ||
        substring(md5('report-2025:' || src.ticker) from 9 for 4) || '-' ||
        substring(md5('report-2025:' || src.ticker) from 13 for 4) || '-' ||
        substring(md5('report-2025:' || src.ticker) from 17 for 4) || '-' ||
        substring(md5('report-2025:' || src.ticker) from 21 for 12)
    )::uuid,
    src.name,
    src.ticker,
    src.isin,
    src.figi,
    src.type,
    src.currency_id,
    src.category_id,
    src.exchange,
    src.country,
    src.price,
    now(),
    actor.user_id,
    now(),
    actor.user_id
FROM src
CROSS JOIN actor
WHERE NOT EXISTS (
    SELECT 1
    FROM instruments dst
    WHERE upper(dst.ticker) = upper(src.ticker)
       OR upper(dst.isin) = upper(src.isin)
       OR (src.figi IS NOT NULL AND upper(dst.figi) = upper(src.figi))
);

COMMIT;
