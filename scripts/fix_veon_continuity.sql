BEGIN;

DO $$
DECLARE
    current_veon_id uuid;
    legacy_veon_id uuid;
    moved_operations_count integer;
    inserted_reverse_split_count integer;
BEGIN
    SELECT id
    INTO current_veon_id
    FROM instruments
    WHERE ticker = 'VEON'
    ORDER BY created_at
    LIMIT 1;

    IF current_veon_id IS NULL THEN
        RAISE EXCEPTION 'VEON instrument was not found.';
    END IF;

    SELECT id
    INTO legacy_veon_id
    FROM instruments
    WHERE ticker = 'VEONold'
       OR isin = 'US91822M1062'
    ORDER BY created_at
    LIMIT 1;

    IF legacy_veon_id IS NULL THEN
        RAISE NOTICE 'Legacy VEON instrument was not found. Continuity metadata will still be ensured.';
    END IF;

    INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
    SELECT
        '0d74d460-c5b2-4736-b993-6ac51e836f8b'::uuid,
        current_veon_id,
        'US91822M1062',
        'US91822M1062'
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_aliases ia
        WHERE ia.normalized_alias_code = 'US91822M1062'
    );

    INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
    SELECT
        '93393046-b9bc-4f19-b8db-0f0fb81fa537'::uuid,
        current_veon_id,
        'VEONold',
        'VEONOLD'
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_aliases ia
        WHERE ia.normalized_alias_code = 'VEONOLD'
    );

    INSERT INTO instrument_corporate_actions (id, instrument_id, type, factor, effective_date, note)
    SELECT
        'b0af630e-7316-44a9-b2fd-81a56d0e22d8'::uuid,
        current_veon_id,
        12,
        0.04,
        timestamptz '2023-03-09 00:00:00+00',
        'System continuity: VEON ADR ratio change 1:25 on 2023-03-09'
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_corporate_actions c
        WHERE c.instrument_id = current_veon_id
          AND c.type = 12
          AND c.effective_date = timestamptz '2023-03-09 00:00:00+00'
    );

    CREATE TEMP TABLE tmp_veon_affected_portfolios ON COMMIT DROP AS
    SELECT DISTINCT o.portfolio_id
    FROM operations o
    WHERE legacy_veon_id IS NOT NULL
      AND o.instrument_id = legacy_veon_id;

    IF legacy_veon_id IS NOT NULL THEN
        UPDATE operations
        SET
            instrument_id = current_veon_id,
            updated_at = now()
        WHERE instrument_id = legacy_veon_id;

        GET DIAGNOSTICS moved_operations_count = ROW_COUNT;
    ELSE
        moved_operations_count := 0;
    END IF;

    INSERT INTO operations (
        id,
        portfolio_id,
        instrument_id,
        broker_operation_key,
        type,
        quantity,
        price,
        fee,
        currency_id,
        trade_date,
        settlement_date,
        note,
        created_at,
        updated_at
    )
    SELECT
        gen_random_uuid(),
        p.portfolio_id,
        current_veon_id,
        NULL,
        12,
        0.04,
        0,
        0,
        'USD',
        timestamptz '2023-03-09 00:00:00+00',
        timestamptz '2023-03-09 00:00:00+00',
        'System continuity: VEON ADR ratio change 1:25 on 2023-03-09',
        now(),
        now()
    FROM tmp_veon_affected_portfolios p
    WHERE NOT EXISTS (
        SELECT 1
        FROM operations o
        WHERE o.portfolio_id = p.portfolio_id
          AND o.instrument_id = current_veon_id
          AND o.type = 12
          AND o.trade_date = timestamptz '2023-03-09 00:00:00+00'
          AND o.note = 'System continuity: VEON ADR ratio change 1:25 on 2023-03-09'
    );

    GET DIAGNOSTICS inserted_reverse_split_count = ROW_COUNT;

    CREATE TEMP TABLE tmp_veon_new_keys ON COMMIT DROP AS
    WITH veon_ops AS (
        SELECT
            o.id,
            o.portfolio_id,
            o.trade_date,
            o.created_at,
            md5(
                concat_ws(
                    '|',
                    'v1',
                    o.type::text,
                    'US91822M5022',
                    to_char(o.quantity, 'FM9999999999999990.000000'),
                    to_char(o.price, 'FM9999999999999990.000000'),
                    to_char(o.fee, 'FM9999999999999990.0000'),
                    o.currency_id,
                    to_char((o.trade_date AT TIME ZONE 'UTC')::date, 'YYYY-MM-DD'),
                    COALESCE(to_char((o.settlement_date AT TIME ZONE 'UTC')::date, 'YYYY-MM-DD'), ''),
                    btrim(COALESCE(o.note, ''))
                )
            ) AS base_hash
        FROM operations o
        WHERE o.instrument_id = current_veon_id
          AND EXISTS (
              SELECT 1
              FROM tmp_veon_affected_portfolios p
              WHERE p.portfolio_id = o.portfolio_id
          )
    )
    SELECT
        id,
        'v2:' || base_hash || ':' ||
        lpad(
            row_number() OVER (
                PARTITION BY portfolio_id, base_hash
                ORDER BY trade_date, created_at, id
            )::text,
            6,
            '0'
        ) AS broker_operation_key
    FROM veon_ops;

    UPDATE operations o
    SET broker_operation_key = NULL
    WHERE EXISTS (
        SELECT 1
        FROM tmp_veon_new_keys k
        WHERE k.id = o.id
    );

    UPDATE operations o
    SET
        broker_operation_key = k.broker_operation_key,
        updated_at = now()
    FROM tmp_veon_new_keys k
    WHERE k.id = o.id;

    RAISE NOTICE
        'VEON continuity fix applied. moved_operations=%, inserted_reverse_splits=%, affected_portfolios=%',
        moved_operations_count,
        inserted_reverse_split_count,
        (SELECT count(*) FROM tmp_veon_affected_portfolios);
END $$;

SELECT portfolio_id
FROM tmp_veon_affected_portfolios
ORDER BY portfolio_id;

COMMIT;
