using System.Threading.Channels;
using BLComponent.Configuration;
using BLComponent.InputPorts;
using Microsoft.Extensions.Options;

namespace DBComponent;

public sealed class ScanQueue(IOptions<ScanningOptions> options) : IScanQueue
{
    private readonly Channel<Guid> _channel = 
        Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    
    public bool TryPush(Guid id)
        => _channel.Writer.TryWrite(id);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
