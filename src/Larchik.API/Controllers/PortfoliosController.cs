using System.Net;
using Larchik.Application.Common.Paging;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.ClearPortfolioData;
using Larchik.Application.Portfolios.CreatePortfolio;
using Larchik.Application.Portfolios.DeletePortfolio;
using Larchik.Application.Portfolios.EditPortfolio;
using Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;
using Larchik.Application.Portfolios.GetAggregatePortfolioSummary;
using Larchik.Application.Portfolios.GetPortfolio;
using Larchik.Application.Portfolios.GetPortfolioPerformance;
using Larchik.Application.Portfolios.GetPortfolios;
using Larchik.Application.Portfolios.GetPortfoliosSummary;
using Larchik.Application.Portfolios.GetPortfolioSummary;
using Larchik.Application.Portfolios.RecalculatePortfolio;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlerts;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationAlertsSummary;
using Larchik.Application.Portfolios.Reconciliation.GetLatestPortfolioReconciliationResult;
using Larchik.Application.Portfolios.Reconciliation.GetPortfolioReconciliationHistory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class PortfoliosController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PortfolioDto>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<PortfolioDto>>> List()
    {
        return HandleResult(await Mediator.Send(new GetPortfoliosQuery(), HttpContext.RequestAborted));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PortfolioDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<PortfolioDto>> Get(Guid id)
    {
        return HandleResult(await Mediator.Send(new GetPortfolioQuery(id), HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Guid>> Create([FromBody] PortfolioModel model)
    {
        return HandleResult(await Mediator.Send(new CreatePortfolioCommand(model), HttpContext.RequestAborted));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Edit(Guid id, [FromBody] PortfolioModel model)
    {
        return HandleResult(await Mediator.Send(new EditPortfolioCommand(id, model), HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Unit), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Unit>> Delete(Guid id)
    {
        return HandleResult(await Mediator.Send(new DeletePortfolioCommand(id), HttpContext.RequestAborted));
    }

    [HttpDelete("{id:guid}/data")]
    [ProducesResponseType(typeof(ClearPortfolioDataResultDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<ClearPortfolioDataResultDto>> ClearData(Guid id)
    {
        return HandleResult(await Mediator.Send(new ClearPortfolioDataCommand(id), HttpContext.RequestAborted));
    }

    [HttpPost("{id:guid}/recalculate")]
    [ProducesResponseType(typeof(RecalculatePortfolioResultDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<RecalculatePortfolioResultDto>> Recalculate(Guid id)
    {
        return HandleResult(await Mediator.Send(new RecalculatePortfolioCommand(id), HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns portfolio summary with selected valuation method: adjustingAvg (default), staticAvg, fifo, lifo.
    /// Security transfers (TransferIn/TransferOut with instrumentId) are quantity-only and do not create realized P&amp;L directly.
    /// TransferIn adds zero-cost quantity. TransferOut reduces quantity without reducing total remaining position cost.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PortfolioSummaryDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<PortfolioSummaryDto>> GetSummary(Guid id, [FromQuery] string? method)
    {
        return HandleResult(await Mediator.Send(new GetPortfolioSummaryQuery(id, method), HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns aggregate summary across all portfolios with selected valuation method: adjustingAvg (default), staticAvg, fifo, lifo.
    /// Security transfers (TransferIn/TransferOut with instrumentId) are quantity-only and do not create realized P&amp;L directly.
    /// TransferIn adds zero-cost quantity. TransferOut reduces quantity without reducing total remaining position cost.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PortfoliosSummaryDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PortfoliosSummaryDto>> GetTotalSummary(
        [FromQuery] string? method,
        [FromQuery] string? currency)
    {
        return HandleResult(await Mediator.Send(new GetPortfoliosSummaryQuery(method, currency), HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns a single combined snapshot across all portfolios with selected valuation method: adjustingAvg (default), staticAvg, fifo, lifo.
    /// Security transfers (TransferIn/TransferOut with instrumentId) are quantity-only and do not create realized P&amp;L directly.
    /// TransferIn adds zero-cost quantity. TransferOut reduces quantity without reducing total remaining position cost.
    /// </summary>
    [HttpGet("aggregate/summary")]
    [ProducesResponseType(typeof(PortfolioSummaryDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PortfolioSummaryDto>> GetAggregateSummary(
        [FromQuery] string? method,
        [FromQuery] string? currency)
    {
        return HandleResult(await Mediator.Send(new GetAggregatePortfolioSummaryQuery(method, currency), HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns monthly performance series with selected valuation method: adjustingAvg (default), staticAvg, fifo, lifo.
    /// Security transfers (TransferIn/TransferOut with instrumentId) are quantity-only and do not create realized P&amp;L directly.
    /// TransferIn adds zero-cost quantity. TransferOut reduces quantity without reducing total remaining position cost.
    /// </summary>
    [HttpGet("{id:guid}/performance")]
    [ProducesResponseType(typeof(IEnumerable<PortfolioPerformanceDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<IEnumerable<PortfolioPerformanceDto>>> GetPerformance(
        Guid id,
        [FromQuery] string? method,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        return HandleResult(await Mediator.Send(
            new GetPortfolioPerformanceQuery(id, method, from, to),
            HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns combined monthly performance series with selected valuation method: adjustingAvg (default), staticAvg, fifo, lifo.
    /// Security transfers (TransferIn/TransferOut with instrumentId) are quantity-only and do not create realized P&amp;L directly.
    /// TransferIn adds zero-cost quantity. TransferOut reduces quantity without reducing total remaining position cost.
    /// </summary>
    [HttpGet("aggregate/performance")]
    [ProducesResponseType(typeof(IEnumerable<PortfolioPerformanceDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<IEnumerable<PortfolioPerformanceDto>>> GetAggregatePerformance(
        [FromQuery] string? method,
        [FromQuery] string? currency,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        return HandleResult(await Mediator.Send(
            new GetAggregatePortfolioPerformanceQuery(method, currency, from, to),
            HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns paged reconciliation history.
    /// Example: /api/portfolios/reconciliation?from=2026-04-01&amp;to=2026-04-30&amp;status=mismatch&amp;severity=critical&amp;alertRequired=true&amp;sortBy=createdAt&amp;sortDirection=desc&amp;page=1&amp;pageSize=50
    /// sortBy: statementDate (default), createdAt, severity, status, navDelta.
    /// sortDirection: asc or desc (default).
    /// </summary>
    [HttpGet("reconciliation")]
    [ProducesResponseType(typeof(PagedResult<PortfolioReconciliationResultDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PagedResult<PortfolioReconciliationResultDto>>> GetReconciliationHistory(
        [FromQuery] Guid? portfolioId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] bool? alertRequired = null,
        [FromQuery] string? source = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] PageQuery? paging = null)
    {
        return HandleResult(await Mediator.Send(
            new GetPortfolioReconciliationHistoryQuery(
                portfolioId,
                from,
                to,
                status,
                severity,
                alertRequired,
                source,
                sortBy,
                sortDirection,
                paging),
            HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns paged open reconciliation alerts (alertRequired = true).
    /// Example: /api/portfolios/reconciliation/alerts?severity=critical&amp;from=2026-04-01&amp;to=2026-04-30&amp;page=1&amp;pageSize=50
    /// </summary>
    [HttpGet("reconciliation/alerts")]
    [ProducesResponseType(typeof(PagedResult<PortfolioReconciliationResultDto>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<PagedResult<PortfolioReconciliationResultDto>>> GetReconciliationAlerts(
        [FromQuery] Guid? portfolioId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? source = null,
        [FromQuery] PageQuery? paging = null)
    {
        return HandleResult(await Mediator.Send(
            new GetPortfolioReconciliationAlertsQuery(portfolioId, from, to, severity, source, paging),
            HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns reconciliation alert aggregates and latest critical alert by portfolio.
    /// Example: /api/portfolios/reconciliation/alerts/summary?from=2026-04-01&amp;to=2026-04-30
    /// </summary>
    [HttpGet("reconciliation/alerts/summary")]
    [ProducesResponseType(typeof(PortfolioReconciliationAlertsSummaryDto), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<PortfolioReconciliationAlertsSummaryDto>> GetReconciliationAlertsSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? source = null)
    {
        return HandleResult(await Mediator.Send(
            new GetPortfolioReconciliationAlertsSummaryQuery(from, to, source),
            HttpContext.RequestAborted));
    }

    /// <summary>
    /// Returns the latest reconciliation result for a portfolio.
    /// Example: /api/portfolios/{id}/reconciliation/latest?source=reconciliation.daily
    /// </summary>
    [HttpGet("{id:guid}/reconciliation/latest")]
    [ProducesResponseType(typeof(PortfolioReconciliationResultDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PortfolioReconciliationResultDto>> GetLatestReconciliationResult(
        Guid id,
        [FromQuery] string? source = null)
    {
        return HandleResult(await Mediator.Send(
            new GetLatestPortfolioReconciliationResultQuery(id, source),
            HttpContext.RequestAborted));
    }
}
