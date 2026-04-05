using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.FxRates.SyncCbrFxRates;

public record SyncCbrFxRatesCommand(DateOnly? Date = null) : IRequest<Result<int>>;
