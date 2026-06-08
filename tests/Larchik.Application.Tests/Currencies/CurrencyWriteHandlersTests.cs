using Larchik.Application.Currencies.CreateCurrency;
using Larchik.Application.Currencies.UpdateCurrency;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Currencies;

public sealed class CurrencyWriteHandlersTests
{
    [Fact]
    public async Task CreateCurrency_AddsNormalizedCurrency()
    {
        await using var harness = new CurrencyWriteHarness();

        var handler = new CreateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new CreateCurrencyCommand(new CurrencyModel(" gbp ", " British Pound ")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var currency = await harness.Context.Currencies
            .AsNoTracking()
            .SingleAsync(x => x.Id == "GBP");
        Assert.Equal("British Pound", currency.Name);
    }

    [Fact]
    public async Task CreateCurrency_RejectsDuplicateCode()
    {
        await using var harness = new CurrencyWriteHarness();
        harness.Context.Currencies.Add(CurrencyTestData.Create("CHF", "Swiss Franc"));
        await harness.Context.SaveChangesAsync();

        var handler = new CreateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new CreateCurrencyCommand(new CurrencyModel("CHF", "Another Franc")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("существует", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCurrency_RejectsInvalidCode()
    {
        await using var harness = new CurrencyWriteHarness();

        var handler = new CreateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new CreateCurrencyCommand(new CurrencyModel("US", "Dollar")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateCurrency_RejectsNonLetterCode()
    {
        await using var harness = new CurrencyWriteHarness();

        var handler = new CreateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new CreateCurrencyCommand(new CurrencyModel("12$", "Invalid")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("букв", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCurrency_RejectsNullName()
    {
        await using var harness = new CurrencyWriteHarness();

        var handler = new CreateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new CreateCurrencyCommand(new CurrencyModel("USD", null!)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("название", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateCurrency_UpdatesNameOnly()
    {
        await using var harness = new CurrencyWriteHarness();
        harness.Context.Currencies.Add(CurrencyTestData.Create("JPY", "Yen"));
        await harness.Context.SaveChangesAsync();
        harness.Context.ChangeTracker.Clear();

        var handler = new UpdateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new UpdateCurrencyCommand("jpy", new UpdateCurrencyModel(" Japanese Yen ")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var currency = await harness.Context.Currencies
            .AsNoTracking()
            .SingleAsync(x => x.Id == "JPY");
        Assert.Equal("Japanese Yen", currency.Name);
    }

    [Fact]
    public async Task UpdateCurrency_ReturnsNotFoundForMissingCurrency()
    {
        await using var harness = new CurrencyWriteHarness();

        var handler = new UpdateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new UpdateCurrencyCommand("XXX", new UpdateCurrencyModel("Missing")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("не найдена", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateCurrency_RejectsNullName()
    {
        await using var harness = new CurrencyWriteHarness();
        harness.Context.Currencies.Add(CurrencyTestData.Create("ABC", "Test Currency"));
        await harness.Context.SaveChangesAsync();
        harness.Context.ChangeTracker.Clear();

        var handler = new UpdateCurrencyCommandHandler(harness.Context);
        var result = await handler.Handle(
            new UpdateCurrencyCommand("ABC", new UpdateCurrencyModel(null!)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("название", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CurrencyWriteHarness : IAsyncDisposable
    {
        private readonly SqliteTestDatabase database;

        public CurrencyWriteHarness()
        {
            database = SqliteTestContextFactory.Create();
            Context = database.Context;
        }

        public LarchikContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }
}
