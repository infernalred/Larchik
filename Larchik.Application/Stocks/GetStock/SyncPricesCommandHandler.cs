using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Prices.SyncPrices;

public class SyncPricesCommandHandler(LarchikContext context)
    : IRequestHandler<SyncPricesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncPricesCommand request, CancellationToken cancellationToken)
    {
        var instrumentIds = request.Prices.Select(p => p.InstrumentId).Distinct().ToArray();
        var knownInstruments = await context.Instruments
            .Where(i => instrumentIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        var filtered = request.Prices.Where(p => knownInstruments.Contains(p.InstrumentId)).ToList();

        var instrumentPriceDates = filtered
            .Select(p => (p.InstrumentId, Date: p.Date.Date, Provider: p.Provider.ToUpperInvariant()))
            .ToHashSet();

        var existing = await context.Prices
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .Where(x => instrumentPriceDates.Any(t =>
                t.Item1 == x.InstrumentId &&
                t.Item2 == x.Date.Date &&
                t.Item3 == x.Provider.ToUpper()))
            .ToListAsync(cancellationToken);

        foreach (var priceModel in filtered)
        {
            var provider = priceModel.Provider.ToUpperInvariant();
            var existingPrice = existing.FirstOrDefault(x =>
                x.InstrumentId == priceModel.InstrumentId &&
                x.Date.Date == priceModel.Date.Date &&
                x.Provider.ToUpper() == provider);

            if (existingPrice is null)
            {
                await context.Prices.AddAsync(new Price
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = priceModel.InstrumentId,
                    Date = priceModel.Date,
                    Value = priceModel.Value,
                    CurrencyId = priceModel.CurrencyId.ToUpperInvariant(),
                    Provider = provider,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                existingPrice.Value = priceModel.Value;
                existingPrice.CurrencyId = priceModel.CurrencyId.ToUpperInvariant();
                existingPrice.Provider = provider;
                existingPrice.UpdatedAt = DateTime.UtcNow;
            }
        }

        var changes = await context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(changes);
    }
}
