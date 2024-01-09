using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Сurrencies;
using Larchik.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class CurrencyController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Result<List<Currency>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Result<List<Currency>>>> GetStocks()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
}