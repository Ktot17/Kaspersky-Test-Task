using BLComponent.Enums;
using BLComponent.Abstractions;
using Microsoft.AspNetCore.Mvc;
using WebComponent.Models;

namespace WebComponent.Controllers;

[ApiController]
[Route("api/v1/scans")]
public sealed class ScanController(
    IScanManager scanManager) 
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(StartScanResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StartScanResponse>> StartScan(
        StartScanRequest request,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(request.RepositoryUrl, UriKind.Absolute, out var repositoryUrl) ||
            (repositoryUrl.Scheme != Uri.UriSchemeHttp && repositoryUrl.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid repository URL",
                Detail = "RepositoryUrl must be an absolute http or https URL."
            });
        
        var id = await scanManager.StartScanAsync(repositoryUrl, ct);
        
        return AcceptedAtAction(
            nameof(GetScan),
            new { id },
            new StartScanResponse(id));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetScanResponse>> GetScan(
        Guid id,
        CancellationToken ct)
    {
        var scan = await scanManager.GetScanAsync(id, ct);

        if (scan is null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Scan not found",
                Detail = $"No scan with id '{id}'."
            });
        
        return Ok(new GetScanResponse(
            scan.Id,
            scan.Status,
            scan.LastUpdatedAt,
            scan.Result,
            scan.ErrorMessage));
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancelScanResponse>> CancelScan(
        Guid id,
        CancellationToken ct)
    {
        var outcome = await scanManager.CancelScanAsync(id, ct);
        
        return outcome.Outcome switch
        {
            CancelScanOutcome.NotFound =>
                NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Scan not found",
                    Detail = $"No scan with id '{id}'."
                }),

            CancelScanOutcome.AlreadyTerminal =>
                Ok(new CancelScanResponse(
                    outcome.Scan!.Id,
                    Success: false,
                    CancelledAt: null,
                    Reason: $"Scan is already in terminal state '{outcome.Scan.Status}'.")),

            CancelScanOutcome.Cancelled =>
                Ok(new CancelScanResponse(
                    outcome.Scan!.Id,
                    Success: true,
                    CancelledAt: outcome.Scan.LastUpdatedAt,
                    Reason: null)),

            _ => throw new InvalidOperationException(
                $"Unexpected cancel outcome: {outcome.Outcome}.")
        };
    }
}
