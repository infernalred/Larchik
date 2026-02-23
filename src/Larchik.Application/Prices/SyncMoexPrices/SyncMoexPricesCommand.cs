using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Prices.SyncMoexPrices;

public record SyncMoexPricesCommand(
    DateOnly? Date,
    IReadOnlyCollection<string>? Boards = null,
    string? Provider = null,
    string? BaseUrl = null) : IRequest<Result<int>>;
