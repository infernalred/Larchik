using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations;

internal static class OperationQueryHelper
{
    public static IQueryable<Operation> WhereVisibleInPortfolio(this IQueryable<Operation> query) =>
        query.Where(x => x.Type != OperationType.Split && x.Type != OperationType.ReverseSplit);

    public static IQueryable<OperationDto> ProjectToDto(this IQueryable<Operation> query) =>
        query.Select(x => new OperationDto
        {
            Id = x.Id,
            PortfolioId = x.PortfolioId,
            InstrumentId = x.InstrumentId,
            InstrumentTicker = x.Instrument != null ? x.Instrument.Ticker : null,
            Type = x.Type,
            Quantity = x.Quantity,
            Price = x.Price,
            Fee = x.Fee,
            CurrencyId = x.CurrencyId,
            TradeDate = x.TradeDate,
            SettlementDate = x.SettlementDate,
            Note = x.Note,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
}
