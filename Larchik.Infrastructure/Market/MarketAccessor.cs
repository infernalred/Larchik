using System.Text;
using System.Text.Json;
using Larchik.Application.Contracts;
using Larchik.Application.Models.Market;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Market;

public class MarketAccessor : IMarketAccessor
{
    private readonly ILogger<MarketAccessor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public MarketAccessor(ILogger<MarketAccessor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<IEnumerable<StockPrice>> GetLastPrice(IEnumerable<string> figis, CancellationToken cancellationToken)
    {
        var request = new LastPriceRequest
        {
            Figi = figis
        };

        var serializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(request, serializeOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("Market");
        var httpResponseMessage = await httpClient.PostAsync("", content, cancellationToken);

        httpResponseMessage.EnsureSuccessStatusCode();

        await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken);
        var response = JsonSerializer.Deserialize<LastPriceResponse>(contentStream, serializeOptions);

        return ConvertToStockPrices(response);
    }

    private static IEnumerable<StockPrice> ConvertToStockPrices(LastPriceResponse? response)
    {
        var result = response?.LastPrices?.Select(x => new StockPrice
        {
            Figi = x.Figi,
            LastPrice = Convert.ToDouble(x.Price.Units) + x.Price.Nano *  0.000000001
        });

        return result ?? new List<StockPrice>();
    }
}