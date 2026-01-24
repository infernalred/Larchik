using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Stocks.GetStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PortfoliosController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PortfolioDto>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<PortfolioDto>>> List()
    {
        return HandleResult(await Mediator.Send(new GetPortfoliosQuery(), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<PortfolioDto>> Get(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetPortfolioQuery(id), HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Guid>> Create([FromBody] PortfolioModel model)
    {
        return HandleResult(await Mediator.Send(new CreatePortfolioCommand(model), HttpContext.RequestAborted));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Edit(Guid id, [FromBody] PortfolioModel model)
    {
        return HandleResult(await Mediator.Send(new EditPortfolioCommand(id, model), HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Delete(Guid id)
    {
        return HandleResult(await Mediator.Send(new DeletePortfolioCommand(id), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PortfolioSummaryDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<PortfolioSummaryDto>> GetSummary(Guid id, [FromQuery] string? method)
    {
        return HandleResult(await Mediator.Send(new GetPortfolioSummaryQuery(id, method), HttpContext.RequestAborted));
    }
}
