using BLComponent.InputPorts;
using BLComponent.Models;
using Microsoft.Extensions.Logging;

namespace BLComponent.Scanner;

public sealed class StubScanner(ILogger<StubScanner> logger, int delay = 3000) : IScanner
{
    private int DelayMilliseconds { get; } = delay;
    
    public async Task<ScanResult> ScanAsync(
        Uri repositoryUrl,
        CancellationToken ct)
    {
        logger.LogInformation("Scan started");
        await Task.Delay(DelayMilliseconds, ct);
        logger.LogInformation("Scan completed");

        return new ScanResult([]);
    }
}
