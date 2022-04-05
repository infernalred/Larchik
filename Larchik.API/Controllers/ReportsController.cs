using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class ReportsController : BaseApiController
{
    [Authorize(Policy = "IsAccountOwner")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActionResult<FileContentResult>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<FileContentResult> CurrencyOperations(Guid id, [FromQuery]ReportParams param)
    {
        var result = await Mediator.Send(new CurrencyOperationsReport.Query {AccountId = id, Params = param});

        return new FileContentResult(result.Result.FileData, result.Result.MimeType)
        {
            FileDownloadName = result.Result.FileName
        };
    }
}