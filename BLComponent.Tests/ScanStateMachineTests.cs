using AutoFixture.Xunit2;
using BLComponent.Abstractions;
using BLComponent.Enums;
using BLComponent.Models;
using BLComponent.StateMachine;
using Moq;

namespace BLComponent.Tests;

public class ScanStateMachineTests
{
    private readonly Mock<IScanRepository> _repository = new();
    private readonly ScanStateMachine _stateMachine;
    private readonly DateTimeOffset _at = DateTimeOffset.UtcNow;

    public ScanStateMachineTests()
        => _stateMachine = new ScanStateMachine(_repository.Object);
    
    [Theory, AutoData]
    public async Task TryMarkRunningAsync_FromQueued_ReturnsTrueAndUpdatesStatus(Scan current)
    {
        current = current with { Status = ScanStatus.Queued };
        _repository
            .Setup(r => 
                r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repository
            .Setup(r => 
                r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _stateMachine.TryMarkRunningAsync(current.Id, _at, CancellationToken.None);

        Assert.True(result);
        _repository.Verify(r => 
            r.GetAsync(current.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.TryUpdateAsync(
                It.Is<Scan>(s =>
                    s.Id == current.Id &&
                    s.Status == ScanStatus.Running &&
                    s.LastUpdatedAt == _at),
                current,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [InlineAutoData(ScanStatus.Running)]
    [InlineAutoData(ScanStatus.Completed)]
    [InlineAutoData(ScanStatus.Failed)]
    [InlineAutoData(ScanStatus.Cancelled)]
    public async Task TryMarkRunningAsync_FromNonQueued_ReturnsFalse(ScanStatus from, Scan current)
    {
        current = current with { Status = from };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var result = await _stateMachine.TryMarkRunningAsync(current.Id, _at, CancellationToken.None);

        Assert.False(result);
        _repository.Verify(r => 
            r.GetAsync(current.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => 
            r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task TryMarkRunningAsync_MissingId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);

        var result = await _stateMachine.TryMarkRunningAsync(id, _at, CancellationToken.None);

        Assert.False(result);
        _repository.Verify(r => 
            r.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => 
            r.TryUpdateAsync(It.IsAny<Scan>(), It.IsAny<Scan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Theory, AutoData]
    public async Task TryMarkRunningAsync_CasFails_RetriesAndSucceeds(Scan current)
    {
        current = current with { Status = ScanStatus.Queued };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repository
            .SetupSequence(r => r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var result = await _stateMachine.TryMarkRunningAsync(current.Id, _at, CancellationToken.None);

        Assert.True(result);
        _repository.Verify(r => r.TryUpdateAsync(
                It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
    
    [Theory, AutoData]
    public async Task TryMarkCompletedAsync_FromRunning_ReturnsTrue(Scan current)
    {
        current = current with { Status = ScanStatus.Running };
        var scanResult = new ScanResult([new ScanFinding("X", SeverityLevel.High)]);
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repository
            .Setup(r => r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _stateMachine.TryMarkCompletedAsync(
            current.Id, scanResult, _at, CancellationToken.None);

        Assert.True(result);
        _repository.Verify(r => r.TryUpdateAsync(
                It.Is<Scan>(s => s.Status == ScanStatus.Completed && s.Result == scanResult),
                current,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [InlineAutoData(ScanStatus.Queued)]
    [InlineAutoData(ScanStatus.Completed)]
    [InlineAutoData(ScanStatus.Failed)]
    [InlineAutoData(ScanStatus.Cancelled)]
    public async Task TryMarkCompletedAsync_FromNonRunning_ReturnsFalse(ScanStatus from, Scan current)
    {
        current = current with { Status = from };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        var scanResult = new ScanResult([]);

        var result = await _stateMachine.TryMarkCompletedAsync(
            current.Id, scanResult, _at, CancellationToken.None);

        Assert.False(result);
    }
    
    [Fact]
    public async Task TryMarkCompletedAsync_MissingId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);
        var scanResult = new ScanResult([]);

        var result = await _stateMachine.TryMarkCompletedAsync(
            id, scanResult, _at, CancellationToken.None);

        Assert.False(result);
        _repository.Verify(r => r.TryUpdateAsync(
            It.IsAny<Scan>(), It.IsAny<Scan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Theory, AutoData]
    public async Task TryMarkFailedAsync_FromRunning_ReturnsTrue(Scan current)
    {
        current = current with { Status = ScanStatus.Running };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repository
            .Setup(r => r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _stateMachine.TryMarkFailedAsync(
            current.Id, "boom", _at, CancellationToken.None);

        Assert.True(result);
        _repository.Verify(r => r.TryUpdateAsync(
                It.Is<Scan>(s => s.Status == ScanStatus.Failed && s.ErrorMessage == "boom"),
                current,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [InlineAutoData(ScanStatus.Queued)]
    [InlineAutoData(ScanStatus.Completed)]
    [InlineAutoData(ScanStatus.Failed)]
    [InlineAutoData(ScanStatus.Cancelled)]
    public async Task TryMarkFailedAsync_FromNonRunning_ReturnsFalse(ScanStatus from, Scan current)
    {
        current = current with { Status = from };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var result = await _stateMachine.TryMarkFailedAsync(
            current.Id, "boom", _at, CancellationToken.None);

        Assert.False(result);
    }
    
    [Fact]
    public async Task TryMarkFailedAsync_MissingId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);

        var result = await _stateMachine.TryMarkFailedAsync(
            id, "boom", _at, CancellationToken.None);

        Assert.False(result);
        _repository.Verify(r => r.TryUpdateAsync(
            It.IsAny<Scan>(), It.IsAny<Scan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Theory]
    [InlineAutoData(ScanStatus.Queued)]
    [InlineAutoData(ScanStatus.Running)]
    public async Task TryMarkCancelledAsync_FromActive_ReturnsTrue(ScanStatus from, Scan current)
    {
        current = current with { Status = from };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repository
            .Setup(r => r.TryUpdateAsync(It.IsAny<Scan>(), current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _stateMachine.TryMarkCancelledAsync(current.Id, _at, CancellationToken.None);

        Assert.True(result);
        _repository.Verify(r => r.TryUpdateAsync(
                It.Is<Scan>(s => s.Status == ScanStatus.Cancelled && s.LastUpdatedAt == _at),
                current,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [InlineAutoData(ScanStatus.Completed)]
    [InlineAutoData(ScanStatus.Failed)]
    [InlineAutoData(ScanStatus.Cancelled)]
    public async Task TryMarkCancelledAsync_FromTerminal_ReturnsFalse(ScanStatus from, Scan current)
    {
        current = current with { Status = from };
        _repository
            .Setup(r => r.GetAsync(current.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var result = await _stateMachine.TryMarkCancelledAsync(current.Id, _at, CancellationToken.None);

        Assert.False(result);
    }
    
    [Fact]
    public async Task TryMarkCancelledAsync_MissingId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);

        var result = await _stateMachine.TryMarkCancelledAsync(
            id, _at, CancellationToken.None);

        Assert.False(result);
        _repository.Verify(r => r.TryUpdateAsync(
            It.IsAny<Scan>(), It.IsAny<Scan>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
