using BLComponent.Enums;
using BLComponent.Exceptions;
using BLComponent.Abstractions;
using BLComponent.Models;
using Microsoft.Extensions.Logging;

namespace BLComponent.Managers;

public sealed class ScanManager(
    IScanRepository scanRepository,
    IScanStateMachine scanStateMachine,
    IScanQueue scanQueue,
    IRunningScanRegistry registry,
    TimeProvider clock,
    ILogger<ScanManager> logger)
    : IScanManager
{
    public async Task<Guid> StartScanAsync(
        Uri repositoryUrl,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var scan = new Scan(
            Guid.NewGuid(),
            repositoryUrl,
            ScanStatus.Queued,
            now,
            now,
            null,
            null);
        
        await scanRepository.AddAsync(scan, ct);

        if (scanQueue.TryPush(scan.Id))
        {
            logger.LogInformation("Scan {ScanId} queued for {RepositoryUrl}", scan.Id, repositoryUrl);
            return scan.Id;
        }

        logger.LogWarning("Scan queue unavailable for scan {ScanId}", scan.Id);
        var ex = new ScanQueueUnavailableException();
        
        await scanStateMachine.TryMarkFailedAsync(
            scan.Id,
            ex.Message,
            clock.GetUtcNow(),
            CancellationToken.None);

        throw ex;
    }

    public Task<Scan?> GetScanAsync(
        Guid id,
        CancellationToken ct) 
        => scanRepository.GetAsync(id, ct);

    public async Task<CancelScanResult> CancelScanAsync(
        Guid id,
        CancellationToken ct)
    {
        var scan = await scanRepository.GetAsync(id, ct);
        if (scan is null)
        {
            logger.LogInformation("Cancel requested for unknown scan {ScanId}", id);
            return new CancelScanResult(CancelScanOutcome.NotFound, null);
        }

        var moved = await scanStateMachine.TryMarkCancelledAsync(
            id,
            clock.GetUtcNow(),
            CancellationToken.None);

        if (!moved)
        {
            logger.LogInformation("Cancel requested for scan {ScanId} in terminal state {Status}", id, scan.Status);
            var current = await scanRepository.GetAsync(id, ct);
            return new CancelScanResult(CancelScanOutcome.AlreadyTerminal, current);
        }
        
        registry.TryCancel(id);
        
        logger.LogInformation("Scan {ScanId} cancelled by user", id);
        
        var updated = await scanRepository.GetAsync(id, ct);
        return new CancelScanResult(CancelScanOutcome.Cancelled, updated);
    }
}
