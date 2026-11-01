namespace BLComponent.Abstractions;

public interface IScanQueue
{
    public bool TryPush(
        Guid id);
    
    public IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken ct);
}
