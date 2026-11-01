using System.Collections.Concurrent;
using BLComponent.Abstractions;
using BLComponent.Models;

namespace DBComponent.Repositories;

public sealed class ScanRepository : IScanRepository
{
    private readonly ConcurrentDictionary<Guid, Scan> _scans;

    internal ScanRepository(
        ConcurrentDictionary<Guid, Scan> scans) 
        => _scans = scans;
    
    public ScanRepository() 
        : this(new ConcurrentDictionary<Guid, Scan>()) {}
    
    public Task AddAsync(
        Scan scan,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scan);
        
        return !_scans.TryAdd(scan.Id, scan)
            ? throw new InvalidOperationException("Scan already exists")
            : Task.CompletedTask;
    }

    public Task<Scan?> GetAsync(
        Guid id,
        CancellationToken ct)
    {
        _scans.TryGetValue(id, out var scan);
        return Task.FromResult(scan);
    }

    public Task<bool> TryUpdateAsync(
        Scan updated,
        Scan expected,
        CancellationToken ct)
        => Task.FromResult(_scans.TryUpdate(expected.Id, updated, expected));
}
