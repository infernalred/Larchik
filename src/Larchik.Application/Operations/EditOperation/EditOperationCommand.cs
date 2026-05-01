using Larchik.Application.Models;

namespace Larchik.Application.Operations.EditOperation;

public record EditOperationCommand(Guid Id, OperationModel Model);
