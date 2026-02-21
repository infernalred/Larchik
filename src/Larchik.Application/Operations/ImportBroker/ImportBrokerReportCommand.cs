using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Operations.ImportBroker;

public record ImportBrokerReportCommand(Guid PortfolioId, string BrokerCode, Stream FileStream, string FileName)
    : IRequest<Result<ImportResultDto>>;
