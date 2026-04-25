using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Helpers;

public sealed class InstrumentListingHistoryResolverTests
{
    [Fact]
    public void Resolve_ReturnsActiveListing_ForAsOfDate()
    {
        var instrumentId = Guid.NewGuid();
        var instrument = new Instrument
        {
            Id = instrumentId,
            Ticker = "AAPL",
            Figi = "FIGI_CURRENT",
            ExchangeId = "NASDAQ",
            CurrencyId = "USD",
            Name = "Apple",
            Type = InstrumentType.Equity
        };
        var asOfDate = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories =
            new Dictionary<Guid, IReadOnlyList<InstrumentListingHistory>>
            {
                [instrumentId] =
                [
                    new()
                    {
                        InstrumentId = instrumentId,
                        Ticker = "AAPL-RM",
                        Figi = "FIGI_OLD",
                        ExchangeId = "MOEX",
                        CurrencyId = "RUB",
                        EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        EffectiveTo = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new()
                    {
                        InstrumentId = instrumentId,
                        Ticker = "AAPL",
                        Figi = "FIGI_NEW",
                        ExchangeId = "SPBX",
                        CurrencyId = "USD",
                        EffectiveFrom = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            };

        var snapshot = InstrumentListingHistoryResolver.Resolve(instrument, histories, asOfDate);

        Assert.Equal("AAPL", snapshot.Ticker);
        Assert.Equal("FIGI_NEW", snapshot.Figi);
        Assert.Equal("SPBX", snapshot.Exchange);
        Assert.Equal("USD", snapshot.CurrencyId);
    }

    [Fact]
    public void Resolve_FallsBackToInstrumentValues_WhenNoActiveListingExists()
    {
        var instrumentId = Guid.NewGuid();
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> histories =
            new Dictionary<Guid, IReadOnlyList<InstrumentListingHistory>>
            {
                [instrumentId] =
                [
                    new()
                    {
                        InstrumentId = instrumentId,
                        Ticker = "OLD",
                        Figi = "OLDFIGI",
                        ExchangeId = "MOEX",
                        CurrencyId = "RUB",
                        EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        EffectiveTo = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
                    }
                ]
            };

        var snapshot = InstrumentListingHistoryResolver.Resolve(
            instrumentId,
            "AAPL",
            "FIGI_CURRENT",
            "NASDAQ",
            "USD",
            histories,
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("AAPL", snapshot.Ticker);
        Assert.Equal("FIGI_CURRENT", snapshot.Figi);
        Assert.Equal("NASDAQ", snapshot.Exchange);
        Assert.Equal("USD", snapshot.CurrencyId);
    }
}
