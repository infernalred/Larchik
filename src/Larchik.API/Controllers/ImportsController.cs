using System.Net;
using Larchik.API.DTOs;
using Larchik.Application.Models;
using Larchik.Application.Operations.ImportBroker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

[Authorize]
[Route("api/portfolios/{portfolioId:guid}/imports")]
public class ImportsController : BaseApiController
{
    [HttpPost("{brokerCode}")]
    [ProducesResponseType(typeof(ImportResultDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<ImportResultDto>> Import(
        Guid portfolioId,
        string brokerCode,
        [FromForm] ImportBrokerReportRequest request)
    {
        var file = request.File!;

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, HttpContext.RequestAborted);
        ms.Position = 0;

        var result = await Mediator.Send(
            new ImportBrokerReportCommand(portfolioId, brokerCode, ms, file.FileName),
            HttpContext.RequestAborted);

        return HandleResult(result);
    }
}
