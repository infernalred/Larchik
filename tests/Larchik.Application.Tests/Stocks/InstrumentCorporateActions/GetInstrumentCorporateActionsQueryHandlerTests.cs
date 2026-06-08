using Larchik.Application.Stocks.InstrumentCorporateActions.GetInstrumentCorporateActions;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Application.Models;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Stocks.InstrumentCorporateActions;

public sealed class GetInstrumentCorporateActionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlySupportedCorporateActions()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;

        var instrumentId = Guid.NewGuid();

        var currencyId = await context.Currencies
            .Select(x => x.Id)
            .FirstOrDefaultAsync() ?? "USD";
        if (!await context.Currencies.AnyAsync(x => x.Id == currencyId))
        {
            context.Currencies.Add(CurrencyTestData.Create(currencyId));
        }

        var categoryId = await context.Categories
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync() ?? 1001;
        if (!await context.Categories.AnyAsync(x => x.Id == categoryId))
        {
            context.Categories.Add(new Category { Id = categoryId, Name = "Stocks" });
        }

        context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = "Apple",
            Ticker = "AAPL",
            Type = InstrumentType.Equity,
            CurrencyId = currencyId,
            CategoryId = categoryId,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });

        context.InstrumentCorporateActions.AddRange(
            new InstrumentCorporateAction
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 2m,
                EffectiveDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                Note = "2-for-1 split"
            },
            new InstrumentCorporateAction
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                Factor = 1m,
                EffectiveDate = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
                Note = "noise action"
            });

        await context.SaveChangesAsync();

        var handler = new GetInstrumentCorporateActionsQueryHandler(context);

        var result = await handler.Handle(new GetInstrumentCorporateActionsQuery(instrumentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<InstrumentCorporateActionDto>>(result.Value);
        var action = Assert.Single(items);
        Assert.Equal(OperationType.Split, action.Type);
    }
}
