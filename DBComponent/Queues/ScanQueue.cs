using System.Threading.Channels;
using BLComponent.Abstractions;
using BLComponent.Configuration;
using Microsoft.Extensions.Options;

namespace DBComponent.Queues;

public sealed class ScanQueue : IScanQueue
{
    private readonly Channel<Guid> _channel;
    
    internal ScanQueue(
        Channel<Guid> channel) 
        => _channel = channel;
    
    public ScanQueue(
        IOptions<ScanningOptions> options) 
        : this(CreateDefaultChannel(options)) {}
    
    public bool TryPush(Guid id)
        => _channel.Writer.TryWrite(id);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
    
    private static Channel<Guid> CreateDefaultChannel(IOptions<ScanningOptions> options)
        => Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.QueueCapacity) 
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
}
