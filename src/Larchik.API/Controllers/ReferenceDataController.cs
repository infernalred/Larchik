using System.Net;
using Larchik.Application.Models;
using Larchik.Application.ReferenceData.GetCountries;
using Larchik.Application.ReferenceData.GetExchanges;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public sealed class ReferenceDataController : BaseApiController
{
    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ReferenceItemDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ReferenceItemDto>>> GetCountries()
    {
        return HandleResult(await Mediator.Send(new GetCountriesQuery(), HttpContext.RequestAborted));
    }

    [HttpGet("exchanges")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ReferenceItemDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ReferenceItemDto>>> GetExchanges()
    {
        return HandleResult(await Mediator.Send(new GetExchangesQuery(), HttpContext.RequestAborted));
    }
}
