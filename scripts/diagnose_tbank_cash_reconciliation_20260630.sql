-- Read-only diagnostics for T-Bank cash reconciliation against the 2026-06-30 UI snapshot.
-- The broker page was captured at 2026-06-30 14:30 MSK, while portfolio_snapshots are end-of-day.

\set portfolio_id '9c1109e4-fce3-4360-9cb2-5f8efbfe57a3'
\set as_of_date '2026-06-30'

WITH expected_cash(currency_id, broker_quantity, broker_value_base) AS (
    VALUES
        ('RUB', 15022.50::numeric, 15420.21::numeric),
        ('USD', 2.30::numeric, 178.83::numeric),
        ('EUR', 0.02::numeric, 1.77::numeric),
        ('CNY', 37.74::numeric, 435.80::numeric)
),
params AS (
    SELECT
        :'portfolio_id'::uuid AS portfolio_id,
        :'as_of_date'::date AS as_of_date
),
ops AS (
    SELECT
        o.*,
        i.ticker,
        i.name AS instrument_name,
        (o.broker_operation_key LIKE 'v2:%' OR o.broker_operation_key LIKE 'v3:%') AS is_imported,
        COALESCE(o.settlement_date, o.trade_date)::date AS settlement_or_trade_date
    FROM operations o
    LEFT JOIN instruments i ON i.id = o.instrument_id
    JOIN params p ON p.portfolio_id = o.portfolio_id
    WHERE o.trade_date::date <= p.as_of_date
),
cash_effects AS (
    SELECT
        o.id,
        o.type,
        o.currency_id,
        o.trade_date,
        o.settlement_date,
        o.ticker,
        o.instrument_name,
        o.note,
        o.broker_operation_key,
        CASE
            WHEN o.is_imported AND o.instrument_id IS NOT NULL THEN o.settlement_or_trade_date
            ELSE o.trade_date::date
        END AS cash_date,
        CASE
            WHEN o.type = 1 AND o.instrument_id IS NOT NULL AND o.is_imported THEN 0
            WHEN o.type = 1 AND o.instrument_id IS NOT NULL THEN -(o.quantity * o.price + o.fee)
            WHEN o.type = 2 AND o.instrument_id IS NOT NULL AND o.is_imported THEN 0
            WHEN o.type = 2 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 3 THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 4 THEN CASE WHEN o.price <> 0 THEN -o.price ELSE -o.fee END
            WHEN o.type = 5 THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 6 THEN -(CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END)
            WHEN o.type = 7 AND o.instrument_id IS NULL THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 8 AND o.instrument_id IS NULL THEN -(CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END)
            WHEN o.type = 9 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 10 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 13 THEN o.price
            ELSE 0
        END AS cash_delta
    FROM ops o
),
actual_cash AS (
    SELECT ce.currency_id, SUM(ce.cash_delta) AS actual_quantity
    FROM cash_effects ce
    JOIN params p ON ce.cash_date <= p.as_of_date
    GROUP BY ce.currency_id
)
SELECT
    'cash_delta_by_currency' AS section,
    e.currency_id,
    e.broker_quantity,
    a.actual_quantity,
    a.actual_quantity - e.broker_quantity AS quantity_delta,
    e.broker_value_base
FROM expected_cash e
LEFT JOIN actual_cash a ON a.currency_id = e.currency_id
ORDER BY e.currency_id;

WITH params AS (
    SELECT
        :'portfolio_id'::uuid AS portfolio_id,
        :'as_of_date'::date AS as_of_date
)
SELECT
    'portfolio_snapshot_cash' AS section,
    ps.date,
    ps.cash_base,
    ps.nav_base
FROM portfolio_snapshots ps
JOIN params p ON p.portfolio_id = ps.portfolio_id
WHERE ps.date::date = p.as_of_date;

WITH params AS (
    SELECT
        :'portfolio_id'::uuid AS portfolio_id,
        :'as_of_date'::date AS as_of_date
),
ops AS (
    SELECT
        o.*,
        i.ticker,
        i.name AS instrument_name,
        (o.broker_operation_key LIKE 'v2:%' OR o.broker_operation_key LIKE 'v3:%') AS is_imported,
        COALESCE(o.settlement_date, o.trade_date)::date AS settlement_or_trade_date
    FROM operations o
    LEFT JOIN instruments i ON i.id = o.instrument_id
    JOIN params p ON p.portfolio_id = o.portfolio_id
    WHERE o.trade_date::date <= p.as_of_date
      AND o.currency_id = 'RUB'
),
cash_effects AS (
    SELECT
        o.id,
        o.type,
        o.currency_id,
        o.trade_date,
        o.settlement_date,
        o.ticker,
        o.instrument_name,
        o.note,
        o.broker_operation_key,
        CASE
            WHEN o.is_imported AND o.instrument_id IS NOT NULL THEN o.settlement_or_trade_date
            ELSE o.trade_date::date
        END AS cash_date,
        CASE
            WHEN o.type = 1 AND o.instrument_id IS NOT NULL AND o.is_imported THEN 0
            WHEN o.type = 1 AND o.instrument_id IS NOT NULL THEN -(o.quantity * o.price + o.fee)
            WHEN o.type = 2 AND o.instrument_id IS NOT NULL AND o.is_imported THEN 0
            WHEN o.type = 2 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 3 THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 4 THEN CASE WHEN o.price <> 0 THEN -o.price ELSE -o.fee END
            WHEN o.type = 5 THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 6 THEN -(CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END)
            WHEN o.type = 7 AND o.instrument_id IS NULL THEN CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END
            WHEN o.type = 8 AND o.instrument_id IS NULL THEN -(CASE WHEN o.price <> 0 THEN o.price ELSE o.quantity END)
            WHEN o.type = 9 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 10 AND o.instrument_id IS NOT NULL THEN o.quantity * o.price - o.fee
            WHEN o.type = 13 THEN o.price
            ELSE 0
        END AS cash_delta
    FROM ops o
),
effective AS (
    SELECT ce.*
    FROM cash_effects ce
    JOIN params p ON ce.cash_date <= p.as_of_date
    WHERE ce.cash_delta <> 0
),
running AS (
    SELECT
        e.*,
        SUM(e.cash_delta) OVER (ORDER BY e.cash_date, e.trade_date, e.id) AS rub_balance
    FROM effective e
)
SELECT
    'rub_cash_operations_from_2026_06_01' AS section,
    cash_date,
    trade_date,
    settlement_date,
    type,
    ROUND(cash_delta, 2) AS cash_delta,
    ROUND(rub_balance, 2) AS rub_balance,
    COALESCE(ticker, '') AS ticker,
    LEFT(COALESCE(instrument_name, ''), 80) AS instrument_name,
    LEFT(COALESCE(note, ''), 160) AS note,
    LEFT(COALESCE(broker_operation_key, ''), 40) AS broker_operation_key
FROM running
WHERE cash_date >= DATE '2026-06-01'
ORDER BY cash_date, trade_date, id;

WITH params AS (
    SELECT
        :'portfolio_id'::uuid AS portfolio_id,
        :'as_of_date'::date AS as_of_date
)
SELECT
    'operations_on_report_date' AS section,
    o.trade_date,
    o.settlement_date,
    o.type,
    o.quantity,
    o.price,
    o.fee,
    o.currency_id,
    COALESCE(i.ticker, '') AS ticker,
    LEFT(COALESCE(i.name, ''), 80) AS instrument_name,
    LEFT(COALESCE(o.note, ''), 180) AS note,
    LEFT(COALESCE(o.broker_operation_key, ''), 40) AS broker_operation_key
FROM operations o
LEFT JOIN instruments i ON i.id = o.instrument_id
JOIN params p ON p.portfolio_id = o.portfolio_id
WHERE o.trade_date::date = p.as_of_date
ORDER BY o.trade_date, o.created_at, o.id;

WITH params AS (
    SELECT
        :'portfolio_id'::uuid AS portfolio_id,
        :'as_of_date'::date AS as_of_date
)
SELECT
    'open_security_settlements_after_as_of' AS section,
    o.trade_date,
    o.settlement_date,
    o.type,
    o.quantity,
    o.price,
    o.fee,
    o.currency_id,
    CASE
        WHEN o.type = 1 THEN -(o.quantity * o.price + o.fee)
        WHEN o.type = 2 THEN o.quantity * o.price - o.fee
        ELSE 0
    END AS planned_cash_delta,
    COALESCE(i.ticker, '') AS ticker,
    LEFT(COALESCE(i.name, ''), 80) AS instrument_name,
    LEFT(COALESCE(o.broker_operation_key, ''), 40) AS broker_operation_key
FROM operations o
LEFT JOIN instruments i ON i.id = o.instrument_id
JOIN params p ON p.portfolio_id = o.portfolio_id
WHERE o.instrument_id IS NOT NULL
  AND (o.broker_operation_key LIKE 'v2:%' OR o.broker_operation_key LIKE 'v3:%')
  AND o.trade_date::date <= p.as_of_date
  AND COALESCE(o.settlement_date, o.trade_date)::date > p.as_of_date
  AND o.type IN (1, 2)
ORDER BY o.settlement_date, o.trade_date, o.id;
