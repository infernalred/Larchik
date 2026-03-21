WITH updated_instrument AS (
    UPDATE instruments
    SET
        name = 'ПАО МТС 002P-17',
        isin = 'RU000A10ELF6',
        figi = 'TCS00A10ELF6',
        type = 2,
        currency_id = 'RUB',
        category_id = 14,
        exchange = 'TQCB',
        country = 'RU',
        price = 998.9400,
        updated_at = now(),
        updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid,
        is_trading = true
    WHERE ticker = 'RU000A10ELF6'
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
        price,
        created_at,
        created_by,
        updated_at,
        updated_by,
        is_trading
    )
    SELECT
        gen_random_uuid(),
        'ПАО МТС 002P-17',
        'RU000A10ELF6',
        'RU000A10ELF6',
        'TCS00A10ELF6',
        2,
        'RUB',
        14,
        'TQCB',
        'RU',
        998.9400,
        now(),
        '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid,
        now(),
        '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid,
        true
    WHERE NOT EXISTS (
        SELECT 1
        FROM instruments
        WHERE ticker = 'RU000A10ELF6'
    )
    RETURNING id
),
instrument_ref AS (
    SELECT id FROM updated_instrument
    UNION ALL
    SELECT id FROM inserted_instrument
    UNION ALL
    SELECT id FROM instruments WHERE ticker = 'RU000A10ELF6'
),
price_rows AS (
    SELECT * FROM (
        VALUES
            ('2026-03-18'::date, 'MOEX'::varchar, 995.3000::numeric, 'RUB'::varchar),
            ('2026-03-19'::date, 'MOEX'::varchar, 998.3700::numeric, 'RUB'::varchar),
            ('2026-03-20'::date, 'MOEX'::varchar, 998.9400::numeric, 'RUB'::varchar),
            ('2026-03-18'::date, 'TBANK'::varchar, 997.9000::numeric, 'RUB'::varchar),
            ('2026-03-19'::date, 'TBANK'::varchar, 998.4700::numeric, 'RUB'::varchar),
            ('2026-03-20'::date, 'TBANK'::varchar, 999.3400::numeric, 'RUB'::varchar)
    ) AS v(price_date, provider, value, currency_id)
)
INSERT INTO prices (
    id,
    instrument_id,
    date,
    value,
    currency_id,
    provider,
    created_at,
    updated_at,
    source_currency_id
)
SELECT
    gen_random_uuid(),
    instrument_ref.id,
    price_rows.price_date::timestamp AT TIME ZONE 'UTC',
    price_rows.value,
    price_rows.currency_id,
    price_rows.provider,
    now(),
    now(),
    NULL
FROM instrument_ref
CROSS JOIN price_rows
ON CONFLICT (instrument_id, date, provider) DO UPDATE
SET
    value = EXCLUDED.value,
    currency_id = EXCLUDED.currency_id,
    updated_at = now(),
    source_currency_id = EXCLUDED.source_currency_id;
