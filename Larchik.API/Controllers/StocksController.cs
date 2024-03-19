using System.Net;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Application.Stocks.GetPagedStocks;
using Larchik.Application.Stocks.GetStock;
using Larchik.Application.Stocks.SearchStocks;
using Larchik.Application.Stocks.UpdateStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class StocksController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(StockDto[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<StockDto[]>> SearchStocks([FromQuery] string ticker)
    {
        return HandleResult(await Mediator.Send(new SearchStocksQuery(ticker), HttpContext.RequestAborted));
    }

    [HttpGet]
    [ProducesResponseType(typeof(StockDto[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Result<StockDto[]>>> GetPagedStocks([FromQuery] StockFilter filter)
    {
        return HandleResult(await Mediator.Send(new GetPagedQuery(filter), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Result<StockDto>>> GetStock(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetStockQuery(id), HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Guid>> CreateStock(StockDto model)
    {
        var result = await Mediator.Send(new CreateStockCommand(model), HttpContext.RequestAborted);

        return CreatedAtRoute(nameof(GetStock), new {id = result.Value});
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Result<Unit>>> UpdateStock(Guid id, StockDto model)
    {
        return HandleResult(await Mediator.Send(new UpdateStockCommand(id, model), HttpContext.RequestAborted));
    }
}