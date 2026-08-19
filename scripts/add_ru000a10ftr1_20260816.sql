BEGIN;

-- Source: official MOEX ISS security description and TQCB board history,
-- verified on 2026-08-16.
-- RU000A10FTR1 = ВИС ФИНАНС БО-П14, RUB, listing start 2026-08-06.

INSERT INTO currencies (id, name)
VALUES ('RUB', 'RUB')
ON CONFLICT (id) DO NOTHING;

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
src (name, ticker, isin, currency_id, registration_number, listing_start) AS (
    VALUES (
        'ВИС ФИНАНС БО-П14',
        'RU000A10FTR1',
        'RU000A10FTR1',
        'RUB',
        '4B02-14-00554-R-001P',
        DATE '2026-08-06'
    )
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

WITH target AS (
    SELECT i.id AS instrument_id
    FROM instruments i
    WHERE upper(coalesce(i.ticker, '')) = 'RU000A10FTR1'
       OR upper(coalesce(i.isin, '')) = 'RU000A10FTR1'
),
updated AS (
    UPDATE instrument_aliases ia
    SET
        instrument_id = target.instrument_id,
        alias_code = '4B02-14-00554-R-001P',
        normalized_alias_code = '4B02-14-00554-R-001P'
    FROM target
    WHERE upper(ia.normalized_alias_code) = '4B02-14-00554-R-001P'
    RETURNING ia.id
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    (
        substring(md5('instrument-alias:moex:4B02-14-00554-R-001P') FROM 1 FOR 8) || '-' ||
        substring(md5('instrument-alias:moex:4B02-14-00554-R-001P') FROM 9 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:4B02-14-00554-R-001P') FROM 13 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:4B02-14-00554-R-001P') FROM 17 FOR 4) || '-' ||
        substring(md5('instrument-alias:moex:4B02-14-00554-R-001P') FROM 21 FOR 12)
    )::uuid,
    target.instrument_id,
    '4B02-14-00554-R-001P',
    '4B02-14-00554-R-001P'
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE upper(ia.normalized_alias_code) = '4B02-14-00554-R-001P'
);

WITH target AS (
    SELECT i.id AS instrument_id
    FROM instruments i
    WHERE upper(coalesce(i.ticker, '')) = 'RU000A10FTR1'
       OR upper(coalesce(i.isin, '')) = 'RU000A10FTR1'
),
closed_active_history AS (
    UPDATE instrument_listing_histories h
    SET
        effective_to = timestamptz '2026-08-05 00:00:00+00',
        updated_at = now()
    FROM target
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_to IS NULL
      AND h.effective_from <> timestamptz '2026-08-06 00:00:00+00'
    RETURNING h.id
),
updated_start_history AS (
    UPDATE instrument_listing_histories h
    SET
        ticker = 'RU000A10FTR1',
        figi = NULL,
        currency_id = 'RUB',
        exchange = 'MOEX',
        effective_to = NULL,
        updated_at = now()
    FROM target
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_from = timestamptz '2026-08-06 00:00:00+00'
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
        substring(md5('listing:moex:RU000A10FTR1:2026-08-06') FROM 1 FOR 8) || '-' ||
        substring(md5('listing:moex:RU000A10FTR1:2026-08-06') FROM 9 FOR 4) || '-' ||
        substring(md5('listing:moex:RU000A10FTR1:2026-08-06') FROM 13 FOR 4) || '-' ||
        substring(md5('listing:moex:RU000A10FTR1:2026-08-06') FROM 17 FOR 4) || '-' ||
        substring(md5('listing:moex:RU000A10FTR1:2026-08-06') FROM 21 FOR 12)
    )::uuid,
    target.instrument_id,
    'RU000A10FTR1',
    NULL::text,
    'RUB',
    'MOEX',
    timestamptz '2026-08-06 00:00:00+00',
    NULL::timestamptz,
    now(),
    now()
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_listing_histories h
    WHERE h.instrument_id = target.instrument_id
      AND h.effective_from = timestamptz '2026-08-06 00:00:00+00'
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM instruments i
        WHERE upper(coalesce(i.isin, '')) = 'RU000A10FTR1'
          AND upper(coalesce(i.price_source::text, '')) = 'MOEX'
          AND i.currency_id = 'RUB'
          AND i.is_trading
    ) THEN
        RAISE EXCEPTION 'RU000A10FTR1 was not imported as a trading RUB MOEX instrument.';
    END IF;
END $$;

COMMIT;
