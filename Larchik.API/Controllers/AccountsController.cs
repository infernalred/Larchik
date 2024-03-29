﻿using System.Net;
using Larchik.Application.Accounts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class AccountsController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<List<AccountDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<List<AccountDto>>>> GetAccounts()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
    
    [Authorize(Policy = "IsAccountOwner")]
    [HttpGet("{id:guid}", Name = nameof(GetAccount))]
    [ProducesResponseType(typeof(OperationResult<AccountDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Account>>> GetAccount(Guid id)
    {
        var result = await Mediator.Send(new Details.Query { Id = id });
        
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<Unit>>> CreateAccount(AccountCreateDto account)
    {
        var result = await Mediator.Send(new Create.Command { Account = account });

        return CreatedAtRoute(nameof(GetAccount), new {id = account.Id}, result);
    }
    
    [Authorize(Policy = "IsAccountOwner")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OperationResult<Unit>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<OperationResult<Unit>>> UpdateAccount(Guid id, AccountCreateDto account)
    {
        account.Id = id;
        var result = await Mediator.Send(new Edit.Command { Account = account });
        
        return Ok(result);
    }
}