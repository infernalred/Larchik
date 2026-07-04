BEGIN;

-- Source: MOEX ISS, verified on 2026-07-04.
-- Adds missing MOEX exchange bonds:
-- RU000A10EEE4 = Балтийский лизинг ООО БО-П24, RUB, listing start 2026-06-10.
-- RU000A10FC62 = ГМК Нор.никель БО-001Р-17, CNY nominal, listing start 2026-06-16.
-- RU000A105M91 = Синара Транспортные Машины 1P3, RUB, listing start 2022-12-16.
-- RU000A10DYG9 = Атомэнергопром АО 001Р-10, RUB, listing start 2025-12-24.

INSERT INTO currencies (id, name)
VALUES ('CNY', 'CNY')
ON CONFLICT (id) DO NOTHING;

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
src (
    id,
    name,
    ticker,
    isin,
    currency_id,
    registration_number,
    listing_start,
    listing_id,
    alias_id
) AS (
    VALUES
        (
            '6a80585f-08c1-4deb-9ded-f428266c45ce'::uuid,
            'Балтийский лизинг ООО БО-П24',
            'RU000A10EEE4',
            'RU000A10EEE4',
            'RUB',
            '4B02-24-36442-R-001P',
            timestamptz '2026-06-10 00:00:00+00',
            'd35f0888-57ca-435c-bca1-f20545462fe7'::uuid,
            '64b4d879-4bf9-49cc-a0ed-0c291e830c79'::uuid
        ),
        (
            '0ef51c82-e670-4298-9380-5bf74fac0e05'::uuid,
            'ГМК Нор.никель БО-001Р-17',
            'RU000A10FC62',
            'RU000A10FC62',
            'CNY',
            '4B02-17-40155-F-001P',
            timestamptz '2026-06-16 00:00:00+00',
            '75513a8b-f121-4169-a9f1-12eec4596a22'::uuid,
            'bb1e9b45-5297-400b-b0e2-d09a516eccdc'::uuid
        ),
        (
            '9a08b8a2-f209-46f3-857a-48ad03b4b5cd'::uuid,
            'Синара Транспортные Машины 1P3',
            'RU000A105M91',
            'RU000A105M91',
            'RUB',
            '4B02-03-55323-E-001P',
            timestamptz '2022-12-16 00:00:00+00',
            '83b26973-98cb-4980-aff7-b126acf8dc5a'::uuid,
            '8fcc929c-f690-47e3-ac7f-a4baaaac23be'::uuid
        ),
        (
            'c76b157c-1b36-4355-b252-c298cc58f05f'::uuid,
            'Атомэнергопром АО 001Р-10',
            'RU000A10DYG9',
            'RU000A10DYG9',
            'RUB',
            '4B02-10-55319-E-001P',
            timestamptz '2025-12-24 00:00:00+00',
            '62b38c85-e248-4603-a95e-24d0bca90890'::uuid,
            '500128d5-27ec-4c71-956f-61274e19ce3d'::uuid
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
    src.id,
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

WITH src (ticker, registration_number, alias_id) AS (
    VALUES
        ('RU000A10EEE4', '4B02-24-36442-R-001P', '64b4d879-4bf9-49cc-a0ed-0c291e830c79'::uuid),
        ('RU000A10FC62', '4B02-17-40155-F-001P', 'bb1e9b45-5297-400b-b0e2-d09a516eccdc'::uuid),
        ('RU000A105M91', '4B02-03-55323-E-001P', '8fcc929c-f690-47e3-ac7f-a4baaaac23be'::uuid),
        ('RU000A10DYG9', '4B02-10-55319-E-001P', '500128d5-27ec-4c71-956f-61274e19ce3d'::uuid)
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

WITH src (ticker, currency_id, effective_from, listing_id) AS (
    VALUES
        ('RU000A10EEE4', 'RUB', timestamptz '2026-06-10 00:00:00+00', 'd35f0888-57ca-435c-bca1-f20545462fe7'::uuid),
        ('RU000A10FC62', 'CNY', timestamptz '2026-06-16 00:00:00+00', '75513a8b-f121-4169-a9f1-12eec4596a22'::uuid),
        ('RU000A105M91', 'RUB', timestamptz '2022-12-16 00:00:00+00', '83b26973-98cb-4980-aff7-b126acf8dc5a'::uuid),
        ('RU000A10DYG9', 'RUB', timestamptz '2025-12-24 00:00:00+00', '62b38c85-e248-4603-a95e-24d0bca90890'::uuid)
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
    target.listing_id,
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

COMMIT;
