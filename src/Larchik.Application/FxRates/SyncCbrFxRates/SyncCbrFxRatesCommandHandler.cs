using System.Globalization;
using System.Xml.Linq;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.FxRates.SyncCbrFxRates;

public class SyncCbrFxRatesCommandHandler(
    LarchikContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<SyncCbrFxRatesCommandHandler> logger)
    : IRequestHandler<SyncCbrFxRatesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncCbrFxRatesCommand request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var asOfUtc = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var nextDayUtc = asOfUtc.AddDays(1);
        var formatted = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var url = $"https://www.cbr.ru/scripts/XML_daily.asp?date_req={formatted}";

        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<int>.Failure($"CBR request failed with status {response.StatusCode}");
        }

        var xml = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
        var doc = XDocument.Parse(xml);
        var rates = new List<FxRate>();
        var supportedCurrencies = await context.Currencies
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);
        var skippedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var valute in doc.Descendants("Valute"))
        {
            var code = valute.Element("CharCode")?.Value;
            var valueStr = valute.Element("Value")?.Value?.Replace(',', '.');
            var nominalStr = valute.Element("Nominal")?.Value;

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(valueStr) || string.IsNullOrWhiteSpace(nominalStr))
                continue;

            if (!decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) continue;
            if (!decimal.TryParse(nominalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var nominal)) continue;

            var normalizedCode = code.ToUpperInvariant();
            if (!supportedCurrencies.Contains(normalizedCode))
            {
                skippedCurrencies.Add(normalizedCode);
                continue;
            }

            var ratePerUnit = value / nominal;

            rates.Add(new FxRate
            {
                Id = Guid.NewGuid(),
                BaseCurrencyId = normalizedCode,
                QuoteCurrencyId = "RUB",
                Date = asOfUtc,
                Rate = ratePerUnit,
                Source = "CBR",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (skippedCurrencies.Count > 0)
        {
            logger.LogWarning(
                "CBR FX sync skipped {Count} unsupported currencies for {Date}: {Currencies}",
                skippedCurrencies.Count,
                date.ToString("yyyy-MM-dd"),
                string.Join(", ", skippedCurrencies.OrderBy(x => x)));
        }

        var existing = await context.FxRates
            .Where(x => x.Date >= asOfUtc && x.Date < nextDayUtc && x.Source == "CBR")
            .ToListAsync(cancellationToken);

        foreach (var existingRate in existing)
        {
            if (existingRate.Date.Kind != DateTimeKind.Utc)
            {
                existingRate.Date = DateTime.SpecifyKind(existingRate.Date, DateTimeKind.Utc);
            }

            if (existingRate.CreatedAt.Kind != DateTimeKind.Utc)
            {
                existingRate.CreatedAt = DateTime.SpecifyKind(existingRate.CreatedAt, DateTimeKind.Utc);
            }
        }

        foreach (var rate in rates)
        {
            var match = existing.FirstOrDefault(x =>
                x.BaseCurrencyId == rate.BaseCurrencyId &&
                x.QuoteCurrencyId == rate.QuoteCurrencyId);

            if (match is null)
            {
                await context.FxRates.AddAsync(rate, cancellationToken);
            }
            else
            {
                match.Rate = rate.Rate;
                match.CreatedAt = DateTime.UtcNow;
            }
        }

        var changed = await context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(changed);
    }
}
