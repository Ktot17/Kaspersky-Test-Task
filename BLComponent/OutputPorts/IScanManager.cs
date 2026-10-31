using BLComponent.Models;

namespace BLComponent.OutputPorts;

public interface IScanManager
{
    public Task<Guid> StartScanAsync(
        Uri repositoryUrl,
        CancellationToken ct);
    
    public Task<Scan?> GetScanAsync(
        Guid id,
        CancellationToken ct);
    
    public Task<CancelScanResult> CancelScanAsync(
        Guid id,
        CancellationToken ct);
}
