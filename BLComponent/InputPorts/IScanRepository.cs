using BLComponent.Models;

namespace BLComponent.InputPorts;

public interface IScanRepository
{
    public Task AddAsync(
        Scan scan,
        CancellationToken ct);
    
    public Task<Scan?> GetAsync(
        Guid id,
        CancellationToken ct);
    
    public Task<bool> TryUpdateAsync(
        Scan updated,
        Scan expected,
        CancellationToken ct);
}
