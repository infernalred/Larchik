BEGIN;

DO $$
DECLARE
    operations_count bigint;
    lots_count bigint;
    position_snapshots_count bigint;
    portfolio_snapshots_count bigint;
BEGIN
    SELECT count(*) INTO operations_count FROM operations;
    SELECT count(*) INTO lots_count FROM lots;
    SELECT count(*) INTO position_snapshots_count FROM position_snapshots;
    SELECT count(*) INTO portfolio_snapshots_count FROM portfolio_snapshots;

    IF operations_count <> 0
        OR lots_count <> 0
        OR position_snapshots_count <> 0
        OR portfolio_snapshots_count <> 0 THEN
        RAISE EXCEPTION
            'Reset aborted: expected empty operations/lots/position_snapshots/portfolio_snapshots, got operations=%, lots=%, position_snapshots=%, portfolio_snapshots=%.',
            operations_count,
            lots_count,
            position_snapshots_count,
            portfolio_snapshots_count;
    END IF;
END $$;

DELETE FROM prices;
DELETE FROM instrument_aliases;
DELETE FROM fx_rates;
DELETE FROM instruments;

DO $$
DECLARE
    prices_count bigint;
    aliases_count bigint;
    fx_count bigint;
    instruments_count bigint;
BEGIN
    SELECT count(*) INTO prices_count FROM prices;
    SELECT count(*) INTO aliases_count FROM instrument_aliases;
    SELECT count(*) INTO fx_count FROM fx_rates;
    SELECT count(*) INTO instruments_count FROM instruments;

    IF prices_count <> 0
        OR aliases_count <> 0
        OR fx_count <> 0
        OR instruments_count <> 0 THEN
        RAISE EXCEPTION
            'Reset validation failed: prices=%, aliases=%, fx_rates=%, instruments=%.',
            prices_count,
            aliases_count,
            fx_count,
            instruments_count;
    END IF;

    RAISE NOTICE
        'Reset validation passed. prices=%, aliases=%, fx_rates=%, instruments=%.',
        prices_count,
        aliases_count,
        fx_count,
        instruments_count;
END $$;

COMMIT;
