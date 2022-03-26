using System.Net;
using Larchik.Application.Deals;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class DealsController : BaseApiController
{
    [Authorize(Policy = "IsAccountOwner")]
    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> Create(Guid id, DealDto deal)
    {
        var result = await Mediator.Send(new Create.Command { AccountId = id, Deal = deal});

        return Ok(result);
    }
    
    [Authorize(Policy = "IsAccountOwner")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> Edit(Guid id, DealDto deal)
    {
        var result = await Mediator.Send(new Edit.Command { AccountId = id, Deal = deal});

        return Ok(result);
    }
    
    [Authorize(Policy = "IsDealOwner")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> Delete(Guid id)
    {
        var result = await Mediator.Send(new Delete.Command { Id = id});

        return Ok(result);
    }
    
    [Authorize(Policy = "IsAccountOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<List<DealDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> AccountDeals(Guid id)
    {
        var result = await Mediator.Send(new List.Query { Id = id});

        return Ok(result);
    }
}