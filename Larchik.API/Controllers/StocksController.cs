using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class StocksController : BaseApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Unit>> CreateStock([FromBody] StockModel model)
    {
        return HandleResult(await Mediator.Send(new CreateStockCommand(model), HttpContext.RequestAborted));
    }
}