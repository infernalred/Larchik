using System.Net;
using Larchik.Application.Common.Paging;
using Larchik.Application.Models;
using Larchik.Application.Stocks.CreateStock;
using Larchik.Application.Stocks.EditStock;
using Larchik.Application.Stocks.GetAdminInstruments;
using Larchik.Application.Stocks.GetInstrument;
using Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.DeleteInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;
using Larchik.Application.Stocks.InstrumentCorporateActions.GetInstrumentCorporateActions;
using Larchik.Application.Stocks.SearchInstruments;
using Larchik.Persistence.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class InstrumentsController : BaseApiController
{
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(PagedResult<InstrumentDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<PagedResult<InstrumentDto>>> ListAdmin(
        [FromQuery] string? query,
        [FromQuery] string? country,
        [FromQuery] PageQuery paging)
    {
        return HandleResult(await Mediator.Send(new GetAdminInstrumentsQuery(query, country, paging), HttpContext.RequestAborted));
    }

    [HttpGet]
    [ProducesResponseType(typeof(InstrumentLookupDto[]), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<InstrumentLookupDto[]>> Search([FromQuery] string? query, [FromQuery] int limit = 20)
    {
        return HandleResult(await Mediator.Send(new SearchInstrumentsQuery(query, limit), HttpContext.RequestAborted));
    }

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

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpGet("{id:guid}/corporate-actions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<InstrumentCorporateActionDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<InstrumentCorporateActionDto>>> ListCorporateActions(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetInstrumentCorporateActionsQuery(id), HttpContext.RequestAborted));
    }

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("{id:guid}/corporate-actions")]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<Guid>> CreateCorporateAction(Guid id, [FromBody] InstrumentCorporateActionModel model)
    {
        return HandleResult(await Mediator.Send(new CreateInstrumentCorporateActionCommand(id, model), HttpContext.RequestAborted));
    }

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPut("{id:guid}/corporate-actions/{actionId:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> EditCorporateAction(Guid id, Guid actionId, [FromBody] InstrumentCorporateActionModel model)
    {
        return HandleResult(await Mediator.Send(new EditInstrumentCorporateActionCommand(id, actionId, model), HttpContext.RequestAborted));
    }

    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpDelete("{id:guid}/corporate-actions/{actionId:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> DeleteCorporateAction(Guid id, Guid actionId)
    {
        return HandleResult(await Mediator.Send(new DeleteInstrumentCorporateActionCommand(id, actionId), HttpContext.RequestAborted));
    }
}
