BEGIN;

-- Source: MOEX ISS, verified on 2026-06-11.
-- RU000A10F850 = РОЛЬФ БО 001Р-09, primary board TQCB, nominal currency RUB,
-- registration number 4B02-09-16689-A-001P, listing start 2026-05-26.
-- The supplied TQOD issue link has no trade history; MOEX ISS marks TQCB as primary.
-- RU000A10FA72 = АФК Система БО 002Р-14, primary board TQCB, nominal currency RUB,
-- registration number 4B02-14-01669-A-002P, listing start 2026-06-02.

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
src (id, name, ticker, isin, currency_id, category_id, registration_number, listing_start, listing_id, alias_id) AS (
    VALUES
        (
            'f460f751-8478-4222-b3f7-bb3d0a8b3411'::uuid,
            'РОЛЬФ БО 001Р-09',
            'RU000A10F850',
            'RU000A10F850',
            'RUB',
            14,
            '4B02-09-16689-A-001P',
            timestamptz '2026-05-26 00:00:00+00',
            '0dc1e56d-d580-4976-bf38-5fa642a965bb'::uuid,
            'a770c133-f161-4f60-8825-87f23fcb73bc'::uuid
        ),
        (
            '50c2130e-be6e-48da-af26-0bae0ba18324'::uuid,
            'АФК Система БО 002Р-14',
            'RU000A10FA72',
            'RU000A10FA72',
            'RUB',
            14,
            '4B02-14-01669-A-002P',
            timestamptz '2026-06-02 00:00:00+00',
            '76d0b6e8-587b-4f80-b9f8-dd5e34cf600d'::uuid,
            '729ffa90-bfcd-491b-a142-69e62f962f4f'::uuid
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
        category_id = src.category_id,
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
    src.id,
    src.name,
    src.ticker,
    src.isin,
    NULL::text,
    2,
    src.currency_id,
    src.category_id,
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

WITH src (ticker, registration_number, alias_id) AS (
    VALUES
        ('RU000A10F850', '4B02-09-16689-A-001P', 'a770c133-f161-4f60-8825-87f23fcb73bc'::uuid),
        ('RU000A10FA72', '4B02-14-01669-A-002P', '729ffa90-bfcd-491b-a142-69e62f962f4f'::uuid)
),
target AS (
    SELECT i.id AS instrument_id, src.registration_number, src.alias_id
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
    WHERE ia.normalized_alias_code = target.registration_number
    RETURNING ia.id
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT target.alias_id, target.instrument_id, target.registration_number, target.registration_number
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE ia.normalized_alias_code = target.registration_number
);

WITH src (ticker, effective_from, listing_id) AS (
    VALUES
        ('RU000A10F850', timestamptz '2026-05-26 00:00:00+00', '0dc1e56d-d580-4976-bf38-5fa642a965bb'::uuid),
        ('RU000A10FA72', timestamptz '2026-06-02 00:00:00+00', '76d0b6e8-587b-4f80-b9f8-dd5e34cf600d'::uuid)
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
        currency_id = 'RUB',
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
    target.listing_id,
    target.instrument_id,
    target.ticker,
    NULL::text,
    'RUB',
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

COMMIT;
