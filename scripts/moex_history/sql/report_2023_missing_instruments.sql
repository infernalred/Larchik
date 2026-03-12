WITH src (ticker, isin, name, type, currency_id, category_id, exchange, country, price) AS (
    VALUES
        ('RU000A105XF4', 'RU000A105XF4', 'ГЛОРАКС 001Р-01', 2, 'RUB', 14, NULL, 'RU', NULL::numeric)
)
INSERT INTO instruments (
    id,
    ticker,
    isin,
    figi,
    name,
    type,
    currency_id,
    category_id,
    exchange,
    country,
    price,
    created_at,
    created_by,
    updated_at
    ,
    updated_by
)
SELECT
    (
        substr(md5('instrument:' || src.ticker), 1, 8) || '-' ||
        substr(md5('instrument:' || src.ticker), 9, 4) || '-' ||
        substr(md5('instrument:' || src.ticker), 13, 4) || '-' ||
        substr(md5('instrument:' || src.ticker), 17, 4) || '-' ||
        substr(md5('instrument:' || src.ticker), 21, 12)
    )::uuid,
    src.ticker,
    src.isin,
    NULL,
    src.name,
    src.type,
    src.currency_id,
    src.category_id,
    src.exchange,
    src.country,
    src.price,
    now(),
    '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid,
    now(),
    '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
FROM src
WHERE NOT EXISTS (
    SELECT 1
    FROM instruments dst
    WHERE upper(dst.ticker) = upper(src.ticker)
       OR upper(coalesce(dst.isin, '')) = upper(src.isin)
);
