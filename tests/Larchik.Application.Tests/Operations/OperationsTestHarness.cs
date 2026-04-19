using Larchik.Application.Common.Paging;
using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Application.Operations.CreateOperation;
using Larchik.Application.Operations.DeleteOperation;
using Larchik.Application.Operations.EditOperation;
using Larchik.Application.Operations.GetOperation;
using Larchik.Application.Operations.GetOperations;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Larchik.Application.Tests.Operations;

internal sealed class OperationsTestHarness : IAsyncDisposable
{
    internal static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid TbankBrokerId = Guid.Parse("f6f784ea-b520-4bc5-8a32-9a17f1637003");
    internal static readonly Guid OtherBrokerId = Guid.Parse("4ee304a8-6f0a-490f-bfa5-58f6f958b002");
    internal static readonly DateTime SeedTimestamp = new(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase database;
    private readonly FixedUserAccessor userAccessor = new(UserId);

    public OperationsTestHarness()
    {
        database = SqliteTestContextFactory.Create();
        Context = database.Context;
        SeedReferenceData();
    }

    public LarchikContext Context { get; }
    public RecordingPortfolioRecalcService Recalc { get; } = new();

    public CreateOperationCommandHandler CreateHandler => new(Context, userAccessor, Recalc);
    public EditOperationCommandHandler EditHandler => new(Context, userAccessor, Recalc);
    public DeleteOperationCommandHandler DeleteHandler => new(Context, userAccessor, Recalc);
    public GetOperationQueryHandler GetHandler => new(Context, userAccessor);
    public GetOperationsQueryHandler GetManyHandler => new(Context, userAccessor);

    public ImportBrokerReportCommandHandler CreateImportHandler(params IBrokerReportParser[] parsers) =>
        new(Context, userAccessor, Recalc, parsers, NullLogger<ImportBrokerReportCommandHandler>.Instance);

    public Guid AddPortfolio(string name, Guid? userId = null, Guid? brokerId = null, string reportingCurrencyId = "RUB")
    {
        var portfolioId = Guid.NewGuid();
        Context.Portfolios.Add(new Portfolio
        {
            Id = portfolioId,
            UserId = userId ?? UserId,
            BrokerId = brokerId ?? TbankBrokerId,
            Name = name,
            ReportingCurrencyId = reportingCurrencyId,
            CreatedAt = SeedTimestamp
        });

        return portfolioId;
    }

    public Guid AddInstrument(
        string ticker,
        string currencyId = "RUB",
        string? isin = null,
        InstrumentType type = InstrumentType.Equity)
    {
        var instrumentId = Guid.NewGuid();
        Context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = ticker,
            Ticker = ticker,
            Isin = isin,
            Type = type,
            CurrencyId = currencyId,
            CategoryId = 2,
            IsTrading = true,
            CreatedBy = UserId,
            UpdatedBy = UserId,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        return instrumentId;
    }

    public void AddInstrumentAlias(Guid instrumentId, string aliasCode)
    {
        Context.InstrumentAliases.Add(new InstrumentAlias
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            AliasCode = aliasCode,
            NormalizedAliasCode = aliasCode.Trim().ToUpperInvariant()
        });
    }

    public Guid AddOperation(
        Guid portfolioId,
        OperationType type,
        DateTime tradeDate,
        Guid? instrumentId = null,
        decimal quantity = 0m,
        decimal price = 0m,
        decimal fee = 0m,
        string currencyId = "RUB",
        string? brokerOperationKey = null,
        string? note = null)
    {
        var id = Guid.NewGuid();
        Context.Operations.Add(new Operation
        {
            Id = id,
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            CurrencyId = currencyId,
            TradeDate = tradeDate,
            SettlementDate = tradeDate,
            BrokerOperationKey = brokerOperationKey,
            Note = note,
            CreatedAt = tradeDate,
            UpdatedAt = tradeDate
        });

        return id;
    }

    public OperationModel BuildModel(
        Guid? instrumentId,
        OperationType type,
        decimal quantity,
        decimal price,
        decimal fee,
        string currencyId,
        DateTimeOffset tradeDate,
        DateTimeOffset? settlementDate = null,
        string? note = null) =>
        new(instrumentId, type, quantity, price, fee, currencyId, tradeDate, settlementDate, note);

    public PageQuery Page(int page = 1, int pageSize = 50) => new() { Page = page, PageSize = pageSize };

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    private void SeedReferenceData()
    {
        Context.Users.AddRange(
            new AppUser
            {
                Id = UserId,
                UserName = "ops-user",
                NormalizedUserName = "OPS-USER"
            },
            new AppUser
            {
                Id = OtherUserId,
                UserName = "other-user",
                NormalizedUserName = "OTHER-USER"
            });

        Context.SaveChanges();
    }

    internal sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }

    internal sealed class RecordingPortfolioRecalcService : IPortfolioRecalcService
    {
        public List<(Guid PortfolioId, DateTime FromDate)> Calls { get; } = [];

        public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default)
        {
            Calls.Add((portfolioId, fromDate));
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeBrokerReportParser(string code, Func<BrokerReportParseResult> parseFactory) : IBrokerReportParser
    {
        public string Code { get; } = code;

        public Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(parseFactory());
    }
}
