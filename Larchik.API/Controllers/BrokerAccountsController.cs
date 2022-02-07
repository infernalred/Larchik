using System.Net;
using Larchik.Application.BrokerAccounts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class BrokerAccountsController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<List<BrokerAccountDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<List<BrokerAccountDto>>>> GetAccounts()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
    
    [HttpGet("{id:guid}", Name = nameof(GetAccount))]
    [ProducesResponseType(typeof(OperationResult<BrokerDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(OperationResult<BrokerDto>), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<Account>>> GetAccount(Guid id)
    {
        var result = await Mediator.Send(new Details.Query { Id = id });
        
        if (result.Result == null) return NotFound();
        
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<Unit>>> CreateAccount(BrokerAccountCreateDto account)
    {
        var result = await Mediator.Send(new Create.Command { Account = account });

        return CreatedAtRoute(nameof(GetAccount), new {id = account.Id}, result);
    }
}