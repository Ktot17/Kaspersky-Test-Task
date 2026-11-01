using BLComponent.Enums;
using BLComponent.Abstractions;
using BLComponent.Models;

namespace BLComponent.StateMachine;

public sealed class ScanStateMachine(
    IScanRepository scanRepository)
    : IScanStateMachine
{
    public Task<bool> TryMarkRunningAsync(Guid id, DateTimeOffset at, CancellationToken ct)
        => TryTransitionAsync(id, ScanStatus.Running, result: null, error: null, at, ct);

    public Task<bool> TryMarkCompletedAsync(Guid id, ScanResult result, DateTimeOffset at, CancellationToken ct)
        => TryTransitionAsync(id, ScanStatus.Completed, result, error: null, at, ct);

    public Task<bool> TryMarkFailedAsync(Guid id, string error, DateTimeOffset at, CancellationToken ct)
        => TryTransitionAsync(id, ScanStatus.Failed, result: null, error, at, ct);

    public Task<bool> TryMarkCancelledAsync(Guid id, DateTimeOffset at, CancellationToken ct)
        => TryTransitionAsync(id, ScanStatus.Cancelled, result: null, error: null, at, ct);
    
    private async Task<bool> TryTransitionAsync(
        Guid id,
        ScanStatus to,
        ScanResult? result,
        string? error,
        DateTimeOffset at,
        CancellationToken ct)
    {
        while (true)
        {
            var current = await scanRepository.GetAsync(id, ct);
            if (current is null || !IsAllowed(current.Status, to))
                return false;

            var updated = current with
            {
                Status = to,
                LastUpdatedAt = at,
                Result = result,
                ErrorMessage = error
            };

            if (await scanRepository.TryUpdateAsync(updated, current, ct))
                return true;
        }
    }

    private static bool IsAllowed(ScanStatus from, ScanStatus to) => (from, to) switch
    {
        (ScanStatus.Queued, ScanStatus.Running) => true,
        (ScanStatus.Running, ScanStatus.Completed) => true,
        (ScanStatus.Running, ScanStatus.Failed) => true,
        (ScanStatus.Queued, ScanStatus.Cancelled) => true,
        (ScanStatus.Running, ScanStatus.Cancelled) => true,
        _ => false
    };
}
