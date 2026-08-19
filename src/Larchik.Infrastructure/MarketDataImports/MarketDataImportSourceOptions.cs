namespace Larchik.Infrastructure.MarketDataImports;

public sealed class MarketDataImportSourceOptions
{
    public const string SectionName = "MarketDataImportSources";

    public MoexMarketDataImportSourceOptions Moex { get; set; } = new();
    public TbankMarketDataImportSourceOptions Tbank { get; set; } = new();
}

public sealed class MoexMarketDataImportSourceOptions
{
    public string BaseUrl { get; set; } = "https://iss.moex.com/iss";
}

public sealed class TbankMarketDataImportSourceOptions
{
    public string FindInstrumentBaseUrl { get; set; } =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.InstrumentsService/FindInstrument";
    public string CandlesBaseUrl { get; set; } =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.MarketDataService/GetCandles";
    public string AccruedInterestsBaseUrl { get; set; } =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.InstrumentsService/GetAccruedInterests";
    public string? Token { get; set; }
}
