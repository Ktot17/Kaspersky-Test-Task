using BLComponent.Models;

namespace BLComponent.InputPorts;

public interface IScanner
{
    public Task<ScanResult> ScanAsync(
        Uri repositoryUrl,
        CancellationToken ct);
}
