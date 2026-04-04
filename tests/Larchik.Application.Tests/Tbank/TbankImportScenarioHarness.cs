using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Application.Operations.CreateOperation;
using Larchik.Application.Operations.DeleteOperation;
using Larchik.Application.Operations.EditOperation;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Application.Portfolios.GetPortfolioPerformance;
using Larchik.Application.Portfolios.GetPortfoliosSummary;
using Larchik.Application.Portfolios.GetPortfolioSummary;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public sealed class TbankImportScenarioHarness : IAsyncDisposable
{
    private static readonly Guid UserId = Guid.Parse("7e89d7d2-21e2-40ce-bef2-58c3b9408abb");
    private static readonly Guid BrokerId = Guid.Parse("f6f784ea-b520-4bc5-8a32-9a17f1637003");

    private readonly SqliteConnection _connection;
    private readonly LarchikContext _context;
    private readonly IUserAccessor _userAccessor;
    private readonly IPortfolioRecalcService _recalc;
    private readonly CreateOperationCommandHandler _createHandler;
    private readonly DeleteOperationCommandHandler _deleteHandler;
    private readonly EditOperationCommandHandler _editHandler;
    private readonly ImportBrokerReportCommandHandler _importHandler;
    private readonly GetPortfolioPerformanceQueryHandler _performanceHandler;
    private readonly GetPortfoliosSummaryQueryHandler _portfoliosSummaryHandler;
    private readonly GetPortfolioSummaryQueryHandler _summaryHandler;
    private readonly Dictionary<string, Guid> _instrumentIdByTicker;

    public TbankImportScenarioHarness()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LarchikContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LarchikContext(options);
        _context.Database.EnsureCreated();

        _userAccessor = new FixedUserAccessor(UserId);
        _recalc = new NoOpPortfolioRecalcService();
        _createHandler = new CreateOperationCommandHandler(_context, _userAccessor, _recalc);
        _deleteHandler = new DeleteOperationCommandHandler(_context, _userAccessor, _recalc);
        _editHandler = new EditOperationCommandHandler(_context, _userAccessor, _recalc);
        _importHandler = new ImportBrokerReportCommandHandler(
            _context,
            _userAccessor,
            _recalc,
            [new TbankReportParser(NullLogger<TbankReportParser>.Instance)],
            NullLogger<ImportBrokerReportCommandHandler>.Instance);
        _performanceHandler = new GetPortfolioPerformanceQueryHandler(_context, _userAccessor);
        _portfoliosSummaryHandler = new GetPortfoliosSummaryQueryHandler(_context, _userAccessor);
        _summaryHandler = new GetPortfolioSummaryQueryHandler(_context, _userAccessor);

        SeedReferenceData(TbankReferenceData.Load());
        _instrumentIdByTicker = _context.Instruments
            .AsNoTracking()
            .ToDictionary(x => x.Ticker, x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public Guid PortfolioId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task<ImportResultDto> ImportAsync(string fileName)
    {
        await using var stream = File.OpenRead(TbankReportFixtureHelper.ResolveFixturePath(fileName));
        var result = await _importHandler.Handle(
            new ImportBrokerReportCommand(PortfolioId, "tbank", stream, fileName),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        await NormalizeOperationTimestampsAsync();
        return result.Value!;
    }

    public async Task<ImportResultDto> ImportSyntheticAsync(string brokerCode, params ParsedOperation[] operations)
    {
        var handler = new ImportBrokerReportCommandHandler(
            _context,
            _userAccessor,
            _recalc,
            [new FakeBrokerReportParser(brokerCode, operations)],
            NullLogger<ImportBrokerReportCommandHandler>.Instance);

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await handler.Handle(
            new ImportBrokerReportCommand(PortfolioId, brokerCode, stream, "synthetic.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        await NormalizeOperationTimestampsAsync();
        return result.Value!;
    }

    public Guid GetInstrumentId(string ticker) => _instrumentIdByTicker[ticker];

    public async Task<Guid> CreateOperationAsync(OperationModel model)
    {
        var result = await _createHandler.Handle(new CreateOperationCommand(PortfolioId, model), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        await NormalizeOperationTimestampsAsync();
        return result.Value;
    }

    public async Task EditOperationAsync(Guid operationId, OperationModel model)
    {
        var result = await _editHandler.Handle(new EditOperationCommand(operationId, model), CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);
        await NormalizeOperationTimestampsAsync();
    }

    public async Task DeleteOperationAsync(Guid operationId)
    {
        var result = await _deleteHandler.Handle(new DeleteOperationCommand(operationId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        await NormalizeOperationTimestampsAsync();
    }

    public async Task ApplyManualOperationsAsync()
    {
        foreach (var seed in TbankReferenceData.LoadManualOperations())
        {
            _context.Operations.Add(new Operation
            {
                Id = Guid.NewGuid(),
                PortfolioId = PortfolioId,
                InstrumentId = seed.Ticker is null ? null : _instrumentIdByTicker[seed.Ticker],
                Type = seed.Type,
                Quantity = seed.Quantity,
                Price = seed.Price,
                Fee = seed.Fee,
                CurrencyId = seed.CurrencyId,
                TradeDate = DateTime.SpecifyKind(seed.TradeDate, DateTimeKind.Utc),
                SettlementDate = seed.SettlementDate is null
                    ? null
                    : DateTime.SpecifyKind(seed.SettlementDate.Value, DateTimeKind.Utc),
                Note = seed.Note,
                CreatedAt = DateTime.SpecifyKind(seed.TradeDate, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(seed.TradeDate, DateTimeKind.Utc)
            });
        }

        await _context.SaveChangesAsync();
        await NormalizeOperationTimestampsAsync();
    }

    public Task<PortfolioSummaryDto> GetSummaryAsync()
    {
        return GetSummaryAsync(PortfolioId);
    }

    public async Task<PortfolioSummaryDto> GetSummaryAsync(Guid portfolioId)
    {
        var result = await _summaryHandler.Handle(new GetPortfolioSummaryQuery(portfolioId), CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);
        return result.Value!;
    }

    public async Task<PortfoliosSummaryDto> GetPortfoliosSummaryAsync(string? method = null, string? currency = null)
    {
        var result = await _portfoliosSummaryHandler.Handle(
            new GetPortfoliosSummaryQuery(method, currency),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    public async Task<IReadOnlyCollection<PortfolioPerformanceDto>> GetPerformanceAsync(
        DateTime? from = null,
        DateTime? to = null,
        string? method = null)
    {
        var result = await _performanceHandler.Handle(
            new GetPortfolioPerformanceQuery(PortfolioId, method, from, to),
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result!.IsSuccess, result.Error);
        return result.Value!;
    }

    public Task<int> CountOperationsAsync() => _context.Operations.CountAsync(x => x.PortfolioId == PortfolioId);

    public Task<Dictionary<OperationType, int>> GetBreakdownAsync() =>
        _context.Operations
            .Where(x => x.PortfolioId == PortfolioId)
            .GroupBy(x => x.Type)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public async Task<IReadOnlyDictionary<string, decimal>> GetCashByCurrencyAsync()
    {
        var summary = await GetSummaryAsync();
        return summary.Cash
            .OrderBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.CurrencyId, x => decimal.Round(x.Amount, 2, MidpointRounding.AwayFromZero), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<PositionSnapshotItem>> GetOpenPositionsAsync()
    {
        var summary = await GetSummaryAsync();
        var instrumentById = await _context.Instruments
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id);

        return summary.Positions
            .Where(x => x.Quantity != 0)
            .Select(x =>
            {
                var instrument = instrumentById[x.InstrumentId];
                return new PositionSnapshotItem(
                    instrument.Ticker,
                    x.InstrumentName,
                    x.Quantity,
                    x.LastPrice is null ? null : decimal.Round(x.LastPrice.Value, 2, MidpointRounding.AwayFromZero),
                    decimal.Round(x.MarketValueBase, 2, MidpointRounding.AwayFromZero),
                    decimal.Round(x.AverageCost, 2, MidpointRounding.AwayFromZero),
                    x.CurrencyId,
                    x.PriceCurrencyId,
                    x.AverageCostCurrencyId);
            })
            .OrderBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<OperationSnapshotItem>> GetOperationsAsync()
    {
        var operations = await _context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == PortfolioId)
            .Include(x => x.Instrument)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new OperationSnapshotItem(
                x.Id,
                x.Type,
                x.Instrument != null ? x.Instrument.Ticker : null,
                x.Quantity,
                x.Price,
                x.Fee,
                x.CurrencyId,
                x.TradeDate,
                x.SettlementDate,
                x.Note,
                x.BrokerOperationKey))
            .ToListAsync();

        return operations;
    }

    private void SeedReferenceData(TbankReferenceData data)
    {
        var existingCurrencyIds = _context.Currencies.AsNoTracking().Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCategoryIds = _context.Categories.AsNoTracking().Select(x => x.Id).ToHashSet();

        _context.Currencies.AddRange(data.Currencies
            .Where(x => !existingCurrencyIds.Contains(x.Id))
            .Select(x => new Currency { Id = x.Id }));

        _context.Categories.AddRange(data.Categories
            .Where(x => !existingCategoryIds.Contains(x.Id))
            .Select(x => new Category { Id = x.Id, Name = x.Name }));

        _context.Instruments.AddRange(data.Instruments.Select(x => new Instrument
        {
            Id = x.Id,
            Name = x.Name,
            Ticker = x.Ticker,
            Isin = x.Isin,
            Figi = x.Figi,
            Type = x.Type,
            CurrencyId = x.CurrencyId,
            CategoryId = x.CategoryId,
            Exchange = x.Exchange,
            Country = x.Country,
            IsTrading = x.IsTrading,
            CreatedBy = UserId,
            UpdatedBy = UserId,
            CreatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc)
        }));

        _context.InstrumentAliases.AddRange(data.Aliases.Select(x => new InstrumentAlias
        {
            Id = Guid.NewGuid(),
            InstrumentId = x.InstrumentId,
            AliasCode = x.AliasCode,
            NormalizedAliasCode = x.NormalizedAliasCode
        }));

        _context.InstrumentCorporateActions.AddRange(data.CorporateActions.Select(x => new InstrumentCorporateAction
        {
            Id = Guid.NewGuid(),
            InstrumentId = x.InstrumentId,
            Type = x.Type,
            Factor = x.Factor,
            EffectiveDate = DateTime.SpecifyKind(x.EffectiveDate, DateTimeKind.Utc),
            Note = x.Note
        }));

        _context.Prices.AddRange(data.Prices.Select(x => new Price
        {
            Id = Guid.NewGuid(),
            InstrumentId = x.InstrumentId,
            Date = DateTime.SpecifyKind(x.Date, DateTimeKind.Utc),
            Value = x.Value,
            CurrencyId = x.CurrencyId,
            SourceCurrencyId = x.SourceCurrencyId,
            Provider = x.Provider,
            CreatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc)
        }));

        _context.FxRates.AddRange(data.FxRates.Select(x => new FxRate
        {
            Id = Guid.NewGuid(),
            BaseCurrencyId = x.BaseCurrencyId,
            QuoteCurrencyId = x.QuoteCurrencyId,
            Date = DateTime.SpecifyKind(x.Date, DateTimeKind.Utc),
            Rate = x.Rate,
            Source = x.Source,
            CreatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc)
        }));

        _context.Portfolios.Add(new Portfolio
        {
            Id = PortfolioId,
            UserId = UserId,
            BrokerId = BrokerId,
            Name = "T-Bank Regression",
            ReportingCurrencyId = "RUB",
            CreatedAt = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc)
        });

        _context.SaveChanges();
    }

    public ValueTask DisposeAsync()
    {
        _context.Dispose();
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class FixedUserAccessor(Guid userId) : IUserAccessor
    {
        public Guid GetUserId() => userId;
    }

    private sealed class NoOpPortfolioRecalcService : IPortfolioRecalcService
    {
        public Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeBrokerReportParser(string brokerCode, IReadOnlyCollection<ParsedOperation> operations) : IBrokerReportParser
    {
        public string Code => brokerCode;

        public Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new BrokerReportParseResult(operations.ToList(), []));
    }

    private async Task NormalizeOperationTimestampsAsync()
    {
        var baseTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var operations = await _context.Operations
            .Where(x => x.PortfolioId == PortfolioId)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < operations.Count; i++)
        {
            var timestamp = baseTimestamp.AddSeconds(i);
            operations[i].CreatedAt = timestamp;
            operations[i].UpdatedAt = timestamp;
        }

        await _context.SaveChangesAsync();
    }

    public sealed record PositionSnapshotItem(
        string Ticker,
        string InstrumentName,
        decimal Quantity,
        decimal? LastPrice,
        decimal MarketValueBase,
        decimal AverageCost,
        string CurrencyId,
        string? PriceCurrencyId,
        string? AverageCostCurrencyId);

    public sealed record OperationSnapshotItem(
        Guid Id,
        OperationType Type,
        string? Ticker,
        decimal Quantity,
        decimal Price,
        decimal Fee,
        string CurrencyId,
        DateTime TradeDate,
        DateTime? SettlementDate,
        string? Note,
        string? BrokerOperationKey);
}
