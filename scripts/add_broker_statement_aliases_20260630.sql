-- Add broker statement aliases observed in the T-Bank portfolio PDF dated 2026-06-30.
-- The OFZ aliases map broker/MOEX short codes to local ISIN-based tickers.
-- TGLD@ is the broker UI code for the local TGLD fund.

BEGIN;

WITH aliases(alias_code, normalized_alias_code, ticker, alias_id) AS (
    VALUES
        ('SU26246RMFS7', 'SU26246RMFS7', 'RU000A108EE1', '7a2e10a4-a8a8-4d34-bb83-bb62db63b4cb'::uuid),
        ('SU26250RMFS9', 'SU26250RMFS9', 'RU000A10BVH7', 'edbd6e19-a5f9-4ba6-bfa8-84b786ea821f'::uuid),
        ('SU26253RMFS3', 'SU26253RMFS3', 'RU000A10D517', 'ba5ff3dd-6bf7-43d3-b62f-13c6cb2c69d2'::uuid),
        ('SU26254RMFS1', 'SU26254RMFS1', 'RU000A10D533', 'b179ea0e-4058-47f8-a527-5c8f1d4e7966'::uuid),
        ('SU29015RMFS3', 'SU29015RMFS3', 'RU000A1025A7', '55ce87b7-4587-4687-8588-e22db494f757'::uuid),
        ('TGLD@', 'TGLD@', 'TGLD', '1e89441c-6b5b-4a47-bbe8-8fd6bdb4d833'::uuid)
), resolved AS (
    SELECT
        a.alias_id,
        a.alias_code,
        a.normalized_alias_code,
        i.id AS instrument_id
    FROM aliases a
    JOIN instruments i ON upper(i.ticker) = upper(a.ticker)
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    r.alias_id,
    r.instrument_id,
    r.alias_code,
    r.normalized_alias_code
FROM resolved r
ON CONFLICT (normalized_alias_code) DO UPDATE
SET
    instrument_id = excluded.instrument_id,
    alias_code = excluded.alias_code;

DO $$
DECLARE
    missing_count integer;
BEGIN
    WITH expected(normalized_alias_code, ticker) AS (
        VALUES
            ('SU26246RMFS7', 'RU000A108EE1'),
            ('SU26250RMFS9', 'RU000A10BVH7'),
            ('SU26253RMFS3', 'RU000A10D517'),
            ('SU26254RMFS1', 'RU000A10D533'),
            ('SU29015RMFS3', 'RU000A1025A7'),
            ('TGLD@', 'TGLD')
    )
    SELECT count(*)
    INTO missing_count
    FROM expected e
    WHERE NOT EXISTS (
        SELECT 1
        FROM instrument_aliases ia
        JOIN instruments i ON i.id = ia.instrument_id
        WHERE ia.normalized_alias_code = e.normalized_alias_code
          AND upper(i.ticker) = upper(e.ticker)
    );

    IF missing_count <> 0 THEN
        RAISE EXCEPTION 'Broker statement alias validation failed: % aliases missing or mapped to wrong instrument.', missing_count;
    END IF;
END $$;

COMMIT;
