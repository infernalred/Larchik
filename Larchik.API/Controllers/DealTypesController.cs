using System.Net;
using Larchik.Application.DealTypes;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class DealTypesController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Result<List<DealType>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Result<List<DealType>>>> GetDealTypes()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
}