using System.Net;
using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Application.Stocks.EditStock;
using Larchik.Application.Stocks.GetInstrument;
using Larchik.Persistence.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class InstrumentsController : BaseApiController
{
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Unit>> CreateInstrument([FromBody] InstrumentModel model)
    {
        return HandleResult(await Mediator.Send(new CreateInstrumentCommand(model), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<InstrumentDto>> GetInstrument(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetInstrumentQuery(id), HttpContext.RequestAborted));
    }

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> EditInstrument(Guid id, [FromBody] InstrumentModel model)
    {
        return HandleResult(await Mediator.Send(new EditInstrumentCommand(id, model), HttpContext.RequestAborted));
    }
}
