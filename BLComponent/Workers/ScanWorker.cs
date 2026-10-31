using BLComponent.Exceptions;
using BLComponent.InputPorts;
using BLComponent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BLComponent.Workers;

public sealed class ScanWorker(
    IScanQueue queue,
    IScanRepository repository,
    IScanStateMachine stateMachine,
    IScanner scanner,
    IRunningScanRegistry registry,
    TimeProvider clock,
    ILogger<ScanWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var scanId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessScanAsync(scanId, stoppingToken);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogError(
                        ex, "Coordination error while processing scan {ScanId}", scanId);
                }
            }
        }
        catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Scan worker stopped");
        }
    }

    private async Task ProcessScanAsync(Guid scanId, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        registry.Register(scanId, cts);

        try
        {
            if (!await stateMachine.TryMarkRunningAsync(
                    scanId, clock.GetUtcNow(), stoppingToken))
            {
                logger.LogDebug(
                    "Scan {ScanId} skipped: already in terminal state", scanId);
                return;
            }

            var scan = await repository.GetAsync(scanId, CancellationToken.None);
            if (scan is null)
            {
                logger.LogWarning(
                    "Scan {ScanId} not found after marking running", scanId);
                return;
            }

            ScanResult result;
            try
            {
                result = await scanner.ScanAsync(scan.RepositoryUrl, cts.Token);
            }
            catch (ScanFailedException ex)
            {
                logger.LogError(ex, "Scan {ScanId} failed in scanner", scanId);
                await stateMachine.TryMarkFailedAsync(
                    scanId, ex.Message, clock.GetUtcNow(), CancellationToken.None);
                return;
            }

            if (!await stateMachine.TryMarkCompletedAsync(
                    scanId, result, clock.GetUtcNow(), CancellationToken.None))
            {
                logger.LogInformation(
                    "Scan {ScanId} completed but was already cancelled", scanId);
            }
        }
        catch (OperationCanceledException ex) when (cts.Token.IsCancellationRequested)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation(ex, "Scan {ScanId} interrupted by shutdown", scanId);
                await stateMachine.TryMarkFailedAsync(
                    scanId,
                    "Service is shutting down.",
                    clock.GetUtcNow(),
                    CancellationToken.None);
            }
            else
            {
                logger.LogInformation(ex, "Scan {ScanId} cancelled by user", scanId);
            }
        }
        finally
        {
            registry.Unregister(scanId);
        }
    }
}
