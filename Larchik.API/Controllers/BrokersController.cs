using System.Net;
using Larchik.Application.Brokers;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class BrokersController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<List<BrokerDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<List<BrokerDto>>>> GetBrokers()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
    
    [HttpGet("{id:int}", Name = nameof(GetBroker))]
    [ProducesResponseType(typeof(OperationResult<BrokerDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(OperationResult<BrokerDto>), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<BrokerDto>>> GetBroker(int id)
    {
        var result = await Mediator.Send(new Details.Query { Id = id });

        if (result.Result == null) return NotFound();
        
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OperationResult<BrokerDto>), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    public async Task<ActionResult<OperationResult<BrokerDto>>> CreateBroker(BrokerDto broker)
    {
        var result = await Mediator.Send(new Create.Command { Broker = broker });

        if (result == null) return Conflict();

        return CreatedAtRoute(nameof(GetBroker), new {id = result.Result.Id}, result);
    }
}