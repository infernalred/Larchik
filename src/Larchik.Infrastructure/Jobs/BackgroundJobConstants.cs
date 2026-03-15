namespace Larchik.Infrastructure.Jobs;

public static class BackgroundJobConstants
{
    public const string FxCbrDailyDefinitionName = "fx_cbr_daily";
    public const string FxCbrDailyJobType = "fx.cbr.daily";
    public const string MoexPricesDailyDefinitionName = "moex_prices_daily";
    public const string MoexPricesDailyJobType = "prices.moex.daily";
    public const string TbankPricesDailyDefinitionName = "tbank_prices_daily";
    public const string TbankPricesDailyJobType = "prices.tbank.daily";
    public const string TbankInstrumentInfoDailyDefinitionName = "tbank_instrument_info_daily";
    public const string TbankInstrumentInfoDailyJobType = "instrument_info.tbank.daily";
}
