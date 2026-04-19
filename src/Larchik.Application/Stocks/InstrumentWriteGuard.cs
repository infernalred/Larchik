using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks;

public static class InstrumentWriteGuard
{
    public static async Task<string?> ValidateAsync(
        LarchikContext context,
        NormalizedInstrumentInput input,
        Guid? excludeInstrumentId,
        CancellationToken cancellationToken)
    {
        if (!await context.Currencies
                .AsNoTracking()
                .AnyAsync(x => x.Id == input.CurrencyId, cancellationToken))
        {
            return "Selected currency was not found.";
        }

        if (!await context.Categories
                .AsNoTracking()
                .AnyAsync(x => x.Id == input.CategoryId, cancellationToken))
        {
            return "Selected category was not found.";
        }

        if (!string.IsNullOrWhiteSpace(input.Isin))
        {
            var isinExists = await context.Instruments
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id != excludeInstrumentId &&
                         x.Isin != null &&
                         x.Isin == input.Isin,
                    cancellationToken);

            if (isinExists)
            {
                return "An instrument with the same ISIN already exists.";
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Figi))
        {
            var figiExists = await context.Instruments
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id != excludeInstrumentId &&
                         x.Figi != null &&
                         x.Figi == input.Figi,
                    cancellationToken);

            if (figiExists)
            {
                return "An instrument with the same FIGI already exists.";
            }
        }

        return null;
    }
}
