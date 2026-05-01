using System.Net;
using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Persistence.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class FxRatesController(SyncCbrFxRatesCommandHandler syncCbr) : BaseApiController
{
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("sync/cbr")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<int>> SyncCbr([FromQuery] DateOnly? date)
    {
        var result = await syncCbr.Handle(new SyncCbrFxRatesCommand(date), HttpContext.RequestAborted);
        return HandleResult(result);
    }
}
