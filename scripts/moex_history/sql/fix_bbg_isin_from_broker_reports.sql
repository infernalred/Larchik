-- Generated from the user's T-Bank broker reports (2019-2026).
-- Fixes instruments where the legacy import stored FIGI in instruments.isin.

BEGIN;

WITH src (ticker, isin) AS (
    VALUES
        ('AAL', 'US02376R1023'),
        ('AAPL', 'US0378331005'),
        ('ABT', 'US0028241000'),
        ('BELU', 'RU000A0HL5M1'),
        ('BSPB', 'RU0009100945'),
        ('BTI', 'US1104481072'),
        ('CHMF', 'RU0009046510'),
        ('DOMRF', 'RU000A0ZZFU5'),
        ('DSKY', 'RU000A0JSQ90'),
        ('EUTR', 'RU000A1002V2'),
        ('GAZP', 'RU0007661625'),
        ('HEAD', 'RU000A107662'),
        ('HRL', 'US4404521001'),
        ('IRAO', 'RU000A0JPNM1'),
        ('LKOH', 'RU0009024277'),
        ('LQDT', 'RU000A1014L8'),
        ('LSNGP', 'RU0009092134'),
        ('LSRG', 'RU000A0JPFP0'),
        ('MAGN', 'RU0009084396'),
        ('MBNK', 'RU000A0JRH43'),
        ('MGNT', 'RU000A0JKQU8'),
        ('MOEX', 'RU000A0JR4A1'),
        ('MRKC', 'RU000A0JPPL8'),
        ('MRKP', 'RU000A0JPN96'),
        ('MSFT', 'US5949181045'),
        ('MSNG', 'RU0008958863'),
        ('MTSS', 'RU0007775219'),
        ('NBIS', 'NL0009805522'),
        ('NLMK', 'RU0009046452'),
        ('NMTP', 'RU0009084446'),
        ('NVTK', 'RU000A0DKVS5'),
        ('OZON', 'RU000A10CW95'),
        ('OZPH', 'RU000A109B25'),
        ('PHOR', 'RU000A0JRKT8'),
        ('ROSN', 'RU000A0J2Q06'),
        ('SBER', 'RU0009029540'),
        ('SBERP', 'RU0009029557'),
        ('SELG', 'RU000A0JPR50'),
        ('SNGSP', 'RU0009029524'),
        ('SVCB', 'RU000A0ZZAC4'),
        ('T', 'RU000A107UL4'),
        ('TATN', 'RU0009033591'),
        ('TATNP', 'RU0006944147'),
        ('TDIV', 'RU000A107563'),
        ('TGKN', 'RU000A0H1ES3'),
        ('TGLD', 'RU000A101X50'),
        ('TMOS', 'RU000A101X76'),
        ('TPAY', 'RU000A108WX3'),
        ('TRMK', 'RU000A0B6NK6'),
        ('TRNFP', 'RU0009091573'),
        ('TRUR', 'RU000A1011U5'),
        ('TUSD', 'RU000A1011S9'),
        ('UGLD', 'RU000A0JPP37'),
        ('VALE', 'US91912E1055'),
        ('VEON', 'US91822M5022'),
        ('VEONOLD', 'US91822M1062'),
        ('VTBR', 'RU000A0JP5V6'),
        ('X5', 'RU000A108X38'),
        ('YDEX', 'RU000A107T19'),
        ('ZAYM', 'RU000A107RM8')
)
UPDATE instruments dst
SET isin = src.isin
FROM src
WHERE upper(dst.ticker) = src.ticker
  AND dst.isin ~ '^BBG';

COMMIT;
