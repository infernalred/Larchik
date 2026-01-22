using System.Net;
using Larchik.Application.Portfolios.GetPortfolioSummary;
using Larchik.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PortfoliosController : BaseApiController
{
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PortfolioSummaryDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<PortfolioSummaryDto>> GetSummary(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetPortfolioSummaryQuery(id), HttpContext.RequestAborted));
    }
}
