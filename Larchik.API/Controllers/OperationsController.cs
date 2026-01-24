using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Stocks.GetStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

[Route("api/portfolios/{portfolioId:guid}/[controller]")]
public class OperationsController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OperationDto>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<OperationDto>>> List(Guid portfolioId)
    {
        return HandleResult(await Mediator.Send(new GetOperationsQuery(portfolioId), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<OperationDto>> Get(Guid portfolioId, Guid id)
    {
        return HandleResult(await Mediator.Send(new GetOperationQuery(id), HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Guid>> Create(Guid portfolioId, [FromBody] OperationModel model)
    {
        return HandleResult(await Mediator.Send(new CreateOperationCommand(portfolioId, model), HttpContext.RequestAborted));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Edit(Guid portfolioId, Guid id, [FromBody] OperationModel model)
    {
        return HandleResult(await Mediator.Send(new EditOperationCommand(id, model), HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Delete(Guid portfolioId, Guid id)
    {
        return HandleResult(await Mediator.Send(new DeleteOperationCommand(id), HttpContext.RequestAborted));
    }
}
