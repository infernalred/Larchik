using System.Net;
using Larchik.Application.Helpers;
using Larchik.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class ReportsController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(OperationResult<CurrencyOperationsReportModel>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<OperationResult<CurrencyOperationsReportModel>>> CurrencyOperations(Guid id, DateTime fromDate, DateTime toDate)
    {
        return Ok(await Mediator.Send(new CurrencyOperationsReport.Query{AccountId = id, FromDate = fromDate, ToDate = toDate}));
    }
}