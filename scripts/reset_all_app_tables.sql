BEGIN;

TRUNCATE TABLE
    job_runs,
    job_definitions,
    position_snapshots,
    portfolio_snapshots,
    cash_balances,
    lots,
    operations,
    portfolios,
    prices,
    fx_rates,
    instrument_listing_histories,
    instrument_corporate_actions,
    instrument_aliases,
    instruments,
    brokers,
    categories,
    currencies
RESTART IDENTITY CASCADE;

DO $$
DECLARE
    currencies_count bigint;
    categories_count bigint;
    brokers_count bigint;
    instruments_count bigint;
    aliases_count bigint;
    corporate_actions_count bigint;
    listing_history_count bigint;
    fx_count bigint;
    prices_count bigint;
    portfolios_count bigint;
    operations_count bigint;
    lots_count bigint;
    cash_balances_count bigint;
    position_snapshots_count bigint;
    portfolio_snapshots_count bigint;
    job_definitions_count bigint;
    job_runs_count bigint;
BEGIN
    SELECT count(*) INTO currencies_count FROM currencies;
    SELECT count(*) INTO categories_count FROM categories;
    SELECT count(*) INTO brokers_count FROM brokers;
    SELECT count(*) INTO instruments_count FROM instruments;
    SELECT count(*) INTO aliases_count FROM instrument_aliases;
    SELECT count(*) INTO corporate_actions_count FROM instrument_corporate_actions;
    SELECT count(*) INTO listing_history_count FROM instrument_listing_histories;
    SELECT count(*) INTO fx_count FROM fx_rates;
    SELECT count(*) INTO prices_count FROM prices;
    SELECT count(*) INTO portfolios_count FROM portfolios;
    SELECT count(*) INTO operations_count FROM operations;
    SELECT count(*) INTO lots_count FROM lots;
    SELECT count(*) INTO cash_balances_count FROM cash_balances;
    SELECT count(*) INTO position_snapshots_count FROM position_snapshots;
    SELECT count(*) INTO portfolio_snapshots_count FROM portfolio_snapshots;
    SELECT count(*) INTO job_definitions_count FROM job_definitions;
    SELECT count(*) INTO job_runs_count FROM job_runs;

    IF currencies_count <> 0
        OR categories_count <> 0
        OR brokers_count <> 0
        OR instruments_count <> 0
        OR aliases_count <> 0
        OR corporate_actions_count <> 0
        OR listing_history_count <> 0
        OR fx_count <> 0
        OR prices_count <> 0
        OR portfolios_count <> 0
        OR operations_count <> 0
        OR lots_count <> 0
        OR cash_balances_count <> 0
        OR position_snapshots_count <> 0
        OR portfolio_snapshots_count <> 0
        OR job_definitions_count <> 0
        OR job_runs_count <> 0 THEN
        RAISE EXCEPTION
            'Full reset validation failed: currencies=%, categories=%, brokers=%, instruments=%, aliases=%, corporate_actions=%, listing_history=%, fx_rates=%, prices=%, portfolios=%, operations=%, lots=%, cash_balances=%, position_snapshots=%, portfolio_snapshots=%, job_definitions=%, job_runs=%.',
            currencies_count,
            categories_count,
            brokers_count,
            instruments_count,
            aliases_count,
            corporate_actions_count,
            listing_history_count,
            fx_count,
            prices_count,
            portfolios_count,
            operations_count,
            lots_count,
            cash_balances_count,
            position_snapshots_count,
            portfolio_snapshots_count,
            job_definitions_count,
            job_runs_count;
    END IF;

    RAISE NOTICE
        'Full reset validation passed. currencies=%, categories=%, brokers=%, instruments=%, aliases=%, corporate_actions=%, listing_history=%, fx_rates=%, prices=%, portfolios=%, operations=%, lots=%, cash_balances=%, position_snapshots=%, portfolio_snapshots=%, job_definitions=%, job_runs=%.',
        currencies_count,
        categories_count,
        brokers_count,
        instruments_count,
        aliases_count,
        corporate_actions_count,
        listing_history_count,
        fx_count,
        prices_count,
        portfolios_count,
        operations_count,
        lots_count,
        cash_balances_count,
        position_snapshots_count,
        portfolio_snapshots_count,
        job_definitions_count,
        job_runs_count;
END $$;

COMMIT;
