with hrl as (
    select id
    from instruments
    where isin = 'US4404521001'
    order by created_at
    limit 1
)
insert into instrument_aliases (id, instrument_id, alias_code, normalized_alias_code)
select gen_random_uuid(), hrl.id, 'HRL-RM', 'HRL-RM'
from hrl
where not exists (
    select 1
    from instrument_aliases ia
    where ia.normalized_alias_code = 'HRL-RM'
);

with hrl as (
    select id
    from instruments
    where isin = 'US4404521001'
    order by created_at
    limit 1
)
delete from prices
where instrument_id = (select id from hrl)
  and provider = 'MOEX'
  and date::date in ('2026-03-16', '2026-03-17', '2026-03-20');

with hrl as (
    select id
    from instruments
    where isin = 'US4404521001'
    order by created_at
    limit 1
)
insert into prices (
    id,
    instrument_id,
    date,
    value,
    currency_id,
    source_currency_id,
    provider,
    created_at,
    updated_at
)
select gen_random_uuid(), hrl.id, v.date, v.value, 'RUB', 'RUB', 'MOEX', now(), now()
from hrl
cross join (
    values
        ('2026-03-16'::date, 370.0::numeric),
        ('2026-03-17'::date, 370.0::numeric),
        ('2026-03-20'::date, 372.0::numeric)
) as v(date, value);
