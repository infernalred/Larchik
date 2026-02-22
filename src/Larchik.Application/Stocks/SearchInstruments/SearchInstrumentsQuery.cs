using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.SearchInstruments;

public record SearchInstrumentsQuery(string? Query, int Limit = 20) : IRequest<Result<InstrumentLookupDto[]>>;
