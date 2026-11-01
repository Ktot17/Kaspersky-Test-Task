using System.Collections.Concurrent;
using BLComponent.Abstractions;

namespace DBComponent.Registries;

public sealed class RunningScanRegistry : IRunningScanRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _scans;

    internal RunningScanRegistry(
        ConcurrentDictionary<Guid, CancellationTokenSource> scans)
        => _scans = scans;
    
    public RunningScanRegistry() 
        : this(new ConcurrentDictionary<Guid, CancellationTokenSource>()) {}

    public void Register(Guid scanId, CancellationTokenSource cts)
    {
        if (_scans.TryAdd(scanId, cts))
            return;
        cts.Dispose();
        throw new InvalidOperationException(
            $"Scan '{scanId}' is already registered.");
    }
    
    public bool TryCancel(Guid scanId)
    {
        if (!_scans.TryGetValue(scanId, out var cts))
            return false;

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
    
    public void Unregister(Guid scanId)
    {
        if (_scans.TryRemove(scanId, out var cts))
            cts.Dispose();
    }
}
