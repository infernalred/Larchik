using System.Net;
using Larchik.Application.Brokers.GetBrokers;
using Larchik.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class BrokersController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(BrokerDto[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<BrokerDto[]>> GetBrokers()
    {
        return HandleResult(await Mediator.Send(new GetBrokersQuery(), HttpContext.RequestAborted));
    }
}
