UPDATE instruments
SET isin = 'RU000A0JP5V6'
WHERE upper(ticker) = 'VTBR'
  AND upper(coalesce(figi, '')) = 'BBG004730ZJ9';
