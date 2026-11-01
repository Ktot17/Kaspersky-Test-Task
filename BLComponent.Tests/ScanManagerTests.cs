using AutoFixture.Xunit2;
using BLComponent.Abstractions;
using BLComponent.Enums;
using BLComponent.Exceptions;
using BLComponent.Managers;
using BLComponent.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BLComponent.Tests;

public class ScanManagerTests
{
    private readonly Mock<IScanRepository> _repository = new();
    private readonly Mock<IScanStateMachine> _stateMachine = new();
    private readonly Mock<IScanQueue> _queue = new();
    private readonly Mock<IRunningScanRegistry> _registry = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly ScanManager _manager;

    public ScanManagerTests()
        => _manager = new ScanManager(
            _repository.Object, _stateMachine.Object, _queue.Object,
            _registry.Object, _clock, NullLogger<ScanManager>.Instance);

    [Theory, AutoData]
    public async Task StartScanAsync_QueueAccepts_PersistsQueuedAndPushesId(Uri url)
    {
        _queue.Setup(q => q.TryPush(It.IsAny<Guid>())).Returns(true);
        var now = _clock.GetUtcNow();

        var id = await _manager.StartScanAsync(url, CancellationToken.None);

        _repository.Verify(r => r.AddAsync(
            It.Is<Scan>(s =>
                s.Id == id &&
                s.RepositoryUrl == url &&
                s.Status == ScanStatus.Queued &&
                s.CreatedAt == now &&
                s.LastUpdatedAt == now &&
                s.Result == null &&
                s.ErrorMessage == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _queue.Verify(q => q.TryPush(id), Times.Once);
    }

    [Theory, AutoData]
    public async Task StartScanAsync_QueueRejects_ThrowsAndMarksFailed(Uri url)
    {
        _queue.Setup(q => q.TryPush(It.IsAny<Guid>())).Returns(false);
        using var requestCts = new CancellationTokenSource();
        var requestToken = requestCts.Token;

        var act = async () => await _manager.StartScanAsync(url, requestToken);

        await Assert.ThrowsAsync<ScanQueueUnavailableException>(act);
        _stateMachine.Verify(s => s.TryMarkFailedAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(),
            CancellationToken.None),
            Times.Once);
    }

    [Theory, AutoData]
    public async Task GetScanAsync_Existing_ReturnsRecord(Scan scan)
    {
        _repository
            .Setup(r => r.GetAsync(scan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scan);

        var result = await _manager.GetScanAsync(scan.Id, CancellationToken.None);

        Assert.Same(scan, result);
    }

    [Fact]
    public async Task GetScanAsync_Missing_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);

        var result = await _manager.GetScanAsync(id, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CancelScanAsync_Missing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Scan?)null);

        var result = await _manager.CancelScanAsync(id, CancellationToken.None);

        Assert.Equal(CancelScanOutcome.NotFound, result.Outcome);
        Assert.Null(result.Scan);
    }

    [Theory, AutoData]
    public async Task CancelScanAsync_Active_ReturnsCancelledAndSignals(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Running };
        var cancelled = scan with { Status = ScanStatus.Cancelled };
        _repository
            .SetupSequence(r => r.GetAsync(scan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scan)
            .ReturnsAsync(cancelled);
        _stateMachine
            .Setup(s => s.TryMarkCancelledAsync(
                scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        using var requestCts = new CancellationTokenSource();

        var result = await _manager.CancelScanAsync(scan.Id, requestCts.Token);

        Assert.Equal(CancelScanOutcome.Cancelled, result.Outcome);
        Assert.Same(cancelled, result.Scan);
        _registry.Verify(r => r.TryCancel(scan.Id), Times.Once);
        _stateMachine.Verify(s => s.TryMarkCancelledAsync(
            scan.Id, It.IsAny<DateTimeOffset>(), CancellationToken.None),
            Times.Once);
    }

    [Theory, AutoData]
    public async Task CancelScanAsync_AlreadyTerminal_ReturnsAlreadyTerminalWithoutSignaling(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Completed };
        _repository
            .SetupSequence(r => r.GetAsync(scan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scan)
            .ReturnsAsync(scan);
        _stateMachine
            .Setup(s => s.TryMarkCancelledAsync(
                scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _manager.CancelScanAsync(scan.Id, CancellationToken.None);

        Assert.Equal(CancelScanOutcome.AlreadyTerminal, result.Outcome);
        Assert.Same(scan, result.Scan);
        _registry.Verify(r => r.TryCancel(It.IsAny<Guid>()), Times.Never);
    }
}
