using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record SyncCbrFxRatesCommand(DateOnly? Date = null) : IRequest<Result<int>>;
