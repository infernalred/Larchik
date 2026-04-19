using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.ClearPortfolioData;
using Larchik.Application.Portfolios.CreatePortfolio;
using Larchik.Application.Portfolios.DeletePortfolio;
using Larchik.Application.Portfolios.EditPortfolio;
using Larchik.Application.Portfolios.GetPortfolio;
using Larchik.Application.Portfolios.GetPortfolios;
using Larchik.Application.Portfolios.RecalculatePortfolio;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;

namespace Larchik.Application.Tests.Portfolios;

internal sealed class PortfolioHandlersTestHarness : IAsyncDisposable
{
    internal static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid TbankBrokerId = Guid.Parse("f6f784ea-b520-4bc5-8a32-9a17f1637003");
    internal static readonly Guid VtbBrokerId = Guid.Parse("4ee304a8-6f0a-490f-bfa5-58f6f958b002");
    internal static readonly DateTime SeedTimestamp = new(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase database;
    private readonly FixedUserAccessor userAccessor = new(UserId);

    public PortfolioHandlersTestHarness()
    {
        database = SqliteTestContextFactory.Create();
        Context = database.Context;
        SeedReferenceData();
    }

    public LarchikContext Context { get; }
    public RecordingPortfolioRecalcService Recalc { get; } = new();

    public CreatePortfolioCommandHandler CreateHandler => new(Context, userAccessor);
    public EditPortfolioCommandHandler EditHandler => new(Context, userAccessor);
    public DeletePortfolioCommandHandler DeleteHandler => new(Context, userAccessor);
    public GetPortfolioQueryHandler GetHandler => new(Context, userAccessor);
    public GetPortfoliosQueryHandler GetManyHandler => new(Context, userAccessor);
    public ClearPortfolioDataCommandHandler ClearHandler => new(Context, userAccessor);
    public RecalculatePortfolioCommandHandler RecalculateHandler => new(Context, userAccessor, Recalc);

    public Guid AddPortfolio(
        string name,
        string reportingCurrencyId = "RUB",
        Guid? userId = null,
        Guid? brokerId = null)
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

    public Guid AddInstrument(string ticker, string currencyId = "RUB", int categoryId = 1)
    {
        var instrumentId = Guid.NewGuid();
        Context.Instruments.Add(new Instrument
        {
            Id = instrumentId,
            Name = ticker,
            Ticker = ticker,
            Isin = $"{ticker}0000001",
            Type = InstrumentType.Equity,
            CurrencyId = currencyId,
            CategoryId = categoryId,
            IsTrading = true,
            CreatedBy = UserId,
            UpdatedBy = UserId,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        return instrumentId;
    }

    public Guid AddOperation(
        Guid portfolioId,
        DateTime tradeDate,
        OperationType type = OperationType.Deposit,
        decimal price = 100m,
        decimal quantity = 0m,
        Guid? instrumentId = null,
        string currencyId = "RUB")
    {
        var operationId = Guid.NewGuid();
        Context.Operations.Add(new Operation
        {
            Id = operationId,
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = 0m,
            CurrencyId = currencyId,
            TradeDate = tradeDate,
            SettlementDate = tradeDate,
            CreatedAt = tradeDate,
            UpdatedAt = tradeDate
        });

        return operationId;
    }

    public void AddPositionSnapshot(Guid portfolioId, Guid instrumentId, DateTime date)
    {
        Context.PositionSnapshots.Add(new PositionSnapshot
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            InstrumentId = instrumentId,
            Date = date,
            Quantity = 1m,
            CostBase = 100m,
            MarketValueBase = 120m,
            UnrealizedBase = 20m,
            RealizedBase = 0m
        });
    }

    public void AddPortfolioSnapshot(Guid portfolioId, DateTime date)
    {
        Context.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            Date = date,
            NavBase = 120m,
            PnlDayBase = 0m,
            PnlMonthBase = 0m,
            PnlYearBase = 0m,
            CashBase = 20m
        });
    }

    public PortfolioModel BuildModel(string name, Guid brokerId, string reportingCurrencyId) =>
        new(name, brokerId, reportingCurrencyId);

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
                UserName = "portfolio-user",
                NormalizedUserName = "PORTFOLIO-USER"
            },
            new AppUser
            {
                Id = OtherUserId,
                UserName = "other-portfolio-user",
                NormalizedUserName = "OTHER-PORTFOLIO-USER"
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
}
