BEGIN;

UPDATE instruments
SET
    name = 'ЮГК',
    type = 1,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQBR',
    updated_at = now()
WHERE ticker = 'RU000A0JPP37';

UPDATE instruments
SET
    name = 'ПАО ДОМ.РФ',
    type = 1,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQBR',
    updated_at = now()
WHERE ticker = 'RU000A0ZZFU5';

UPDATE instruments
SET
    name = 'Т-Капитал Золото',
    type = 3,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQTF',
    updated_at = now()
WHERE ticker = 'RU000A101X50';

UPDATE instruments
SET
    name = 'ОФЗ 29015',
    type = 2,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQOB',
    updated_at = now()
WHERE ticker = 'RU000A1025A7';

UPDATE instruments
SET
    name = 'ОФЗ 26246',
    type = 2,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQOB',
    updated_at = now()
WHERE ticker = 'RU000A108EE1';

UPDATE instruments
SET
    name = 'ОФЗ 26250',
    type = 2,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQOB',
    updated_at = now()
WHERE ticker = 'RU000A10BVH7';

UPDATE instruments
SET
    name = 'ОФЗ 26253',
    type = 2,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQOB',
    updated_at = now()
WHERE ticker = 'RU000A10D517';

UPDATE instruments
SET
    name = 'ОФЗ 26254',
    type = 2,
    currency_id = 'RUB',
    country = 'RU',
    exchange = 'TQOB',
    updated_at = now()
WHERE ticker = 'RU000A10D533';

WITH aliases(alias_code, normalized_alias_code, ticker, alias_id) AS (
    VALUES
        ('UGLD', 'UGLD', 'RU000A0JPP37', '8a1fbf0e-4244-4bbf-8121-0d0fdcb69e31'::uuid),
        ('DOMRF', 'DOMRF', 'RU000A0ZZFU5', '0c1d44c5-83c0-4f3b-9108-d442e2d9345f'::uuid),
        ('TGLD', 'TGLD', 'RU000A101X50', 'cc7ccefb-b028-4cbc-bf0c-46f6bd67aaf2'::uuid),
        ('SU29015RMFS3', 'SU29015RMFS3', 'RU000A1025A7', '55ce87b7-4587-4687-8588-e22db494f757'::uuid),
        ('SU26246RMFS7', 'SU26246RMFS7', 'RU000A108EE1', '7a2e10a4-a8a8-4d34-bb83-bb62db63b4cb'::uuid),
        ('SU26250RMFS9', 'SU26250RMFS9', 'RU000A10BVH7', 'edbd6e19-a5f9-4ba6-bfa8-84b786ea821f'::uuid),
        ('SU26253RMFS3', 'SU26253RMFS3', 'RU000A10D517', 'ba5ff3dd-6bf7-43d3-b62f-13c6cb2c69d2'::uuid),
        ('SU26254RMFS1', 'SU26254RMFS1', 'RU000A10D533', 'b179ea0e-4058-47f8-a527-5c8f1d4e7966'::uuid)
)
INSERT INTO instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
SELECT
    a.alias_id,
    i.id,
    a.alias_code,
    a.normalized_alias_code
FROM aliases a
JOIN instruments i ON i.ticker = a.ticker
WHERE NOT EXISTS (
    SELECT 1
    FROM instrument_aliases ia
    WHERE ia.normalized_alias_code = a.normalized_alias_code
);

COMMIT;
