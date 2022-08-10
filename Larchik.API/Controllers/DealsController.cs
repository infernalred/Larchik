using System.Net;
using Larchik.API.Configuration;
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
    [ProducesResponseType(typeof(OperationResult<PagedList<DealDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<PagedList<DealDto>>>> GetAccountDeals(Guid id, [FromQuery]DealParams param)
    {
        var result = await Mediator.Send(new List.Query { Id = id, Params = param });
        
        Response.AddPaginationHeader(result.Result.CurrentPage, result.Result.PageSize, result.Result.TotalCount, result.Result.TotalPages);

        return Ok(result);
    }
    
    [Authorize(Policy = "IsDealOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<DealDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<DealDto>>> GetDeal(Guid id)
    {
        var result = await Mediator.Send(new Details.Query { Id = id});

        return Ok(result);
    }
}