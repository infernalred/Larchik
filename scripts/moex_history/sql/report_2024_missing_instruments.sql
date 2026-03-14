BEGIN;

-- Existing MTSS row has FIGI stored in both isin/figi. Fix the ISIN in-place
-- so report imports resolve to the existing instrument and reuse its prices.
UPDATE instruments
SET isin = 'RU0007775219',
    figi = COALESCE(figi, 'BBG004S681W1'),
    updated_at = now(),
    updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
WHERE upper(ticker) = 'MTSS'
  AND upper(isin) = 'BBG004S681W1';

WITH src (name, ticker, isin, figi, type, currency_id, category_id, exchange, country, price) AS (
    VALUES
        ('паи БПИФ рфи Т-КапиталДивидАкц', 'TDIV', 'RU000A107563', NULL::text, 3, 'RUB', 22, NULL::text, NULL::text, NULL::numeric(18,4)),
        ('Yandex clA', 'NBIS', 'NL0009805522', NULL::text, 1, 'RUB', 25, NULL::text, 'NL', NULL::numeric(18,4)),
        ('АйДи Коллект обб04', 'RU000A107C34', 'RU000A107C34', NULL::text, 2, 'RUB', 14, NULL::text, 'RU', NULL::numeric(18,4)),
        ('КОНТРОЛ лизинг оббП01', 'RU000A106T85', 'RU000A106T85', NULL::text, 2, 'RUB', 14, NULL::text, 'RU', NULL::numeric(18,4)),
        ('Хайтэк-Интеграция БО-01', 'RU000A104TM1', 'RU000A104TM1', NULL::text, 2, 'RUB', 14, 'TQCB', 'RU', NULL::numeric(18,4))
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
        substring(md5('report-2024:' || src.ticker) from 1 for 8) || '-' ||
        substring(md5('report-2024:' || src.ticker) from 9 for 4) || '-' ||
        substring(md5('report-2024:' || src.ticker) from 13 for 4) || '-' ||
        substring(md5('report-2024:' || src.ticker) from 17 for 4) || '-' ||
        substring(md5('report-2024:' || src.ticker) from 21 for 12)
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
