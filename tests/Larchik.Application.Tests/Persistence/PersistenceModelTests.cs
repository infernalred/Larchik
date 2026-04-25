using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using Larchik.Application.Tests.TestInfrastructure;

namespace Larchik.Application.Tests.Persistence;

public sealed class PersistenceModelTests : IDisposable
{
    private readonly SqliteTestDatabase database;
    private readonly LarchikContext context;

    public PersistenceModelTests()
    {
        database = SqliteTestContextFactory.Create(ensureCreated: false);
        context = database.Context;
    }

    [Fact]
    public void Context_UsesNoTrackingByDefault()
    {
        Assert.Equal(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public void Model_DoesNotMapLegacyLot_AndCashBalanceEntities()
    {
        Assert.Null(context.Model.FindEntityType(typeof(Lot)));
        Assert.Null(context.Model.FindEntityType(typeof(CashBalance)));
    }

    [Fact]
    public void Instrument_Isin_IsNullable_InModel()
    {
        var entityType = context.Model.FindEntityType(typeof(Instrument));
        Assert.NotNull(entityType);
        var isin = entityType!.FindProperty(nameof(Instrument.Isin));
        Assert.NotNull(isin);

        Assert.True(isin!.IsNullable);
    }

    [Fact]
    public void Instrument_ReferenceCodes_HaveBoundedLengths_AndForeignKeys()
    {
        var entityType = context.Model.FindEntityType(typeof(Instrument));
        Assert.NotNull(entityType);
        var currencyId = entityType!.FindProperty(nameof(Instrument.CurrencyId));
        var countryId = entityType.FindProperty(nameof(Instrument.CountryId));
        var exchangeId = entityType.FindProperty(nameof(Instrument.ExchangeId));
        Assert.NotNull(currencyId);
        Assert.NotNull(countryId);
        Assert.NotNull(exchangeId);

        Assert.False(currencyId!.IsNullable);
        Assert.Equal(3, currencyId.GetMaxLength());
        Assert.Equal(2, countryId!.GetMaxLength());
        Assert.Equal(16, exchangeId!.GetMaxLength());
        Assert.Contains(entityType.GetForeignKeys(), x => x.Properties.Single().Name == nameof(Instrument.CurrencyId));
        Assert.Contains(entityType.GetForeignKeys(), x => x.Properties.Single().Name == nameof(Instrument.CountryId));
        Assert.Contains(entityType.GetForeignKeys(), x => x.Properties.Single().Name == nameof(Instrument.ExchangeId));
    }

    [Fact]
    public void Portfolio_ReportingCurrencyId_UsesThreeLetterCode_AndCreatedAt_IsGeneratedOnAdd()
    {
        var entityType = context.Model.FindEntityType(typeof(Portfolio));
        Assert.NotNull(entityType);
        var reportingCurrency = entityType!.FindProperty(nameof(Portfolio.ReportingCurrencyId));
        var createdAt = entityType.FindProperty(nameof(Portfolio.CreatedAt));
        Assert.NotNull(reportingCurrency);
        Assert.NotNull(createdAt);

        Assert.False(reportingCurrency!.IsNullable);
        Assert.Equal(3, reportingCurrency.GetMaxLength());
        Assert.Equal(ValueGenerated.OnAdd, createdAt!.ValueGenerated);
    }

    [Fact]
    public void Price_HasUniqueCompositeIndex_AndExpectedStoreShape()
    {
        var entityType = context.Model.FindEntityType(typeof(Price));
        Assert.NotNull(entityType);
        var uniqueIndex = entityType!.GetIndexes().Single(
            x => x.IsUnique &&
                 x.Properties.Select(p => p.Name).SequenceEqual(
                 [
                     nameof(Price.InstrumentId),
                     nameof(Price.Date),
                     nameof(Price.Provider)
                 ]));
        var value = entityType.FindProperty(nameof(Price.Value));
        var sourceCurrency = entityType.FindProperty(nameof(Price.SourceCurrencyId));
        Assert.NotNull(value);
        Assert.NotNull(sourceCurrency);

        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal("TEXT", value!.GetColumnType());
        Assert.Equal(3, sourceCurrency!.GetMaxLength());
    }

    [Fact]
    public void JobRun_Payload_UsesJsonb_AndDedupKeyIsUnique()
    {
        var entityType = context.Model.FindEntityType(typeof(JobRun));
        Assert.NotNull(entityType);
        var payload = entityType!.FindProperty(nameof(JobRun.PayloadJson));
        Assert.NotNull(payload);

        Assert.Equal("jsonb", payload!.GetColumnType());
        Assert.Contains(
            entityType.GetIndexes(),
            x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(JobRun.DedupKey)]));
    }

    [Fact]
    public void FxRate_CurrencyCodes_AreRequiredAndThreeLetters()
    {
        var entityType = context.Model.FindEntityType(typeof(FxRate));
        Assert.NotNull(entityType);
        var baseCurrency = entityType!.FindProperty(nameof(FxRate.BaseCurrencyId));
        var quoteCurrency = entityType.FindProperty(nameof(FxRate.QuoteCurrencyId));
        Assert.NotNull(baseCurrency);
        Assert.NotNull(quoteCurrency);

        Assert.False(baseCurrency!.IsNullable);
        Assert.False(quoteCurrency!.IsNullable);
        Assert.Equal(3, baseCurrency.GetMaxLength());
        Assert.Equal(3, quoteCurrency.GetMaxLength());
    }

    [Fact]
    public void Operation_BrokerOperationKey_UniqueIndex_IsFiltered()
    {
        var entityType = context.Model.FindEntityType(typeof(Operation));
        Assert.NotNull(entityType);
        var index = entityType!.GetIndexes().Single(
            x => x.IsUnique &&
                 x.Properties.Select(p => p.Name).SequenceEqual(
                 [
                     nameof(Operation.PortfolioId),
                     nameof(Operation.BrokerOperationKey)
                 ]));

        Assert.Equal("\"broker_operation_key\" IS NOT NULL", index.GetFilter());
    }

    public void Dispose()
    {
        database.Dispose();
    }
}
