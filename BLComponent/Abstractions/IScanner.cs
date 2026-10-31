using BLComponent.Models;

namespace BLComponent.Abstractions;

public interface IScanner
{
    public Task<ScanResult> ScanAsync(
        Uri repositoryUrl,
        CancellationToken ct);
}
