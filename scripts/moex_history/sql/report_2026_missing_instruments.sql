BEGIN;

UPDATE instruments
SET isin = 'RU0006944147',
    figi = COALESCE(figi, 'BBG004S68829'),
    updated_at = now(),
    updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
WHERE upper(ticker) = 'TATNP'
  AND upper(isin) = 'BBG004S68829';

UPDATE instruments
SET isin = 'RU0009024277',
    figi = COALESCE(figi, 'BBG004731032'),
    updated_at = now(),
    updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
WHERE upper(ticker) = 'LKOH'
  AND upper(isin) = 'BBG004731032';

UPDATE instruments
SET isin = 'RU0009100945',
    figi = COALESCE(figi, 'BBG000QJW156'),
    updated_at = now(),
    updated_by = '7e89d7d2-21e2-40ce-bef2-58c3b9408abb'::uuid
WHERE upper(ticker) = 'BSPB'
  AND upper(isin) = 'BBG000QJW156';

COMMIT;
