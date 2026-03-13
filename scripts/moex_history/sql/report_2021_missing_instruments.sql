insert into instruments (
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
    updated_by
)
select
    'd3cf86b6-5148-49bf-9530-6b6a91d4c1a2'::uuid,
    'VEON ADR Lev2',
    'VEONold',
    'US91822M1062',
    null,
    src.type,
    src.currency_id,
    src.category_id,
    src.exchange,
    src.country,
    null,
    now(),
    src.created_by,
    now(),
    src.updated_by
from instruments src
where src.ticker = 'VEON'
  and not exists (
      select 1
      from instruments i
      where i.isin = 'US91822M1062'
         or i.ticker = 'VEONold'
  );
