using BLComponent.Models;

namespace BLComponent.InputPorts;

public interface IScanStateMachine
{
    public Task<bool> TryMarkRunningAsync(
        Guid id,
        DateTimeOffset at,
        CancellationToken ct);
    
    public Task<bool> TryMarkCompletedAsync(
        Guid id,
        ScanResult result,
        DateTimeOffset at,
        CancellationToken ct);
    
    public Task<bool> TryMarkFailedAsync(
        Guid id,
        string error,
        DateTimeOffset at,
        CancellationToken ct);
    
    public Task<bool> TryMarkCancelledAsync(
        Guid id,
        DateTimeOffset at,
        CancellationToken ct);
}
