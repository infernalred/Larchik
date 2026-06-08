using System.Net;
using Larchik.Application.Currencies.CreateCurrency;
using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Application.Currencies.UpdateCurrency;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Constants;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class CurrenciesController(
    GetCurrenciesQueryHandler getCurrencies,
    CreateCurrencyCommandHandler createCurrency,
    UpdateCurrencyCommandHandler updateCurrency) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Currency[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Currency[]>> GetCurrencies()
    {
        return HandleResult(await getCurrencies.Handle(new GetCurrenciesQuery(), HttpContext.RequestAborted));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Unit>> CreateCurrency([FromBody] CurrencyModel model)
    {
        return HandleResult(await createCurrency.Handle(new CreateCurrencyCommand(model), HttpContext.RequestAborted));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Unit>> UpdateCurrency(string id, [FromBody] UpdateCurrencyModel model)
    {
        return HandleResult(await updateCurrency.Handle(new UpdateCurrencyCommand(id, model), HttpContext.RequestAborted));
    }
}
