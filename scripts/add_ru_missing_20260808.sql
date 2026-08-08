BEGIN;

-- Source: official MOEX ISS security descriptions and TQCB board history,
-- verified on 2026-08-08. Adds the eight bonds missing from broker import.

INSERT INTO currencies (id, name)
VALUES
    ('RUB', 'RUB'),
    ('USD', 'USD'),
    ('CNY', 'CNY')
ON CONFLICT (id) DO NOTHING;

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
src (name, ticker, isin, currency_id, registration_number, listing_start) AS (
    VALUES
        ('НОВАТЭК 001Р-04', 'RU000A10BV55', 'RU000A10BV55', 'USD', '4B02-04-00268-E-001P', DATE '2025-06-24'),
        ('ГЛОРАКС 002Р-01', 'RU000A10FMQ8', 'RU000A10FMQ8', 'RUB', '4B02-01-16806-A-002P', DATE '2026-07-10'),
        ('Облачные технологии 001P-01', 'RU000A10FJS0', 'RU000A10FJS0', 'RUB', '4B02-01-00272-L-001P', DATE '2026-07-03'),
        ('Атомэнергопром АО 001Р-16', 'RU000A10FNA0', 'RU000A10FNA0', 'CNY', '4B02-16-55319-E-001P', DATE '2026-07-14'),
        ('Атомэнергопром АО 001Р-15', 'RU000A10FNB8', 'RU000A10FNB8', 'USD', '4B02-15-55319-E-001P', DATE '2026-07-14'),
        ('Селектел 001P-08R', 'RU000A10FMY2', 'RU000A10FMY2', 'RUB', '4B02-08-16765-A-001P', DATE '2026-07-13'),
        ('СФО Сплит Финанс ПВ-3', 'RU000A10FGH9', 'RU000A10FGH9', 'RUB', '4-03-00908-R-001P', DATE '2026-07-21'),
        ('Аэрофлот П02-БО-02', 'RU000A10CS75', 'RU000A10CS75', 'RUB', '4B02-02-00010-A-002P', DATE '2025-09-19')
),
updated AS (
    UPDATE instruments i
    SET
        name = src.name,
        ticker = src.ticker,
        isin = src.isin,
        figi = NULL,
        type = 2,
        currency_id = src.currency_id,
        category_id = 14,
        exchange = 'MOEX',
        country = 'RU',
        price_source = 'MOEX',
        is_trading = true,
        updated_at = now(),
        updated_by = actor.user_id
    FROM src
    CROSS JOIN actor
    WHERE upper(coalesce(i.ticker, '')) = src.ticker
       OR upper(coalesce(i.isin, '')) = src.isin
    RETURNING i.id
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
    price_source,
    created_at,
    created_by,
    updated_at,
    updated_by,
    is_trading
)
SELECT
    (
        substring(md5('instrument:moex:' || src.isin) FROM 1 FOR 8) || '-' ||
        substring(md5('instrument:moex:' || src.isin) FROM 9 FOR 4) || '-' ||
        substring(md5('instrument:moex:' || src.isin) FROM 13 FOR 4) || '-' ||
        substring(md5('instrument:moex:' || src.isin) FROM 17 FOR 4) || '-' ||
        substring(md5('instrument:moex:' || src.isin) FROM 21 FOR 12)
    )::uuid,
    src.name,
    src.ticker,
    src.isin,
    NULL::text,
    2,
    src.currency_id,
    14,
    'MOEX',
    'RU',
    'MOEX',
    now(),
    actor.user_id,
    now(),
    actor.user_id,
    true
FROM src
CROSS JOIN actor
WHERE NOT EXISTS (
    SELECT 1
    FROM instruments i
    WHERE upper(coalesce(i.ticker, '')) = src.ticker
       OR upper(coalesce(i.isin, '')) = src.isin
);

WITH src (ticker, registration_number) AS (
    VALUES
        ('RU000A10BV55', '4B02-04-00268-E-001P'),
        ('RU000A10FMQ8', '4B02-01-16806-A-002P'),
        ('RU000A10FJS0', '4B02-01-00272-L-001P'),
        ('RU000A10FNA0', '4B02-16-55319-E-001P'),
        ('RU000A10FNB8', '4B02-15-55319-E-001P'),
        ('RU000A10FMY2', '4B02-08-16765-A-001P'),
        ('RU000A10FGH9', '4-03-00908-R-001P'),
        ('RU000A10CS75', '4B02-02-00010-A-002P')
),
target AS (
    SELECT i.id AS instrument_id, src.*
    FROM src
    JOIN instruments i
      ON upper(coalesce(i.ticker, '')) = src.ticker
      OR upper(coalesce(i.isin, '')) = src.ticker
),
updated AS (
    UPDATE instrument_aliases ia
    SET
        instrument_id = target.instrument_id,
        alias_code = target.registration_number,
        normalized_alias_code = target.registration_number
    FROM target
    WHERE upper(ia.normalized_alias_code) = upper(target.registration_number)
    RETURNING ia.id
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    (
        substring(md5('instrument-alias:moex:' || target.registration_number) FROM 1 FOR 8) || '-' ||
        substring(md5('instrument-alias:moex:' || target.registration_number) FROM 9 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:' || target.registration_number) FROM 13 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:' || target.registration_number) FROM 17 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:' || target.registration_number) FROM 21 FOR 12)
    )::uuid,
    target.instrument_id,
    target.registration_number,
    target.registration_number
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE upper(ia.normalized_alias_code) = upper(target.registration_number)
);

WITH src (ticker, currency_id, effective_from) AS (
    VALUES
        ('RU000A10BV55', 'USD', timestamptz '2025-06-24 00:00:00+00'),
        ('RU000A10FMQ8', 'RUB', timestamptz '2026-07-10 00:00:00+00'),
        ('RU000A10FJS0', 'RUB', timestamptz '2026-07-03 00:00:00+00'),
        ('RU000A10FNA0', 'CNY', timestamptz '2026-07-14 00:00:00+00'),
        ('RU000A10FNB8', 'USD', timestamptz '2026-07-14 00:00:00+00'),
        ('RU000A10FMY2', 'RUB', timestamptz '2026-07-13 00:00:00+00'),
        ('RU000A10FGH9', 'RUB', timestamptz '2026-07-21 00:00:00+00'),
        ('RU000A10CS75', 'RUB', timestamptz '2025-09-19 00:00:00+00')
),
target AS (
    SELECT i.id AS instrument_id, src.*
    FROM src
    JOIN instruments i
      ON upper(coalesce(i.ticker, '')) = src.ticker
      OR upper(coalesce(i.isin, '')) = src.ticker
),
closed_active_history AS (
    UPDATE instrument_listing_histories h
    SET
        effective_to = target.effective_from - interval '1 day',
        updated_at = now()
    FROM target
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_to IS NULL
      AND h.effective_from <> target.effective_from
    RETURNING h.id
),
updated_start_history AS (
    UPDATE instrument_listing_histories h
    SET
        ticker = target.ticker,
        figi = NULL,
        currency_id = target.currency_id,
        exchange = 'MOEX',
        effective_to = NULL,
        updated_at = now()
    FROM target
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_from = target.effective_from
    RETURNING h.id
)
INSERT INTO instrument_listing_histories (
    id,
    instrument_id,
    ticker,
    figi,
    currency_id,
    exchange,
    effective_from,
    effective_to,
    created_at,
    updated_at
)
SELECT
    (
        substring(md5('listing:moex:' || target.ticker || ':' || target.effective_from::date) FROM 1 FOR 8) || '-' ||
        substring(md5('listing:moex:' || target.ticker || ':' || target.effective_from::date) FROM 9 FOR 4) || '-' ||
        substring(md5('listing:moex:' || target.ticker || ':' || target.effective_from::date) FROM 13 FOR 4) || '-' ||
        substring(md5('listing:moex:' || target.ticker || ':' || target.effective_from::date) FROM 17 FOR 4) || '-' ||
        substring(md5('listing:moex:' || target.ticker || ':' || target.effective_from::date) FROM 21 FOR 12)
    )::uuid,
    target.instrument_id,
    target.ticker,
    NULL::text,
    target.currency_id,
    'MOEX',
    target.effective_from,
    NULL::timestamptz,
    now(),
    now()
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_listing_histories h
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_from = target.effective_from
);

DO $$
DECLARE
    missing_isins text;
BEGIN
    WITH required(isin) AS (
        VALUES
            ('RU000A10BV55'),
            ('RU000A10FMQ8'),
            ('RU000A10FJS0'),
            ('RU000A10FNA0'),
            ('RU000A10FNB8'),
            ('RU000A10FMY2'),
            ('RU000A10FGH9'),
            ('RU000A10CS75')
    )
    SELECT string_agg(required.isin, ', ' ORDER BY required.isin)
    INTO missing_isins
    FROM required
    WHERE NOT EXISTS (
        SELECT 1
        FROM instruments i
        WHERE upper(coalesce(i.isin, '')) = required.isin
          AND upper(coalesce(i.price_source::text, '')) = 'MOEX'
          AND i.is_trading
    );

    IF missing_isins IS NOT NULL THEN
        RAISE EXCEPTION 'Missing MOEX bonds after import: %', missing_isins;
    END IF;
END $$;

COMMIT;
