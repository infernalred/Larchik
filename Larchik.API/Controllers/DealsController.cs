﻿using System.Net;
using Larchik.Application.Deals;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class DealsController : BaseApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> Create(DealDto deal)
    {
        var result = await Mediator.Send(new Create.Command { Deal = deal});

        return Ok(result);
    }
    
    [Authorize(Policy = "IsDealOwner")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> Edit(Guid id, DealDto deal)
    {
        deal.Id = id;
        var result = await Mediator.Send(new Edit.Command { Deal = deal});

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
    [HttpGet("accounts/{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<List<DealDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> AccountDeals(Guid id)
    {
        var result = await Mediator.Send(new List.Query { Id = id});

        return Ok(result);
    }
    
    [Authorize(Policy = "IsDealOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<DealDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> GetDeal(Guid id)
    {
        var result = await Mediator.Send(new Details.Query { Id = id});

        return Ok(result);
    }
}