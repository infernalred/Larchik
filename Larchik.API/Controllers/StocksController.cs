﻿using System.Net;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Application.Stocks;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class StocksController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<List<StockDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<List<StockDto>>>> GetStocks()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
}