using System.Net;
using System.Text;
using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Tests.Prices;
using Larchik.Application.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Larchik.Application.Tests.FxRates;

public sealed class SyncCbrFxRatesCommandHandlerTests
{
    [Fact]
    public async Task Handle_InsertsSupportedRates_AndSkipsUnsupportedCurrencies()
    {
        await using var harness = new PriceSyncTestHarness();
        var factory = new FakeHttpClientFactory((request, _) =>
        {
            Assert.Contains("date_req=20/04/2026", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                <ValCurs Date="20.04.2026">
                  <Valute>
                    <CharCode>USD</CharCode>
                    <Nominal>1</Nominal>
                    <Value>80,5000</Value>
                  </Valute>
                  <Valute>
                    <CharCode>XXX</CharCode>
                    <Nominal>1</Nominal>
                    <Value>99,0000</Value>
                  </Valute>
                </ValCurs>
                """, Encoding.UTF8, "application/xml")
            });
        });

        var handler = new SyncCbrFxRatesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncCbrFxRatesCommandHandler>.Instance);

        var result = await handler.Handle(new SyncCbrFxRatesCommand(new DateOnly(2026, 4, 20)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var rates = await harness.Context.FxRates.ToListAsync();
        Assert.Single(rates);
        Assert.Equal("USD", rates[0].BaseCurrencyId);
        Assert.Equal("RUB", rates[0].QuoteCurrencyId);
        Assert.Equal(80.5m, rates[0].Rate);
        Assert.Equal("CBR", rates[0].Source);
        Assert.Equal(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), rates[0].Date);
    }

    [Fact]
    public async Task Handle_UpdatesExistingRate_ForSameDateAndPair()
    {
        await using var harness = new PriceSyncTestHarness();
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        harness.AddFxRate("USD", "RUB", date, 79m, "CBR");
        await harness.Context.SaveChangesAsync();

        var factory = new FakeHttpClientFactory((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                <ValCurs Date="20.04.2026">
                  <Valute>
                    <CharCode>USD</CharCode>
                    <Nominal>1</Nominal>
                    <Value>81,2500</Value>
                  </Valute>
                </ValCurs>
                """, Encoding.UTF8, "application/xml")
            }));

        var handler = new SyncCbrFxRatesCommandHandler(
            harness.Context,
            factory,
            NullLogger<SyncCbrFxRatesCommandHandler>.Instance);

        var result = await handler.Handle(new SyncCbrFxRatesCommand(new DateOnly(2026, 4, 20)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var rates = await harness.Context.FxRates.ToListAsync();
        Assert.Single(rates);
        Assert.Equal(81.25m, rates[0].Rate);
    }
}
