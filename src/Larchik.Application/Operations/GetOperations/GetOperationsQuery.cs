using Larchik.Application.Common.Paging;

namespace Larchik.Application.Operations.GetOperations;

public record GetOperationsQuery(Guid PortfolioId, PageQuery Paging);

