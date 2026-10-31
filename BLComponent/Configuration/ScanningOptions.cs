namespace BLComponent.Configuration;

public sealed class ScanningOptions
{
    public int MaxParallelScans { get; init; } = 3;
    
    public int QueueCapacity { get; init; } = 1000;
}
