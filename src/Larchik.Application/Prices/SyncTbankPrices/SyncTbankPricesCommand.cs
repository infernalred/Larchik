using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Prices.SyncTbankPrices;

public record SyncTbankPricesCommand(
    DateOnly? Date,
    string? Provider = null,
    string? BaseUrl = null,
    string? Token = null,
    bool? AllowInvalidTls = null,
    IReadOnlyCollection<string>? CountryExclusions = null,
    int? MaxHistoryLookbackDays = null,
    int? MaxParallelism = null) : IRequest<Result<int>>;
