using Larchik.Application.Models;

namespace Larchik.Application.Operations.CreateOperation;

public record CreateOperationCommand(Guid PortfolioId, OperationModel Model);
