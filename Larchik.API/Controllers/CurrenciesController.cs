using System.Net;
using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class CurrenciesController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Currency[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Currency[]>> GetCurrencies()
    {
        return HandleResult(await Mediator.Send(new GetCurrenciesQuery(), HttpContext.RequestAborted));
    }
}