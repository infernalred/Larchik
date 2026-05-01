using System.Net;
using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Operations.CreateOperation;
using Larchik.Application.Operations.DeleteOperation;
using Larchik.Application.Operations.EditOperation;
using Larchik.Application.Operations.GetOperation;
using Larchik.Application.Operations.GetOperations;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

[Route("api/portfolios/{portfolioId:guid}/[controller]")]
public class OperationsController(
    GetOperationsQueryHandler listOperations,
    GetOperationQueryHandler getOperation,
    CreateOperationCommandHandler createOperation,
    EditOperationCommandHandler editOperation,
    DeleteOperationCommandHandler deleteOperation) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OperationDto>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<PagedResult<OperationDto>>> List(
        Guid portfolioId,
        [FromQuery] PageQuery paging)
    {
        return HandleResult(await listOperations.Handle(new GetOperationsQuery(portfolioId, paging), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<OperationDto>> Get(Guid portfolioId, Guid id)
    {
        return HandleResult(await getOperation.Handle(new GetOperationQuery(id), HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Guid>> Create(Guid portfolioId, [FromBody] OperationModel model)
    {
        return HandleResult(await createOperation.Handle(new CreateOperationCommand(portfolioId, model), HttpContext.RequestAborted));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Edit(Guid portfolioId, Guid id, [FromBody] OperationModel model)
    {
        return HandleResult(await editOperation.Handle(new EditOperationCommand(id, model), HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Delete(Guid portfolioId, Guid id)
    {
        return HandleResult(await deleteOperation.Handle(new DeleteOperationCommand(id), HttpContext.RequestAborted));
    }
}
