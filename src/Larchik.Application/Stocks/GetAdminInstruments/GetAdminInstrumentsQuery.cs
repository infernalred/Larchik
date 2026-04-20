using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetAdminInstruments;

public record GetAdminInstrumentsQuery(string? Query, string? Country, bool? IsTrading, PageQuery Paging) : IRequest<Result<PagedResult<InstrumentDto>>>;
