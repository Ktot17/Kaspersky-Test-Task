using System.Collections.Concurrent;
using AutoFixture.Xunit2;
using BLComponent.Enums;
using BLComponent.Models;
using DBComponent.Repositories;

namespace DBComponent.Tests;

public class ScanRepositoryTests
{
    private readonly ConcurrentDictionary<Guid, Scan> _scans = new();
    private readonly ScanRepository _repository;
    
    public ScanRepositoryTests() 
        => _repository = new ScanRepository(_scans);

    [Theory, AutoData]
    public async Task AddAsync_NewScan_DoesNotThrow(Scan scan)
    {
        await _repository.AddAsync(scan, CancellationToken.None);
        
        Assert.Single(_scans);
        Assert.Contains(scan.Id, _scans.Keys);
        Assert.Equivalent(scan, _scans[scan.Id]);
    }

    [Fact]
    public async Task AddAsync_NullScan_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _repository.AddAsync(null!, CancellationToken.None));
    }

    [Theory, AutoData]
    public async Task AddAsync_DuplicateId_ThrowsInvalidOperationException(Scan scan)
    {
        _scans.TryAdd(scan.Id, scan);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.AddAsync(scan, CancellationToken.None));
        Assert.Single(_scans);
        Assert.Equivalent(scan, _scans[scan.Id]);
    }

    [Theory, AutoData]
    public async Task GetAsync_ExistingId_ReturnsSameRecord(Scan scan)
    {
        _scans.TryAdd(scan.Id, scan);
        
        var result = await _repository.GetAsync(scan.Id, CancellationToken.None);
        
        Assert.NotNull(result);
        Assert.Equivalent(scan, result);
    }

    [Fact]
    public async Task GetAsync_MissingId_ReturnsNull()
    {
        var result = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);
        
        Assert.Null(result);
    }

    [Theory, AutoData]
    public async Task TryUpdateAsync_ExpectedMatchesStored_ReturnsTrue(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Queued };
        _scans.TryAdd(scan.Id, scan);
        var newScan = scan with { Status = ScanStatus.Running };
        
        var result = await _repository.TryUpdateAsync(newScan, scan, CancellationToken.None);
        
        Assert.True(result);
        Assert.Equivalent(newScan, _scans[scan.Id]);
    }

    [Theory, AutoData]
    public async Task TryUpdateAsync_ExpectedDiffersFromStored_ReturnsFalse(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Queued };
        _scans.TryAdd(scan.Id, scan);
        var newScan = scan with { Status = ScanStatus.Running };
        var expectedScan = scan with { Status = ScanStatus.Completed };
        
        var result = await _repository.TryUpdateAsync(newScan, expectedScan, CancellationToken.None);
        
        Assert.False(result);
        Assert.Equivalent(scan, _scans[scan.Id]);
    }

    [Theory, AutoData]
    public async Task TryUpdateAsync_MissingId_ReturnsFalse(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Queued };
        var newScan = scan with { Status = ScanStatus.Running };
        
        var result = await _repository.TryUpdateAsync(newScan, scan, CancellationToken.None);
        
        Assert.False(result);
        Assert.Empty(_scans);
    }
}
