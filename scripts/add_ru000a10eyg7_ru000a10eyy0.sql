BEGIN;

-- Source: MOEX ISS, verified on 2026-05-06.
-- History is loaded through 2026-05-05 (yesterday in Europe/Moscow).
-- RU000A10EYG7 = Polyus PBO-06, board TQCB, nominal currency CNY, listing start 2026-04-27.
-- RU000A10EYY0 = Medscan 001P-02, board TQCB, nominal currency RUB, listing start 2026-04-28.

INSERT INTO currencies (id, name)
VALUES ('CNY', 'CNY')
ON CONFLICT (id) DO NOTHING;

WITH actor AS (
    SELECT '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid AS user_id
),
src (id, name, ticker, isin, type, currency_id, category_id, exchange, country, price_source, is_trading) AS (
    VALUES
        (
            '478e7964-0fc1-4b84-b118-c9d291288ad3'::uuid,
            'Полюс ПБО-06',
            'RU000A10EYG7',
            'RU000A10EYG7',
            2,
            'CNY',
            14,
            'MOEX',
            'RU',
            'MOEX',
            true
        ),
        (
            'bb85c2a0-7feb-450e-a90b-76bda975638f'::uuid,
            'Медскан 001Р-02',
            'RU000A10EYY0',
            'RU000A10EYY0',
            2,
            'RUB',
            21,
            'MOEX',
            'RU',
            'MOEX',
            true
        )
),
updated AS (
    UPDATE instruments i
    SET
        name = src.name,
        ticker = src.ticker,
        isin = src.isin,
        figi = NULL,
        type = src.type,
        currency_id = src.currency_id,
        category_id = src.category_id,
        exchange = src.exchange,
        country = src.country,
        price_source = src.price_source,
        is_trading = src.is_trading,
        updated_at = now(),
        updated_by = (SELECT user_id FROM actor)
    FROM src
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
    src.type,
    src.currency_id,
    src.category_id,
    src.exchange,
    src.country,
    src.price_source,
    now(),
    actor.user_id,
    now(),
    actor.user_id,
    src.is_trading
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
        ('RU000A10EYG7', '4B02-06-55192-E-001P'),
        ('RU000A10EYY0', '4B02-02-00305-G-001P')
),
target AS (
    SELECT i.id, src.registration_number
    FROM src
    JOIN instruments i
      ON upper(coalesce(i.ticker, '')) = src.ticker
      OR upper(coalesce(i.isin, '')) = src.ticker
),
updated AS (
    UPDATE instrument_aliases ia
    SET
        instrument_id = target.id,
        alias_code = target.registration_number,
        normalized_alias_code = target.registration_number
    FROM target
    WHERE ia.normalized_alias_code = target.registration_number
    RETURNING ia.id
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    CASE target.registration_number
        WHEN '4B02-06-55192-E-001P' THEN '7a24bc55-4029-4c32-aa8f-5c2d6c2e605f'::uuid
        ELSE '83c93788-ea2d-4517-84ab-1904460518f3'::uuid
    END,
    target.id,
    target.registration_number,
    target.registration_number
FROM target
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE ia.normalized_alias_code = target.registration_number
);

WITH src (ticker, currency_id, exchange, effective_from, listing_id) AS (
    VALUES
        ('RU000A10EYG7', 'CNY', 'MOEX', timestamptz '2026-04-27 00:00:00+00', 'ee24f730-0f21-4ce9-9f58-3ab90805e59e'::uuid),
        ('RU000A10EYY0', 'RUB', 'MOEX', timestamptz '2026-04-28 00:00:00+00', '322f491f-aa2d-4a5b-9bc0-3227a31aaaa3'::uuid)
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
        exchange = target.exchange,
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
    target.exchange,
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

CREATE TEMP TABLE stg_moex_prices_src (
    ticker text NOT NULL,
    trade_date date NOT NULL,
    raw_price numeric(18,8) NOT NULL,
    trade_currency_id text NULL,
    face_value numeric(18,8) NULL,
    face_currency_id text NULL,
    accrued_interest numeric(18,8) NULL
) ON COMMIT DROP;

INSERT INTO stg_moex_prices_src (ticker, trade_date, raw_price, trade_currency_id, face_value, face_currency_id, accrued_interest)
VALUES
    ('RU000A10EYG7', DATE '2026-04-27', 99.8351, 'SUR', 1000, 'CNY', 0),
    ('RU000A10EYG7', DATE '2026-04-28', 101.829, 'SUR', 1000, 'CNY', 2.2998),
    ('RU000A10EYG7', DATE '2026-04-29', 101.4092, 'SUR', 1000, 'CNY', 4.5956),
    ('RU000A10EYG7', DATE '2026-04-30', 101.6, 'SUR', 1000, 'CNY', 6.9007),
    ('RU000A10EYG7', DATE '2026-05-04', 102.2998, 'SUR', 1000, 'CNY', 16.1102),
    ('RU000A10EYG7', DATE '2026-05-05', 101.3598, 'SUR', 1000, 'CNY', 18.5376),
    ('RU000A10EYY0', DATE '2026-04-28', 100, 'SUR', 1000, 'RUB', 0),
    ('RU000A10EYY0', DATE '2026-04-29', 100.02, 'SUR', 1000, 'RUB', 0.42),
    ('RU000A10EYY0', DATE '2026-04-30', 100.25, 'SUR', 1000, 'RUB', 0.84),
    ('RU000A10EYY0', DATE '2026-05-04', 100.25, 'SUR', 1000, 'RUB', 2.52),
    ('RU000A10EYY0', DATE '2026-05-05', 100.16, 'SUR', 1000, 'RUB', 2.94);

WITH src (base_currency_id, quote_currency_id, rate_date, rate) AS (
    VALUES
        ('CNY', 'RUB', DATE '2026-04-27', 11.0270),
        ('CNY', 'RUB', DATE '2026-04-28', 10.9513),
        ('CNY', 'RUB', DATE '2026-04-29', 10.9420),
        ('CNY', 'RUB', DATE '2026-04-30', 10.9535),
        ('CNY', 'RUB', DATE '2026-05-04', 10.9593),
        ('CNY', 'RUB', DATE '2026-05-05', 11.0343)
)
INSERT INTO fx_rates (
    id,
    base_currency_id,
    quote_currency_id,
    date,
    rate,
    source,
    created_at,
    updated_at
)
SELECT
    (
        SUBSTRING(md5('fx:cbr:' || base_currency_id || ':' || quote_currency_id || ':' || rate_date::text) FROM 1 FOR 8) || '-' ||
        SUBSTRING(md5('fx:cbr:' || base_currency_id || ':' || quote_currency_id || ':' || rate_date::text) FROM 9 FOR 4) || '-' ||
        SUBSTRING(md5('fx:cbr:' || base_currency_id || ':' || quote_currency_id || ':' || rate_date::text) FROM 13 FOR 4) || '-' ||
        SUBSTRING(md5('fx:cbr:' || base_currency_id || ':' || quote_currency_id || ':' || rate_date::text) FROM 17 FOR 4) || '-' ||
        SUBSTRING(md5('fx:cbr:' || base_currency_id || ':' || quote_currency_id || ':' || rate_date::text) FROM 21 FOR 12)
    )::uuid,
    base_currency_id,
    quote_currency_id,
    (rate_date::timestamp AT TIME ZONE 'UTC'),
    rate,
    'CBR',
    now(),
    now()
FROM src
ON CONFLICT (base_currency_id, quote_currency_id, date)
DO UPDATE
SET
    rate = EXCLUDED.rate,
    source = EXCLUDED.source,
    updated_at = now();

DO $$
DECLARE
    missing_fx_dates text;
BEGIN
    SELECT string_agg(src.trade_date::text, ', ' ORDER BY src.trade_date)
    INTO missing_fx_dates
    FROM stg_moex_prices_src src
    JOIN instruments i
      ON upper(coalesce(i.ticker, '')) = src.ticker
      OR upper(coalesce(i.isin, '')) = src.ticker
    WHERE src.accrued_interest > 0
      AND upper(coalesce(src.trade_currency_id, '')) IN ('SUR', 'RUR', 'RUB')
      AND i.currency_id = 'CNY'
      AND NOT EXISTS (
          SELECT 1
          FROM fx_rates fx
          WHERE fx.base_currency_id = 'RUB'
            AND fx.quote_currency_id = 'CNY'
            AND fx.date::date <= src.trade_date
      )
      AND NOT EXISTS (
          SELECT 1
          FROM fx_rates fx
          WHERE fx.base_currency_id = 'CNY'
            AND fx.quote_currency_id = 'RUB'
            AND fx.date::date <= src.trade_date
      );

    IF missing_fx_dates IS NOT NULL THEN
        RAISE EXCEPTION 'Missing RUB/CNY FX rates for accrued interest conversion on: %', missing_fx_dates;
    END IF;
END $$;

CREATE TEMP TABLE stg_moex_prices_resolved ON COMMIT DROP AS
WITH resolved_base AS (
    SELECT
        i.id AS instrument_id,
        src.trade_date,
        i.type AS instrument_type,
        src.raw_price,
        CASE
            WHEN upper(coalesce(src.trade_currency_id, '')) IN ('SUR', 'RUR') THEN 'RUB'
            WHEN nullif(upper(coalesce(src.trade_currency_id, '')), '') IS NULL THEN i.currency_id
            ELSE upper(src.trade_currency_id)
        END AS trade_currency_id,
        src.face_value,
        CASE
            WHEN upper(coalesce(src.face_currency_id, '')) IN ('SUR', 'RUR') THEN 'RUB'
            WHEN nullif(upper(coalesce(src.face_currency_id, '')), '') IS NULL THEN i.currency_id
            ELSE upper(src.face_currency_id)
        END AS face_currency_id,
        coalesce(src.accrued_interest, 0) AS accrued_interest,
        i.currency_id,
        md5(i.id::text || '|' || src.trade_date::text || '|MOEX') AS hash_key
    FROM stg_moex_prices_src src
    JOIN LATERAL (
        SELECT instrument.*
        FROM instruments instrument
        WHERE upper(coalesce(instrument.ticker, '')) = upper(src.ticker)
           OR upper(coalesce(instrument.isin, '')) = upper(src.ticker)
           OR upper(coalesce(instrument.figi, '')) = upper(src.ticker)
           OR EXISTS (
               SELECT 1
               FROM instrument_aliases ia
               WHERE ia.instrument_id = instrument.id
                 AND upper(ia.normalized_alias_code) = upper(src.ticker)
           )
        ORDER BY
            CASE WHEN upper(coalesce(instrument.ticker, '')) = upper(src.ticker) THEN 0 ELSE 1 END,
            CASE WHEN upper(coalesce(instrument.isin, '')) = upper(src.ticker) THEN 0 ELSE 1 END,
            CASE WHEN upper(coalesce(instrument.figi, '')) = upper(src.ticker) THEN 0 ELSE 1 END,
            instrument.created_at
        LIMIT 1
    ) i ON TRUE
),
resolved AS (
    SELECT
        rb.instrument_id,
        rb.trade_date,
        CASE
            WHEN rb.instrument_type = 2 AND coalesce(rb.face_value, 0) > 0 THEN (
                CASE
                    WHEN rb.face_currency_id = rb.currency_id THEN (rb.raw_price / 100.0) * rb.face_value
                    WHEN clean_direct.rate IS NOT NULL THEN ((rb.raw_price / 100.0) * rb.face_value) * clean_direct.rate
                    WHEN clean_inverse.rate IS NOT NULL AND clean_inverse.rate <> 0 THEN ((rb.raw_price / 100.0) * rb.face_value) / clean_inverse.rate
                    ELSE (rb.raw_price / 100.0) * rb.face_value
                END
                +
                CASE
                    WHEN rb.accrued_interest = 0 THEN 0
                    WHEN rb.trade_currency_id = rb.currency_id THEN rb.accrued_interest
                    WHEN accrued_direct.rate IS NOT NULL THEN rb.accrued_interest * accrued_direct.rate
                    WHEN accrued_inverse.rate IS NOT NULL AND accrued_inverse.rate <> 0 THEN rb.accrued_interest / accrued_inverse.rate
                    ELSE rb.accrued_interest
                END
            )::numeric(18,4)
            ELSE rb.raw_price::numeric(18,4)
        END AS price,
        rb.currency_id,
        rb.hash_key
    FROM resolved_base rb
    LEFT JOIN LATERAL (
        SELECT fx.rate
        FROM fx_rates fx
        WHERE fx.base_currency_id = rb.face_currency_id
          AND fx.quote_currency_id = rb.currency_id
          AND fx.date::date <= rb.trade_date
        ORDER BY fx.date DESC, fx.created_at DESC
        LIMIT 1
    ) AS clean_direct ON true
    LEFT JOIN LATERAL (
        SELECT fx.rate
        FROM fx_rates fx
        WHERE fx.base_currency_id = rb.currency_id
          AND fx.quote_currency_id = rb.face_currency_id
          AND fx.date::date <= rb.trade_date
        ORDER BY fx.date DESC, fx.created_at DESC
        LIMIT 1
    ) AS clean_inverse ON true
    LEFT JOIN LATERAL (
        SELECT fx.rate
        FROM fx_rates fx
        WHERE fx.base_currency_id = rb.trade_currency_id
          AND fx.quote_currency_id = rb.currency_id
          AND fx.date::date <= rb.trade_date
        ORDER BY fx.date DESC, fx.created_at DESC
        LIMIT 1
    ) AS accrued_direct ON true
    LEFT JOIN LATERAL (
        SELECT fx.rate
        FROM fx_rates fx
        WHERE fx.base_currency_id = rb.currency_id
          AND fx.quote_currency_id = rb.trade_currency_id
          AND fx.date::date <= rb.trade_date
        ORDER BY fx.date DESC, fx.created_at DESC
        LIMIT 1
    ) AS accrued_inverse ON true
)
SELECT
    instrument_id,
    trade_date,
    price,
    currency_id,
    hash_key
FROM resolved;

WITH upserted AS (
    INSERT INTO prices (id, instrument_id, date, value, currency_id, provider, created_at, updated_at)
    SELECT
        (
            substr(hash_key, 1, 8) || '-' ||
            substr(hash_key, 9, 4) || '-' ||
            substr(hash_key, 13, 4) || '-' ||
            substr(hash_key, 17, 4) || '-' ||
            substr(hash_key, 21, 12)
        )::uuid,
        instrument_id,
        (trade_date::timestamp AT TIME ZONE 'UTC'),
        price,
        currency_id,
        'MOEX',
        now(),
        now()
    FROM stg_moex_prices_resolved
    ON CONFLICT (instrument_id, date, provider)
    DO UPDATE
    SET value = EXCLUDED.value,
        currency_id = EXCLUDED.currency_id,
        updated_at = now()
    RETURNING instrument_id
)
SELECT
    11 AS expected_rows,
    (SELECT count(*) FROM stg_moex_prices_resolved) AS resolved_rows,
    count(*) AS applied_rows
FROM upserted;

DO $$
DECLARE
    expected_rows integer := 11;
    source_rows integer;
    resolved_rows integer;
    applied_rows integer;
BEGIN
    SELECT count(*) INTO source_rows FROM stg_moex_prices_src;
    SELECT count(*) INTO resolved_rows FROM stg_moex_prices_resolved;

    SELECT count(*) INTO applied_rows
    FROM prices p
    JOIN stg_moex_prices_resolved r
      ON r.instrument_id = p.instrument_id
     AND p.date::date = r.trade_date
     AND p.provider = 'MOEX';

    IF source_rows <> expected_rows OR resolved_rows <> expected_rows OR applied_rows <> expected_rows THEN
        RAISE EXCEPTION 'MOEX backfill mismatch: expected %, source %, resolved %, applied %',
            expected_rows, source_rows, resolved_rows, applied_rows;
    END IF;
END $$;

COMMIT;
