using System.Threading.Channels;
using AutoFixture.Xunit2;
using BLComponent.Abstractions;
using BLComponent.Enums;
using BLComponent.Exceptions;
using BLComponent.Models;
using BLComponent.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace BLComponent.Tests;

public class ScanWorkerTests : IAsyncLifetime
{
    private readonly Mock<IScanQueue> _queue = new();
    private readonly Mock<IScanRepository> _repository = new();
    private readonly Mock<IScanStateMachine> _stateMachine = new();
    private readonly Mock<IScanner> _scanner = new();
    private readonly Mock<IRunningScanRegistry> _registry = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    private readonly ScanWorker _worker;

    public ScanWorkerTests()
    {
        _queue
            .Setup(q => q.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _channel.Reader.ReadAllAsync(ct));

        _worker = new ScanWorker(
            _queue.Object, _repository.Object, _stateMachine.Object,
            _scanner.Object, _registry.Object, _clock,
            NullLogger<ScanWorker>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _worker.StopAsync(cts.Token);
    }

    [Theory, AutoData]
    public async Task ExecuteAsync_QueuedScan_CompletesSuccessfully(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Queued };
        var result = new ScanResult([new ScanFinding("X", SeverityLevel.High)]);
        var unregistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository
            .Setup(r => r.GetAsync(scan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scan);
        _stateMachine
            .Setup(s => s.TryMarkRunningAsync(scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _stateMachine
            .Setup(s => s.TryMarkCompletedAsync(scan.Id, result, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _scanner
            .Setup(s => s.ScanAsync(scan.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _registry
            .Setup(r => r.Unregister(scan.Id))
            .Callback(() => unregistered.TrySetResult());
        _channel.Writer.TryWrite(scan.Id);

        await _worker.StartAsync(CancellationToken.None);
        await unregistered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _stateMachine.Verify(s => s.TryMarkRunningAsync(
            scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _stateMachine.Verify(s => s.TryMarkCompletedAsync(
            scan.Id, result, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _registry.Verify(r => r.Register(scan.Id, It.IsAny<CancellationTokenSource>()), Times.Once);
        _registry.Verify(r => r.Unregister(scan.Id), Times.Once);
    }

    [Theory, AutoData]
    public async Task ExecuteAsync_AlreadyTerminal_DoesNotInvokeScanner(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Cancelled };
        var runningChecked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stateMachine
            .Setup(s => s.TryMarkRunningAsync(scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => runningChecked.TrySetResult())
            .ReturnsAsync(false);
        _channel.Writer.TryWrite(scan.Id);

        await _worker.StartAsync(CancellationToken.None);
        await runningChecked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _scanner.Verify(s => s.ScanAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoData]
    public async Task ExecuteAsync_ScannerThrowsScanFailed_MarksFailed(Scan scan)
    {
        scan = scan with { Status = ScanStatus.Queued };
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository
            .Setup(r => r.GetAsync(scan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scan);
        _stateMachine
            .Setup(s => s.TryMarkRunningAsync(scan.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _scanner
            .Setup(s => s.ScanAsync(scan.RepositoryUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScanFailedException("boom"));
        _stateMachine
            .Setup(s => s.TryMarkFailedAsync(scan.Id, "boom", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => failed.TrySetResult())
            .ReturnsAsync(true);
        _channel.Writer.TryWrite(scan.Id);

        await _worker.StartAsync(CancellationToken.None);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _stateMachine.Verify(s => s.TryMarkFailedAsync(
            scan.Id, "boom", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
