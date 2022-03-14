using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Operations;
using Larchik.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class OperationsController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<List<Operation>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<List<Operation>>>> GetOperations()
    {
        return Ok(await Mediator.Send(new List.Query()));
    }
}