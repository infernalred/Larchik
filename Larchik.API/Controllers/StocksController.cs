using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Application.Stocks.EditStock;
using Larchik.Application.Stocks.GetStock;
using Larchik.Persistence.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class StocksController : BaseApiController
{
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Unit>> CreateStock([FromBody] StockModel model)
    {
        return HandleResult(await Mediator.Send(new CreateStockCommand(model), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<StockDto>> GetStock(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetStockQuery(id), HttpContext.RequestAborted));
    }

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> EditStock(Guid id, [FromBody] StockModel model)
    {
        return HandleResult(await Mediator.Send(new EditStockCommand(id, model), HttpContext.RequestAborted));
    }
}