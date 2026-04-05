using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.SyncTbankInstrumentInfo;

public record SyncTbankInstrumentInfoCommand(
    string? BaseUrl = null,
    string? Token = null,
    bool? AllowInvalidTls = null,
    IReadOnlyCollection<string>? CountryExclusions = null,
    int? MaxParallelism = null) : IRequest<Result<int>>;
