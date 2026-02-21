using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Prices.SyncPrices;
using Larchik.Persistence.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PricesController : BaseApiController
{
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<int>> Sync([FromBody] IReadOnlyCollection<PriceModel> prices)
    {
        return HandleResult(await Mediator.Send(new SyncPricesCommand(prices), HttpContext.RequestAborted));
    }
}
