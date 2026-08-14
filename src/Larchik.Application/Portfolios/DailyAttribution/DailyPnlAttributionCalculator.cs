using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.DailyAttribution;

public sealed class DailyPnlAttributionCalculator
{
    private const decimal ReconciliationTolerance = 0.01m;

    public DailyPnlAttributionDto Calculate(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime comparisonDate,
        DateTime valuationDate)
    {
        var startDate = comparisonDate.Date;
        var endDate = valuationDate.Date;
        var calculator = new PortfolioAnalyticsCalculator();
        var startSummary = calculator.CalculateSummary(
            portfolio,
            operations,
            instruments,
            data,
            "adjustingAvg",
            baseCurrency,
            startDate,
            includeAnnualizedReturn: false);
        var endSummary = calculator.CalculateSummary(
            portfolio,
            operations,
            instruments,
            data,
            "adjustingAvg",
            baseCurrency,
            endDate,
            includeAnnualizedReturn: false);

        var periodOperations = operations
            .Where(x => x.TradeDate.Date > startDate && x.TradeDate.Date <= endDate)
            .ToArray();
        var externalFlowBase = periodOperations.Sum(x =>
            PortfolioExternalFlowCalculator.Calculate(x, instruments, data, baseCurrency));
        var totalPnlBase = endSummary.NavBase - startSummary.NavBase - externalFlowBase;

        var startPositions = startSummary.Positions.ToDictionary(x => x.InstrumentId);
        var endPositions = endSummary.Positions.ToDictionary(x => x.InstrumentId);
        var positionIds = startPositions.Keys
            .Concat(endPositions.Keys)
            .Concat(periodOperations.Where(x => x.InstrumentId.HasValue).Select(x => x.InstrumentId!.Value))
            .Distinct()
            .ToArray();

        var positionRows = new List<PositionDailyPnlAttributionDto>(positionIds.Length);
        var globalWarnings = new List<string>();
        foreach (var instrumentId in positionIds)
        {
            if (!instruments.TryGetValue(instrumentId, out var instrument) || instrument.Type == InstrumentType.Currency)
            {
                continue;
            }

            startPositions.TryGetValue(instrumentId, out var startPosition);
            endPositions.TryGetValue(instrumentId, out var endPosition);
            var instrumentOperations = periodOperations.Where(x => x.InstrumentId == instrumentId).ToArray();
            var row = BuildPositionRow(
                instrument,
                startPosition,
                endPosition,
                instrumentOperations,
                data,
                baseCurrency,
                startDate,
                endDate);
            positionRows.Add(row);
            globalWarnings.AddRange(row.Warnings.Select(x => $"{instrument.Name}: {x}"));
        }

        positionRows.Sort(static (left, right) => left.PnlBase.CompareTo(right.PnlBase));

        var cashRows = BuildCashRows(
            portfolio,
            operations,
            startSummary,
            endSummary,
            instruments,
            data,
            baseCurrency,
            startDate,
            endDate);
        foreach (var cash in cashRows.Where(x => x.DataQuality != "complete"))
        {
            globalWarnings.Add($"Денежный остаток {cash.CurrencyId}: отсутствует или устарел валютный курс.");
        }
        var priceEffectBase = positionRows.Sum(x => x.PriceEffectBase);
        var securityFxEffectBase = positionRows.Sum(x => x.FxEffectBase);
        var crossEffectBase = positionRows.Sum(x => x.CrossEffectBase);
        var tradingEffectBase = positionRows.Sum(x => x.TradingEffectBase);
        var incomeEffectBase = CalculateIncomeEffect(periodOperations, data, baseCurrency);
        var feeEffectBase = CalculateFeeEffect(periodOperations, data, baseCurrency);
        var cashFxEffectBase = cashRows.Sum(x => x.FxEffectBase);
        var explainedBeforeOther = priceEffectBase + securityFxEffectBase + crossEffectBase + tradingEffectBase +
                                   cashFxEffectBase + incomeEffectBase + feeEffectBase;
        var otherEffectBase = totalPnlBase - explainedBeforeOther;
        var reconciliationResidualBase = totalPnlBase - (explainedBeforeOther + otherEffectBase);

        if (periodOperations.Any(x => x.Type == OperationType.CashAdjustment) && otherEffectBase != 0m)
        {
            globalWarnings.Add("Прочие денежные корректировки включены в компонент «Другое».");
        }

        if (Math.Abs(reconciliationResidualBase) > ReconciliationTolerance)
        {
            globalWarnings.Add($"Остаток сверки превышает {ReconciliationTolerance:N2} {baseCurrency}.");
        }

        var positionIncome = periodOperations
            .Where(x => x.Type == OperationType.Dividend && x.InstrumentId.HasValue)
            .GroupBy(x => x.InstrumentId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(op => ConvertIncome(op, data, baseCurrency)));
        var positionFees = periodOperations
            .Where(x => x.InstrumentId.HasValue)
            .GroupBy(x => x.InstrumentId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(op => ConvertFee(op, data, baseCurrency)));
        positionRows = positionRows
            .Select(row =>
            {
                var income = positionIncome.GetValueOrDefault(row.InstrumentId);
                var fee = positionFees.GetValueOrDefault(row.InstrumentId);
                return row with
                {
                    IncomeEffectBase = income,
                    FeeEffectBase = fee,
                    PnlBase = row.PnlBase + income + fee
                };
            })
            .OrderBy(x => x.PnlBase)
            .ToList();

        return new DailyPnlAttributionDto
        {
            PortfolioId = portfolio.Id,
            Name = portfolio.Name,
            ReportingCurrencyId = baseCurrency,
            ComparisonDate = startDate,
            ValuationDate = endDate,
            StartNavBase = startSummary.NavBase,
            EndNavBase = endSummary.NavBase,
            ExternalFlowBase = externalFlowBase,
            PnlBase = totalPnlBase,
            ReturnPct = startSummary.NavBase == 0m ? null : totalPnlBase / startSummary.NavBase,
            PriceEffectBase = priceEffectBase,
            SecurityFxEffectBase = securityFxEffectBase,
            CrossEffectBase = crossEffectBase,
            TradingEffectBase = tradingEffectBase,
            CashFxEffectBase = cashFxEffectBase,
            IncomeEffectBase = incomeEffectBase,
            FeeEffectBase = feeEffectBase,
            OtherEffectBase = otherEffectBase,
            ReconciliationResidualBase = reconciliationResidualBase,
            IsComplete = globalWarnings.Count == 0 && Math.Abs(reconciliationResidualBase) <= ReconciliationTolerance,
            Warnings = globalWarnings.Distinct().ToArray(),
            Positions = positionRows,
            Cash = cashRows
        };
    }

    private static PositionDailyPnlAttributionDto BuildPositionRow(
        Instrument instrument,
        PositionHoldingDto? startPosition,
        PositionHoldingDto? endPosition,
        IReadOnlyCollection<Operation> operations,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        var startPrice = data.GetPrice(instrument.Id, startDate);
        var endPrice = data.GetPrice(instrument.Id, endDate);
        var currency = endPrice?.CurrencyId ?? startPrice?.CurrencyId ?? instrument.CurrencyId;
        var startFx = data.GetRateQuote(currency, baseCurrency, startDate);
        var endFx = data.GetRateQuote(currency, baseCurrency, endDate);
        var startQuantity = startPosition?.Quantity ?? 0m;
        var endQuantity = endPosition?.Quantity ?? 0m;
        var startMarketValueBase = startPosition?.MarketValueBase ?? 0m;
        var endMarketValueBase = endPosition?.MarketValueBase ?? 0m;
        var capitalFlowBase = operations.Sum(x => CalculateSecurityCapitalFlowBase(x, instrument, data, baseCurrency));
        var positionPnlBase = endMarketValueBase - startMarketValueBase - capitalFlowBase;
        var warnings = new List<string>();

        decimal priceEffectBase = 0m;
        decimal fxEffectBase = 0m;
        decimal crossEffectBase = 0m;
        decimal? priceReturnPct = null;
        decimal? fxReturnPct = null;
        decimal? totalMarketReturnPct = null;
        if (startPrice is null || endPrice is null)
        {
            warnings.Add("нет цены для одной из дат оценки");
        }
        else if (!string.Equals(startPrice.CurrencyId, endPrice.CurrencyId, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("валюта котировки изменилась между датами; эффект отнесён в торговый/прочий компонент");
        }
        else if (startFx is null || endFx is null)
        {
            warnings.Add($"нет курса {currency}/{baseCurrency} для одной из дат оценки");
        }
        else
        {
            var priceMove = endPrice.Value - startPrice.Value;
            var fxMove = endFx.Rate - startFx.Rate;
            priceEffectBase = startQuantity * priceMove * startFx.Rate;
            fxEffectBase = startQuantity * startPrice.Value * fxMove;
            crossEffectBase = startQuantity * priceMove * fxMove;
            priceReturnPct = startPrice.Value == 0m ? null : priceMove / startPrice.Value;
            fxReturnPct = startFx.Rate == 0m ? null : fxMove / startFx.Rate;
            var startUnitBase = startPrice.Value * startFx.Rate;
            totalMarketReturnPct = startUnitBase == 0m
                ? null
                : endPrice.Value * endFx.Rate / startUnitBase - 1m;
        }

        if (startPrice is not null && startPrice.Date.Date < startDate)
        {
            warnings.Add($"начальная цена устарела: {startPrice.Date:yyyy-MM-dd}");
        }

        if (endPrice is not null && endPrice.Date.Date < endDate)
        {
            warnings.Add($"конечная цена устарела: {endPrice.Date:yyyy-MM-dd}");
        }

        if (startFx is not null && startFx.Date.Date < startDate && !string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"начальный курс устарел: {startFx.Date:yyyy-MM-dd}");
        }

        if (endFx is not null && endFx.Date.Date < endDate && !string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"конечный курс устарел: {endFx.Date:yyyy-MM-dd}");
        }

        var tradingEffectBase = positionPnlBase - priceEffectBase - fxEffectBase - crossEffectBase;
        return new PositionDailyPnlAttributionDto
        {
            InstrumentId = instrument.Id,
            InstrumentName = instrument.Name,
            InstrumentType = instrument.Type.ToString(),
            CategoryName = instrument.Category?.Name,
            CurrencyId = currency,
            StartQuantity = startQuantity,
            EndQuantity = endQuantity,
            StartPrice = startPrice?.Value,
            EndPrice = endPrice?.Value,
            StartPriceDate = startPrice?.Date.Date,
            EndPriceDate = endPrice?.Date.Date,
            StartFxRate = startFx?.Rate,
            EndFxRate = endFx?.Rate,
            StartFxRateDate = startFx?.Date.Date,
            EndFxRateDate = endFx?.Date.Date,
            StartMarketValueBase = startMarketValueBase,
            EndMarketValueBase = endMarketValueBase,
            PnlBase = positionPnlBase,
            ReturnPct = startMarketValueBase == 0m ? null : positionPnlBase / startMarketValueBase,
            PriceReturnPct = priceReturnPct,
            FxReturnPct = fxReturnPct,
            TotalMarketReturnPct = totalMarketReturnPct,
            PriceEffectBase = priceEffectBase,
            FxEffectBase = fxEffectBase,
            CrossEffectBase = crossEffectBase,
            TradingEffectBase = tradingEffectBase,
            DataQuality = warnings.Count == 0 ? "complete" : ResolveDataQuality(warnings),
            Warnings = warnings
        };
    }

    private static IReadOnlyCollection<CashDailyPnlAttributionDto> BuildCashRows(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        PortfolioSummaryDto startSummary,
        PortfolioSummaryDto endSummary,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        var startCash = startSummary.Cash.ToDictionary(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase);
        var endCash = endSummary.Cash.ToDictionary(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase);
        var currencies = startCash.Keys.Concat(endCash.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        var cashMovementBaseByCurrency = CalculateCashMovementBaseByCurrency(
            portfolio,
            operations,
            instruments,
            data,
            baseCurrency,
            startDate,
            endDate);

        return currencies
            .Select(currency =>
            {
                var startAmount = startCash.GetValueOrDefault(currency)?.Amount ?? 0m;
                var endAmount = endCash.GetValueOrDefault(currency)?.Amount ?? 0m;
                var startAmountBase = startCash.GetValueOrDefault(currency)?.AmountInBase ?? 0m;
                var endAmountBase = endCash.GetValueOrDefault(currency)?.AmountInBase ?? 0m;
                var startFx = data.GetRateQuote(currency, baseCurrency, startDate);
                var endFx = data.GetRateQuote(currency, baseCurrency, endDate);
                var movementBase = cashMovementBaseByCurrency.GetValueOrDefault(currency);
                var isBaseCurrency = string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase);
                var isStale = !isBaseCurrency &&
                              (startFx?.Date.Date < startDate || endFx?.Date.Date < endDate);
                return new CashDailyPnlAttributionDto
                {
                    CurrencyId = currency,
                    StartAmount = startAmount,
                    EndAmount = endAmount,
                    StartFxRate = startFx?.Rate,
                    EndFxRate = endFx?.Rate,
                    FxEffectBase = endAmountBase - startAmountBase - movementBase,
                    DataQuality = startFx is null || endFx is null ? "missingFx" : isStale ? "stale" : "complete"
                };
            })
            .OrderBy(x => x.CurrencyId)
            .ToArray();
    }

    private static Dictionary<string, decimal> CalculateCashMovementBaseByCurrency(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var usesBrokerCashLedger = BrokerCashLedgerHelper.UsesBrokerCashLedger(portfolio);
        foreach (var operation in operations)
        {
            var effectiveDate = operation.Type is OperationType.Buy or OperationType.Sell or
                OperationType.BondPartialRedemption or OperationType.BondMaturity
                ? BrokerCashLedgerHelper.GetCashEffectiveDate(operation)
                : operation.TradeDate.Date;
            if (effectiveDate <= startDate || effectiveDate > endDate)
            {
                continue;
            }

            foreach (var movement in GetCashMovements(operation, usesBrokerCashLedger, instruments))
            {
                var movementBase = data.Convert(movement.Amount, movement.CurrencyId, baseCurrency, effectiveDate);
                result[movement.CurrencyId] = result.GetValueOrDefault(movement.CurrencyId) + movementBase;
            }
        }

        return result;
    }

    private static IEnumerable<CashMovement> GetCashMovements(
        Operation operation,
        bool usesBrokerCashLedger,
        IReadOnlyDictionary<Guid, Instrument> instruments)
    {
        var amount = operation.Price != 0m ? operation.Price : operation.Quantity;
        var tradeValue = operation.Quantity * operation.Price;
        var imported = BrokerCashLedgerHelper.IsImportedBrokerOperation(operation, usesBrokerCashLedger);
        var instrument = operation.InstrumentId.HasValue
            ? instruments.GetValueOrDefault(operation.InstrumentId.Value)
            : null;
        switch (operation.Type)
        {
            case OperationType.Buy when !imported:
                if (instrument?.Type == InstrumentType.Currency)
                {
                    yield return new CashMovement(instrument.CurrencyId, operation.Quantity);
                }
                yield return new CashMovement(operation.CurrencyId, -(tradeValue + operation.Fee));
                break;
            case OperationType.Sell when !imported:
                if (instrument?.Type == InstrumentType.Currency)
                {
                    yield return new CashMovement(instrument.CurrencyId, -operation.Quantity);
                }
                yield return new CashMovement(operation.CurrencyId, tradeValue - operation.Fee);
                break;
            case OperationType.BondPartialRedemption:
            case OperationType.BondMaturity:
                yield return new CashMovement(operation.CurrencyId, tradeValue - operation.Fee);
                break;
            case OperationType.Dividend:
                yield return new CashMovement(operation.CurrencyId, amount);
                break;
            case OperationType.Fee:
                yield return new CashMovement(operation.CurrencyId, -(amount != 0m ? amount : operation.Fee));
                break;
            case OperationType.CashAdjustment:
                yield return new CashMovement(operation.CurrencyId, operation.Price);
                break;
            case OperationType.Deposit:
            case OperationType.TransferIn when operation.InstrumentId is null:
                yield return new CashMovement(operation.CurrencyId, amount);
                break;
            case OperationType.TransferIn when instrument?.Type == InstrumentType.Currency:
                yield return new CashMovement(instrument.CurrencyId, operation.Quantity);
                break;
            case OperationType.Withdraw:
            case OperationType.TransferOut when operation.InstrumentId is null:
                yield return new CashMovement(operation.CurrencyId, -amount);
                break;
            case OperationType.TransferOut when instrument?.Type == InstrumentType.Currency:
                yield return new CashMovement(instrument.CurrencyId, -operation.Quantity);
                break;
        }
    }

    private static decimal CalculateSecurityCapitalFlowBase(
        Operation operation,
        Instrument instrument,
        HistoricalDataLookup data,
        string baseCurrency) => operation.Type switch
    {
        OperationType.Buy => data.Convert(operation.Quantity * operation.Price, operation.CurrencyId, baseCurrency, operation.TradeDate),
        OperationType.Sell => -data.Convert(operation.Quantity * operation.Price, operation.CurrencyId, baseCurrency, operation.TradeDate),
        OperationType.TransferIn => CalculateTransferValueBase(operation, instrument, data, baseCurrency),
        OperationType.TransferOut => -CalculateTransferValueBase(operation, instrument, data, baseCurrency),
        OperationType.BondPartialRedemption or OperationType.BondMaturity =>
            -data.Convert(operation.Quantity * operation.Price, operation.CurrencyId, baseCurrency, operation.TradeDate),
        _ => 0m
    };

    private static decimal CalculateTransferValueBase(
        Operation operation,
        Instrument instrument,
        HistoricalDataLookup data,
        string baseCurrency)
    {
        var price = data.GetPrice(instrument.Id, operation.TradeDate);
        return price is null
            ? 0m
            : data.Convert(operation.Quantity * price.Value, price.CurrencyId, baseCurrency, operation.TradeDate);
    }

    private static decimal CalculateIncomeEffect(
        IEnumerable<Operation> operations,
        HistoricalDataLookup data,
        string baseCurrency) => operations
        .Where(x => x.Type == OperationType.Dividend)
        .Sum(x => ConvertIncome(x, data, baseCurrency));

    private static decimal ConvertIncome(Operation operation, HistoricalDataLookup data, string baseCurrency)
    {
        var amount = operation.Price != 0m ? operation.Price : operation.Quantity;
        return data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate);
    }

    private static decimal CalculateFeeEffect(
        IEnumerable<Operation> operations,
        HistoricalDataLookup data,
        string baseCurrency) => operations.Sum(x => ConvertFee(x, data, baseCurrency));

    private static decimal ConvertFee(Operation operation, HistoricalDataLookup data, string baseCurrency)
    {
        var amount = operation.Type == OperationType.Fee
            ? operation.Price != 0m ? operation.Price : operation.Fee
            : operation.Fee;
        return amount == 0m ? 0m : -data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate);
    }

    private static string ResolveDataQuality(IEnumerable<string> warnings)
    {
        var items = warnings.ToArray();
        if (items.Any(x => x.Contains("нет цены", StringComparison.Ordinal)))
        {
            return "missingPrice";
        }

        if (items.Any(x => x.Contains("нет курса", StringComparison.Ordinal)))
        {
            return "missingFx";
        }

        return "stale";
    }

    private sealed record CashMovement(string CurrencyId, decimal Amount);
}
