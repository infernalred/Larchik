using Larchik.Application.Models;
using Larchik.Application.Stocks.InstrumentCorporateActions;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Stocks.InstrumentCorporateActions;

public sealed class InstrumentCorporateActionWriteHelperTests
{
    [Theory]
    [InlineData(OperationType.Split, 2)]
    [InlineData(OperationType.ReverseSplit, 0.5)]
    public void Validate_AcceptsConfiguredCorporateActionContract(OperationType type, decimal factor)
    {
        var model = new InstrumentCorporateActionModel(
            type,
            factor,
            new DateTimeOffset(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            "valid action");

        var error = InstrumentCorporateActionWriteHelper.Validate(model, InstrumentType.Equity);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(OperationType.Split, 0.5)]
    [InlineData(OperationType.Split, 1)]
    [InlineData(OperationType.ReverseSplit, 1)]
    [InlineData(OperationType.ReverseSplit, 2)]
    public void Validate_RejectsInvalidFactorsByType(OperationType type, decimal factor)
    {
        var model = new InstrumentCorporateActionModel(
            type,
            factor,
            new DateTimeOffset(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            "invalid factor");

        var error = InstrumentCorporateActionWriteHelper.Validate(model, InstrumentType.Equity);

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(InstrumentType.Currency)]
    [InlineData(InstrumentType.Bond)]
    [InlineData(InstrumentType.Commodity)]
    [InlineData(InstrumentType.Crypto)]
    public void Validate_RejectsUnsupportedInstrumentTypes(InstrumentType instrumentType)
    {
        var model = new InstrumentCorporateActionModel(
            OperationType.Split,
            2m,
            new DateTimeOffset(new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            "unsupported instrument type");

        var error = InstrumentCorporateActionWriteHelper.Validate(model, instrumentType);

        Assert.Equal("Corporate actions are supported only for Equity and Etf instruments.", error);
    }

    [Fact]
    public async Task IsDuplicateConflict_ReturnsTrue_ForSqliteUniqueViolation()
    {
        await using var db = SqliteTestContextFactory.Create();
        var context = db.Context;
        var instrumentId = Guid.NewGuid();

        if (!await context.Currencies.AnyAsync(x => x.Id == "USD"))
        {
            context.Currencies.Add(CurrencyTestData.Usd);
        }

        var categoryId = await context.Categories
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync() ?? 2001;
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
            CurrencyId = "USD",
            CategoryId = categoryId,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });

        await context.SaveChangesAsync();

        var effectiveDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        context.InstrumentCorporateActions.AddRange(
            new InstrumentCorporateAction
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 2m,
                EffectiveDate = effectiveDate,
                Note = "first"
            },
            new InstrumentCorporateAction
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 3m,
                EffectiveDate = effectiveDate,
                Note = "duplicate"
            });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.True(InstrumentCorporateActionWriteHelper.IsDuplicateConflict(exception));
    }
}
