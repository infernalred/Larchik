using System.Net;
using Larchik.Application.MarketDataImports;
using Larchik.Application.MarketDataImports.GetMarketDataImport;
using Larchik.Application.MarketDataImports.QueueMarketDataImport;
using Larchik.Persistence.Constants;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("api/market-data/imports")]
public sealed class MarketDataImportsController(
    QueueMarketDataImportCommandHandler queueImport,
    GetMarketDataImportQueryHandler getImport) : BaseApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(MarketDataImportDto), (int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(MarketDataImportDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<MarketDataImportDto>> Queue(
        [FromBody] MarketDataImportModel model,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var result = await queueImport.Handle(
            new QueueMarketDataImportCommand(model.Source, model.Isin, model.FromDate, idempotencyKey),
            HttpContext.RequestAborted);

        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(result.Error);
        }

        return result.Value.Status == MarketDataImportStatus.SkippedExisting
            ? Ok(result.Value)
            : AcceptedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MarketDataImportDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<MarketDataImportDto>> Get(Guid id) =>
        HandleResult(await getImport.Handle(new GetMarketDataImportQuery(id), HttpContext.RequestAborted));
}
