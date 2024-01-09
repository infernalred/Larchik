using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Portfolios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PortfolioController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Result<Portfolio>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Result<Portfolio>>> GetPortfolio()
    {
        return Ok(await Mediator.Send(new Details.Query()));
    }

    [Authorize(Policy = "IsAccountOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<Portfolio>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Result<Portfolio>>> GetAccountPortfolio(Guid id)
    {
        return Ok(await Mediator.Send(new DetailsAccount.Query {Id = id}));
    }
}