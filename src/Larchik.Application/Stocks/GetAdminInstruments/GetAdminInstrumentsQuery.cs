using Larchik.Application.Common.Paging;

namespace Larchik.Application.Stocks.GetAdminInstruments;

public record GetAdminInstrumentsQuery(string? Query, string? Country, bool? IsTrading, PageQuery Paging);
