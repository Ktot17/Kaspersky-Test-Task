using System.Collections.Concurrent;
using DBComponent.Registries;

namespace DBComponent.Tests;

public class RunningScanRegistryTests
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _scans = new();
    private readonly RunningScanRegistry _registry;
    
    public RunningScanRegistryTests()
        => _registry = new RunningScanRegistry(_scans);

    [Fact]
    public void Register_NewScan_DoesNotThrow()
    {
        var cts = new CancellationTokenSource();
        var id = Guid.NewGuid();
        
        _registry.Register(id, cts);
        
        Assert.Single(_scans);
        Assert.Contains(id, _scans.Keys);
        Assert.Same(cts, _scans[id]);
    }

    [Fact]
    public void Register_DuplicateId_ThrowsInvalidOperationException()
    {
        var existing = new CancellationTokenSource();
        var duplicate = new CancellationTokenSource();
        var id = Guid.NewGuid();
        _scans[id] = existing;

        Assert.Throws<InvalidOperationException>(() => _registry.Register(id, duplicate));

        Assert.Single(_scans);
        Assert.Same(existing, _scans[id]);
        Assert.Throws<ObjectDisposedException>(() => _ = duplicate.Token);
    }

    [Fact]
    public void TryCancel_RegisteredScan_ReturnsTrueAndCancelsToken()
    {
        var cts = new CancellationTokenSource();
        var id = Guid.NewGuid();
        _scans[id] = cts;

        var result = _registry.TryCancel(id);
        
        Assert.True(result);
        Assert.Single(_scans);
        Assert.Contains(id, _scans.Keys);
        Assert.True(_scans[id].IsCancellationRequested);
    }

    [Fact]
    public void TryCancel_MissingScan_ReturnsFalse()
    {
        var result = _registry.TryCancel(Guid.NewGuid());
        
        Assert.False(result);
        Assert.Empty(_scans);
    }

    [Fact]
    public void TryCancel_AfterCtsDisposed_ReturnsFalse()
    {
        var cts = new CancellationTokenSource();
        var id = Guid.NewGuid();
        _scans[id] = cts;
        cts.Dispose();
        
        var result = _registry.TryCancel(id);
        
        Assert.False(result);
        Assert.Single(_scans);
        Assert.Contains(id, _scans.Keys);
    }

    [Fact]
    public void Unregister_RegisteredScan_RemovesEntry()
    {
        var cts = new CancellationTokenSource();
        var id = Guid.NewGuid();
        _scans[id] = cts;
        
        _registry.Unregister(id);
        
        Assert.Empty(_scans);
        Assert.Throws<ObjectDisposedException>(cts.Cancel);
    }

    [Fact]
    public void Unregister_MissingScan_DoesNotThrow()
    {
        _registry.Unregister(Guid.NewGuid());
        
        Assert.Empty(_scans);
    }
}
