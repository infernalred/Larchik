using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Portfolios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PortfolioController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<Portfolio>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<Portfolio>>> GetPortfolio()
    {
        return Ok(await Mediator.Send(new Details.Query()));
    }

    [Authorize(Policy = "IsAccountOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Portfolio>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Portfolio>>> GetAccountPortfolio(Guid id)
    {
        return Ok(await Mediator.Send(new DetailsAccount.Query {Id = id}));
    }
}