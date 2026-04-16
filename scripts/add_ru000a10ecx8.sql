BEGIN;

-- Source: MOEX n97872 and issue history, verified on 2026-04-16.
-- RU000A10ECX8 = АО "Полипласт", П02-БО-14, reg. number 4B02-15-06757-A-002P
-- Listing start: 2026-02-25, board TQCB, listing level 2, nominal currency USD.

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
updated_instrument AS (
    UPDATE instruments
    SET
        name = 'Полипласт АО П02-БО-14',
        ticker = 'RU000A10ECX8',
        isin = 'RU000A10ECX8',
        figi = NULL,
        type = 2,
        currency_id = 'USD',
        category_id = 14,
        exchange = 'TQCB',
        country = 'RU',
        price_source = 'MOEX',
        price = NULL,
        is_trading = true,
        updated_at = now(),
        updated_by = (SELECT user_id FROM actor)
    WHERE upper(coalesce(ticker, '')) = 'RU000A10ECX8'
       OR upper(coalesce(isin, '')) = 'RU000A10ECX8'
    RETURNING id
),
inserted_instrument AS (
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
        price,
        created_at,
        created_by,
        updated_at,
        updated_by,
        is_trading
    )
    SELECT
        '2bb1c205-44df-4f4a-9f68-1d4a8e837bb3'::uuid,
        'Полипласт АО П02-БО-14',
        'RU000A10ECX8',
        'RU000A10ECX8',
        NULL::text,
        2,
        'USD',
        14,
        'TQCB',
        'RU',
        'MOEX',
        NULL::numeric(18,4),
        now(),
        actor.user_id,
        now(),
        actor.user_id,
        true
    FROM actor
    WHERE NOT EXISTS (
        SELECT 1
        FROM instruments
        WHERE upper(coalesce(ticker, '')) = 'RU000A10ECX8'
           OR upper(coalesce(isin, '')) = 'RU000A10ECX8'
    )
    RETURNING id
),
target_instrument AS (
    SELECT id FROM updated_instrument
    UNION ALL
    SELECT id FROM inserted_instrument
)
UPDATE instrument_aliases ia
SET
    instrument_id = ti.id,
    alias_code = '4B02-15-06757-A-002P',
    normalized_alias_code = '4B02-15-06757-A-002P'
FROM target_instrument ti
WHERE ia.normalized_alias_code = '4B02-15-06757-A-002P';

WITH target_instrument AS (
    SELECT id
    FROM instruments
    WHERE upper(coalesce(ticker, '')) = 'RU000A10ECX8'
       OR upper(coalesce(isin, '')) = 'RU000A10ECX8'
),
closed_active_history AS (
    UPDATE instrument_listing_histories h
    SET
        effective_to = timestamptz '2026-02-24 00:00:00+00',
        updated_at = now()
    WHERE h.instrument_id IN (SELECT id FROM target_instrument)
      AND h.effective_to IS NULL
      AND h.effective_from <> timestamptz '2026-02-25 00:00:00+00'
    RETURNING h.id
),
updated_start_history AS (
    UPDATE instrument_listing_histories h
    SET
        ticker = 'RU000A10ECX8',
        figi = NULL,
        currency_id = 'USD',
        exchange = 'TQCB',
        effective_to = NULL,
        updated_at = now()
    WHERE h.instrument_id IN (SELECT id FROM target_instrument)
      AND h.effective_from = timestamptz '2026-02-25 00:00:00+00'
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
    '0af79ea4-dcc0-4d93-a4a0-c97f55f86116'::uuid,
    ti.id,
    'RU000A10ECX8',
    NULL::text,
    'USD',
    'TQCB',
    timestamptz '2026-02-25 00:00:00+00',
    NULL::timestamptz,
    now(),
    now()
FROM target_instrument ti
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_listing_histories h
    WHERE h.instrument_id = ti.id
      AND h.effective_from = timestamptz '2026-02-25 00:00:00+00'
);

WITH target_instrument AS (
    SELECT id
    FROM instruments
    WHERE upper(coalesce(ticker, '')) = 'RU000A10ECX8'
       OR upper(coalesce(isin, '')) = 'RU000A10ECX8'
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    '6e3b6f42-cb31-4937-96eb-3655cf6015dc'::uuid,
    ti.id,
    '4B02-15-06757-A-002P',
    '4B02-15-06757-A-002P'
FROM target_instrument ti
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE ia.normalized_alias_code = '4B02-15-06757-A-002P'
);

COMMIT;
