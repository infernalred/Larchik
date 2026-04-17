BEGIN;

DO $$
DECLARE
    current_t_id uuid;
    duplicate_t_ids uuid[];
    duplicate_count integer;
    moved_operations_count integer := 0;
    merged_prices_count integer := 0;
    removed_duplicate_instruments_count integer := 0;
BEGIN
    UPDATE instruments
    SET ticker = 'T@US',
        updated_at = now(),
        updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
    WHERE upper(coalesce(isin, '')) = 'US00206R1023'
      AND upper(ticker) = 'T';

    SELECT id
    INTO current_t_id
    FROM instruments
    WHERE upper(coalesce(isin, '')) = 'RU000A107UL4'
    ORDER BY created_at, id
    LIMIT 1;

    IF current_t_id IS NULL THEN
        RAISE EXCEPTION 'T-Technologies instrument (RU000A107UL4) was not found.';
    END IF;

    SELECT COALESCE(array_agg(id), ARRAY[]::uuid[])
    INTO duplicate_t_ids
    FROM instruments
    WHERE upper(coalesce(isin, '')) = 'RU000A107UL4'
      AND id <> current_t_id;

    duplicate_count := COALESCE(array_length(duplicate_t_ids, 1), 0);

    IF duplicate_count > 0 THEN
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
            current_t_id,
            p.date,
            p.value,
            p.currency_id,
            p.provider,
            p.created_at,
            p.updated_at,
            p.source_currency_id
        FROM prices p
        WHERE p.instrument_id = ANY(duplicate_t_ids)
        ON CONFLICT (instrument_id, date, provider) DO UPDATE
        SET
            value = EXCLUDED.value,
            currency_id = EXCLUDED.currency_id,
            source_currency_id = EXCLUDED.source_currency_id,
            updated_at = GREATEST(prices.updated_at, EXCLUDED.updated_at);

        GET DIAGNOSTICS merged_prices_count = ROW_COUNT;

        UPDATE operations
        SET instrument_id = current_t_id,
            updated_at = now()
        WHERE instrument_id = ANY(duplicate_t_ids);

        GET DIAGNOSTICS moved_operations_count = ROW_COUNT;

        INSERT INTO instrument_corporate_actions (
            id,
            instrument_id,
            type,
            factor,
            effective_date,
            note
        )
        SELECT
            gen_random_uuid(),
            current_t_id,
            c.type,
            c.factor,
            c.effective_date,
            c.note
        FROM instrument_corporate_actions c
        WHERE c.instrument_id = ANY(duplicate_t_ids)
        ON CONFLICT (instrument_id, type, effective_date) DO NOTHING;

        INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
        SELECT
            gen_random_uuid(),
            current_t_id,
            ia.alias_code,
            ia.normalized_alias_code
        FROM instrument_aliases ia
        WHERE ia.instrument_id = ANY(duplicate_t_ids)
          AND NOT EXISTS (
              SELECT 1
              FROM instrument_aliases existing
              WHERE existing.normalized_alias_code = ia.normalized_alias_code
          );

        DELETE FROM prices
        WHERE instrument_id = ANY(duplicate_t_ids);

        DELETE FROM instrument_aliases
        WHERE instrument_id = ANY(duplicate_t_ids);

        DELETE FROM instrument_listing_histories
        WHERE instrument_id = ANY(duplicate_t_ids);

        DELETE FROM instrument_corporate_actions
        WHERE instrument_id = ANY(duplicate_t_ids);

        DELETE FROM instruments
        WHERE id = ANY(duplicate_t_ids);

        GET DIAGNOSTICS removed_duplicate_instruments_count = ROW_COUNT;
    END IF;

    UPDATE instruments
    SET name = 'Т-Технологии МКПАО ао',
        ticker = 'T',
        isin = 'RU000A107UL4',
        currency_id = 'RUB',
        exchange = 'TQBR',
        country = 'RU',
        is_trading = true,
        price_source = 'MOEX',
        updated_at = now(),
        updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
    WHERE id = current_t_id;

    DELETE FROM instrument_aliases
    WHERE normalized_alias_code IN ('TCSG', 'BBG00QPYJ5H0')
      AND instrument_id <> current_t_id;

    INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
    SELECT
        'b7606c41-0f06-4da5-9090-85ef77aebefd'::uuid,
        current_t_id,
        'TCSG',
        'TCSG'
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_aliases ia
        WHERE ia.normalized_alias_code = 'TCSG'
    );

    INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
    SELECT
        '8a3b13be-313d-45c0-b2b2-31305c543819'::uuid,
        current_t_id,
        'BBG00QPYJ5H0',
        'BBG00QPYJ5H0'
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_aliases ia
        WHERE ia.normalized_alias_code = 'BBG00QPYJ5H0'
    );

    DELETE FROM instrument_listing_histories
    WHERE instrument_id = current_t_id;

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
    VALUES
        (
            '9b23d89e-7725-4b5f-9f07-90fc08f0a184'::uuid,
            current_t_id,
            'TCSG',
            'BBG00QPYJ5H0',
            'RUB',
            'TQBR',
            timestamptz '1900-01-01 00:00:00+00',
            timestamptz '2024-11-27 00:00:00+00',
            now(),
            now()
        ),
        (
            '631a83ce-a8c8-4f18-b18f-467f50fc7198'::uuid,
            current_t_id,
            'T',
            COALESCE((SELECT nullif(i.figi, '') FROM instruments i WHERE i.id = current_t_id), 'BBG00QPYJ5H0'),
            'RUB',
            'TQBR',
            timestamptz '2024-11-28 00:00:00+00',
            NULL,
            now(),
            now()
        );

    RAISE NOTICE
        'T-Technologies continuity fix applied. duplicates=%, merged_prices=%, moved_operations=%, removed_duplicate_instruments=%',
        duplicate_count,
        merged_prices_count,
        moved_operations_count,
        removed_duplicate_instruments_count;
END $$;

SELECT
    i.id,
    i.name,
    i.ticker,
    i.isin,
    i.exchange,
    i.country,
    i.is_trading,
    i.price_source,
    max(p.date)::date AS last_price_date
FROM instruments i
LEFT JOIN prices p ON p.instrument_id = i.id
WHERE i.isin = 'RU000A107UL4'
GROUP BY i.id, i.name, i.ticker, i.isin, i.exchange, i.country, i.is_trading, i.price_source
ORDER BY i.id;

COMMIT;
