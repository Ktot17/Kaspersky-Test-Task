using System.Threading.Channels;
using DBComponent.Queues;

namespace DBComponent.Tests;

public class ScanQueueTests
{
    private const int QueueCount = 3;
    private readonly Channel<Guid> _channel = 
        Channel.CreateBounded<Guid>(new BoundedChannelOptions(QueueCount) 
        { 
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly ScanQueue _scans;
    
    public ScanQueueTests()
        => _scans = new ScanQueue(_channel);

    [Fact]
    public void TryPush_WhenNotFull_ReturnsTrue()
    {
        var result = _scans.TryPush(Guid.NewGuid());
        
        Assert.True(result);
        Assert.Equal(1, _channel.Reader.Count);
    }

    [Fact]
    public void TryPush_WhenFull_ReturnsFalse()
    {
        for (var i = 0; i < QueueCount; ++i)
            _channel.Writer.TryWrite(Guid.NewGuid());
        
        var result = _scans.TryPush(Guid.NewGuid());
        
        Assert.False(result);
        Assert.Equal(QueueCount, _channel.Reader.Count);
    }

    [Fact]
    public void TryPush_AfterFullThenRead_SucceedsAgain()
    {
        for (var i = 0; i < QueueCount; ++i)
            _channel.Writer.TryWrite(Guid.NewGuid());
        _channel.Reader.TryRead(out _);
        
        var result = _scans.TryPush(Guid.NewGuid());
        
        Assert.True(result);
        Assert.Equal(QueueCount, _channel.Reader.Count);
    }

    [Fact]
    public async Task ReadAllAsync_AfterComplete_YieldsAllInPushOrder()
    {
        List<Guid> ids = [.. Enumerable.Range(0, QueueCount).Select(_ => Guid.NewGuid())];
        foreach (var id in ids)
            _channel.Writer.TryWrite(id);
        _channel.Writer.Complete();

        List<Guid> result = [];
        await foreach (var item in _scans.ReadAllAsync(CancellationToken.None))
            result.Add(item);
        
        Assert.Equal(ids, result);
    }
    
    [Fact]
    public async Task ReadAllAsync_AfterCompleteAndEmpty_CompletesImmediately()
    {
        _channel.Writer.Complete();

        var received = new List<Guid>();
        await foreach (var id in _scans.ReadAllAsync(CancellationToken.None))
            received.Add(id);

        Assert.Empty(received);
    }
    
    [Fact]
    public async Task ReadAllAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var enumerator = _scans.ReadAllAsync(cts.Token).GetAsyncEnumerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public void ReadAllAsync_WhenEmpty_DoesNotComplete()
    {
        var enumerator = _scans.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        var moveNext = enumerator.MoveNextAsync().AsTask();
        Assert.False(moveNext.IsCompleted);
    }
}
